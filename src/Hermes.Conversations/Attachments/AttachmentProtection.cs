using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Operations;
using Hermes.Sending;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

using NpgsqlTypes;

namespace Hermes.Conversations.Attachments;

/// <summary>
/// The retention job's step for attachments (<c>06</c> MSG-OPS-008; <c>09</c> MSG-CONV-009;
/// M-037): each attachment whose name and chunks the library protected under a
/// <c>contentKeys</c> version below the write version is protected again, its chunks read from
/// the store and written, opened and protected again one at a time, to a new object under a new
/// key; its row then names the new key, where it still names the old one, and a row without a
/// conversation keeps the old key, so the <c>attachment-scans</c> job deletes the old object. A
/// value a host's protector wrote is left as it is.
/// </summary>
internal sealed partial class AttachmentProtection(
    MessagingDatabase database,
    LibraryProtector library,
    IServiceScopeFactory scopes,
    IOptionsMonitor<MessagingOptions> options,
    TimeProvider clock,
    ILogger<AttachmentProtection> logger) : IProtectAgain
{
    private const string Due = """
        SELECT * FROM messaging.attachments
        WHERE content_key_version < @write AND store_key IS NOT NULL AND name IS NOT NULL AND id > @after
        ORDER BY id LIMIT @batch
        """;

    private const string Moved = """
        UPDATE messaging.attachments SET name = @name, content_key_version = @version, store_key = @key
        WHERE id = @id AND store_key = @was_key AND name = @was_name
        """;

    private enum Again
    {
        Protected,
        Left,
        Refused,
    }

    public async Task<bool> ProtectAgainAsync(int write, Func<Task<bool>> renew, CancellationToken cancellationToken)
    {
        AsyncServiceScope scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            if (scope.ServiceProvider.GetService<IAttachmentStore>() is not { } store)
            {
                return true;
            }

            MessagingOptions current = options.CurrentValue;
            Guid after = Guid.Empty;
            int protectedRows = 0;
            while (true)
            {
                List<AttachmentRow> rows;
                MessagingContext reading = database.Context();
                await using (reading.ConfigureAwait(false))
                {
                    rows = await reading.Attachments.FromSqlRaw(Due, new NpgsqlParameter("write", write), new NpgsqlParameter("after", after), new NpgsqlParameter("batch", current.Jobs.BatchSize))
                        .AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
                }

                bool refused = false;
                foreach (AttachmentRow row in rows)
                {
                    after = row.Id;
                    Again outcome = await OneAsync(store, row, write, current, cancellationToken).ConfigureAwait(false);
                    protectedRows += outcome == Again.Protected ? 1 : 0;
                    if (outcome == Again.Refused)
                    {
                        refused = true;
                        break;
                    }
                }

                if (!await renew().ConfigureAwait(false))
                {
                    return false;
                }

                if (refused || rows.Count < current.Jobs.BatchSize)
                {
                    break;
                }
            }

            if (protectedRows > 0)
            {
                ProtectedAgain(logger, protectedRows);
            }

            return true;
        }
    }

    /// <summary>Protects one attachment again; left where it is not the library's, does not open, or changed meanwhile; refused where no value can be reserved.</summary>
    private async Task<Again> OneAsync(IAttachmentStore store, AttachmentRow row, int write, MessagingOptions current, CancellationToken cancellationToken)
    {
        string person = row.Owner ?? string.Empty;
        (Again named, byte[]? name) = await AgainAsync(person, row.Name!, AttachmentFiles.NameContext(row.Id), cancellationToken).ConfigureAwait(false);
        if (named != Again.Protected)
        {
            return named;
        }

        if (await store.OpenAsync(row.StoreKey!, cancellationToken).ConfigureAwait(false) is not { } stored)
        {
            return Again.Left;
        }

        string key = AttachmentFiles.NewKey();
        AttachmentFiles.Streamed streamed;
        AttachmentFiles.Content content = new(stored, row.Chunks, async (chunk, frame, token) =>
        {
            ContentEnvelope.Envelope envelope = ContentEnvelope.Read(frame);
            if (envelope.Form != ContentEnvelope.EnvelopeForm.Value || !string.Equals(envelope.Id, LibraryProtector.LibraryId, StringComparison.Ordinal))
            {
                return null;
            }

            return await library.UnprotectAsync(person, envelope.Output, AttachmentFiles.ChunkContext(row.Id, chunk)).ConfigureAwait(false) is UnprotectResult.Opened opened
                ? opened.Plaintext.ToArray()
                : null;
        });
        await using (content.ConfigureAwait(false))
        {
            try
            {
                byte[] first = new byte[AttachmentFiles.ChunkBytes];
                int read = await AttachmentFiles.FillAsync(content, first, cancellationToken).ConfigureAwait(false);
                streamed = await AttachmentFiles.WriteAsync(
                    store,
                    key,
                    first.AsMemory(0, read),
                    content,
                    long.MaxValue,
                    async (chunk, plaintext, token) =>
                    {
                        ContentProtecting made = await library.ProtectAsync(person, plaintext, AttachmentFiles.ChunkContext(row.Id, chunk), token).ConfigureAwait(false);
                        return made.Stored is { } output ? ContentEnvelope.Wrap(LibraryProtector.LibraryId, output) : null;
                    },
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception failure) when (failure is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                Left(logger, failure.GetType().Name);
                await AttachmentWork.LeaveForTheJobAsync(database, current, key, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
                return Again.Left;
            }
        }

        if (streamed.Result != AttachmentFiles.Streamed.Outcome.Written)
        {
            await AttachmentWork.LeaveForTheJobAsync(database, current, key, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
            return Again.Refused;
        }

        return await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                int changed = await context.Database.ExecuteSqlRawAsync(
                    Moved,
                    [
                        new NpgsqlParameter("id", row.Id),
                        new NpgsqlParameter("version", write),
                        new NpgsqlParameter("key", key),
                        new NpgsqlParameter("was_key", row.StoreKey!),
                        new NpgsqlParameter("name", NpgsqlDbType.Bytea) { Value = name! },
                        new NpgsqlParameter("was_name", NpgsqlDbType.Bytea) { Value = row.Name! },
                    ],
                    token).ConfigureAwait(false);

                // The object no row names any longer goes at the job's next pass: the old one, or, where the row changed meanwhile, the new.
                await AttachmentWork.LeaveForTheJobAsync(context, changed == 1 ? row.StoreKey! : key, clock.GetUtcNow(), token).ConfigureAwait(false);
                return changed == 1 ? Again.Protected : Again.Left;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A library value opened and protected again under the write version; left where it is not the library's or does not open, refused where no value can be reserved.</summary>
    private async Task<(Again Outcome, byte[]? Stored)> AgainAsync(string person, byte[] stored, string context, CancellationToken cancellationToken)
    {
        ContentEnvelope.Envelope envelope = ContentEnvelope.Read(stored);
        if (envelope.Form != ContentEnvelope.EnvelopeForm.Value || !string.Equals(envelope.Id, LibraryProtector.LibraryId, StringComparison.Ordinal))
        {
            return (Again.Left, null);
        }

        if (await library.UnprotectAsync(person, envelope.Output, context).ConfigureAwait(false) is not UnprotectResult.Opened opened)
        {
            return (Again.Left, null);
        }

        ContentProtecting made = await library.ProtectAsync(person, opened.Plaintext, context, cancellationToken).ConfigureAwait(false);
        return made.Stored is null ? (Again.Refused, null) : (Again.Protected, ContentEnvelope.Wrap(LibraryProtector.LibraryId, made.Stored));
    }

    [LoggerMessage(EventId = 82, EventName = "AttachmentLeftUnprotected", Level = LogLevel.Warning, Message = "An attachment was left under its older key version: reading or writing its chunks failed with {Failure}.")]
    private static partial void Left(ILogger logger, string failure);

    [LoggerMessage(EventId = 83, EventName = "AttachmentsProtectedAgain", Level = LogLevel.Information, Message = "The retention job protected again {Count} attachments under the contentKeys write version.")]
    private static partial void ProtectedAgain(ILogger logger, int count);
}