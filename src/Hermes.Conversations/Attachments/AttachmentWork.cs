using System;
using System.Collections.Generic;
using System.Linq;
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

namespace Hermes.Conversations.Attachments;

/// <summary>
/// One pass of the <c>attachment-scans</c> job (<c>09</c> MSG-CONV-007, MSG-CONV-009; M-037),
/// in a scope of its own. It lets go of each attachment no message named within 24 hours, so
/// none can name it any longer; it removes, stored bytes first, each row whose conversation went,
/// whose message went or was deleted, or whose name an erasure cleared, a store deletion that
/// fails being tried again at the next pass; it deletes an infected attachment's bytes, keeping
/// its row; and it scans each pending attachment that is due: <c>Clean</c> opens it,
/// <c>Infected</c> deletes its bytes, and <c>Failed</c> tries again with backoff, marking it
/// <c>failed</c> and raising <c>attachment-scan</c> at the fifth.
/// </summary>
internal sealed partial class AttachmentWork(
    MessagingDatabase database,
    ContentProtection protection,
    Alerts alerts,
    IOptionsMonitor<MessagingOptions> options,
    IServiceProvider services,
    TimeProvider clock,
    ILogger<AttachmentWork> logger)
{
    /// <summary>The scans an attachment is given before it is marked failed.</summary>
    internal const int MostScans = 5;

    /// <summary>The condition of an attachment marked failed.</summary>
    internal const string Condition = "attachment-scan";

    /// <summary>How long an attachment waits to be named by a message.</summary>
    internal static readonly TimeSpan Unnamed = TimeSpan.FromHours(24);

    private const string LetGo = """
        UPDATE messaging.attachments SET conversation_id = NULL
        WHERE conversation_id IS NOT NULL AND named_at IS NULL AND created_at < @unnamed
        """;

    private const string Removable = """
        SELECT * FROM messaging.attachments
        WHERE conversation_id IS NULL OR name IS NULL OR (named_at IS NOT NULL AND message_id IS NULL)
           OR (status = 'infected' AND store_key IS NOT NULL)
        ORDER BY created_at, id
        LIMIT @batch OFFSET @skip
        """;

    private const string Remove = """
        DELETE FROM messaging.attachments
        WHERE id = @id AND store_key IS NOT DISTINCT FROM @key
          AND (conversation_id IS NULL OR name IS NULL OR (named_at IS NOT NULL AND message_id IS NULL))
        """;

    private const string Emptied = "UPDATE messaging.attachments SET store_key = NULL WHERE id = @id AND store_key = @key AND status = 'infected'";

    private const string Due = """
        SELECT * FROM messaging.attachments
        WHERE status = 'pending' AND conversation_id IS NOT NULL AND name IS NOT NULL AND store_key IS NOT NULL AND scan_at <= @now
        ORDER BY scan_at, id
        LIMIT @batch
        """;

    private const string Scanned = """
        UPDATE messaging.attachments SET status = @status, scan_attempts = @attempts, scan_at = @scan_at
        WHERE id = @id AND status = 'pending'
        """;

    private const string Tombstone = """
        INSERT INTO messaging.attachments (id, media_type, size, chunks, store_key, status, scan_attempts, created_at)
        VALUES (@id, 'application/octet-stream', 0, 0, @key, 'failed', 0, @now)
        """;

    /// <summary>
    /// Leaves stored bytes no row names to the job: a row without a conversation keeps
    /// <paramref name="key"/>, so the next pass deletes them, in a transaction of its own.
    /// </summary>
    internal static async Task LeaveForTheJobAsync(MessagingDatabase database, MessagingOptions current, string key, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await LibraryTransactions.RunAsync(
            database,
            current,
            (context, token) => LeaveForTheJobAsync(context, key, now, token),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>As <see cref="LeaveForTheJobAsync(MessagingDatabase, MessagingOptions, string, DateTimeOffset, CancellationToken)"/>, in <paramref name="context"/>'s transaction.</summary>
    internal static Task<int> LeaveForTheJobAsync(MessagingContext context, string key, DateTimeOffset now, CancellationToken cancellationToken)
    {
        return context.Database.ExecuteSqlRawAsync(
            Tombstone,
            [new NpgsqlParameter("id", Guid.CreateVersion7()), new NpgsqlParameter("key", key), new NpgsqlParameter("now", now)],
            cancellationToken);
    }

    /// <summary>Runs one pass at <paramref name="now"/>, renewing the job's claim between units; it stops where a renewal finds the claim no longer the run's.</summary>
    internal async Task PassAsync(DateTimeOffset now, Func<CancellationToken, Task<bool>> renew, CancellationToken cancellationToken)
    {
        IAttachmentStore store = services.GetRequiredService<IAttachmentStore>();
        MessagingOptions current = options.CurrentValue;
        await LibraryTransactions.RunAsync(
            database,
            current,
            (context, token) => context.Database.ExecuteSqlRawAsync(LetGo, [new NpgsqlParameter("unnamed", now - Unnamed)], token),
            cancellationToken).ConfigureAwait(false);
        if (await RemoveAsync(store, current, renew, cancellationToken).ConfigureAwait(false))
        {
            await ScanAsync(store, now, current, renew, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Removes the rows that are to go, and empties the infected, a batch at a time; false where the claim was lost.</summary>
    private async Task<bool> RemoveAsync(IAttachmentStore store, MessagingOptions current, Func<CancellationToken, Task<bool>> renew, CancellationToken cancellationToken)
    {
        int batch = current.Jobs.BatchSize;
        int skipped = 0;
        while (true)
        {
            List<AttachmentRow> rows;
            MessagingContext reading = database.Context();
            await using (reading.ConfigureAwait(false))
            {
                rows = await reading.Attachments.FromSqlRaw(Removable, new NpgsqlParameter("batch", batch), new NpgsqlParameter("skip", skipped))
                    .AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
            }

            foreach (AttachmentRow row in rows)
            {
                if (row.StoreKey is { } key)
                {
                    try
                    {
                        await store.DeleteAsync(key, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception failure) when (failure is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                    {
                        // Tried again at the next pass; this pass reads past it.
                        DeleteFailed(logger, failure.GetType().Name);
                        skipped++;
                        continue;
                    }
                }

                bool infected = string.Equals(row.Status, ConversationSpellings.Of(AttachmentStatus.Infected), StringComparison.Ordinal);
                int changed = await LibraryTransactions.RunAsync(
                    database,
                    current,
                    (context, token) => context.Database.ExecuteSqlRawAsync(
                        Remove,
                        [new NpgsqlParameter("id", row.Id), new NpgsqlParameter("key", NpgsqlTypes.NpgsqlDbType.Text) { Value = (object?)row.StoreKey ?? DBNull.Value }],
                        token),
                    cancellationToken).ConfigureAwait(false);
                if (changed == 0 && infected && row.StoreKey is { } emptied)
                {
                    changed = await LibraryTransactions.RunAsync(
                        database,
                        current,
                        (context, token) => context.Database.ExecuteSqlRawAsync(Emptied, [new NpgsqlParameter("id", row.Id), new NpgsqlParameter("key", emptied)], token),
                        cancellationToken).ConfigureAwait(false);
                }

                // A row changed meanwhile, as by re-protection, is read again at the next pass.
                if (changed == 0)
                {
                    skipped++;
                }
            }

            if (!await renew(cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            if (rows.Count < batch)
            {
                return true;
            }
        }
    }

    /// <summary>Scans each pending attachment that is due, a batch at a time; false where the claim was lost.</summary>
    private async Task<bool> ScanAsync(IAttachmentStore store, DateTimeOffset now, MessagingOptions current, Func<CancellationToken, Task<bool>> renew, CancellationToken cancellationToken)
    {
        IAttachmentScanner scanner = services.GetRequiredService<IAttachmentScanner>();
        List<AttachmentRow> rows;
        MessagingContext reading = database.Context();
        await using (reading.ConfigureAwait(false))
        {
            rows = await reading.Attachments.FromSqlRaw(Due, new NpgsqlParameter("now", now), new NpgsqlParameter("batch", current.Jobs.BatchSize))
                .AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        bool decided = false;
        foreach (AttachmentRow row in rows)
        {
            (ScanResult result, bool rejected) = await ScanOneAsync(store, scanner, row, current, cancellationToken).ConfigureAwait(false);
            if (rejected)
            {
                await alerts.HoldsUntilAsync(
                    "content-rejected",
                    ConversationTables.Attachments,
                    new Dictionary<string, string>(StringComparer.Ordinal) { ["table"] = ConversationTables.Attachments },
                    cancellationToken).ConfigureAwait(false);
            }

            int attempts = row.ScanAttempts + 1;
            (AttachmentStatus status, DateTimeOffset? next) = result switch
            {
                ScanResult.Clean => (AttachmentStatus.Clean, (DateTimeOffset?)null),
                ScanResult.Infected => (AttachmentStatus.Infected, null),
                _ when attempts >= MostScans => (AttachmentStatus.Failed, null),
                _ => (AttachmentStatus.Pending, now + TimeSpan.FromMinutes(1 << (attempts - 1))),
            };
            int changed = await LibraryTransactions.RunAsync(
                database,
                current,
                (context, token) => context.Database.ExecuteSqlRawAsync(
                    Scanned,
                    [
                        new NpgsqlParameter("id", row.Id),
                        new NpgsqlParameter("status", ConversationSpellings.Of(status)),
                        new NpgsqlParameter("attempts", attempts),
                        new NpgsqlParameter("scan_at", NpgsqlTypes.NpgsqlDbType.TimestampTz) { Value = next is { } at ? at : DBNull.Value },
                    ],
                    token),
                cancellationToken).ConfigureAwait(false);
            if (changed == 1)
            {
                decided |= status is AttachmentStatus.Clean or AttachmentStatus.Infected;
                if (status == AttachmentStatus.Infected)
                {
                    await EmptyAsync(store, row, current, cancellationToken).ConfigureAwait(false);
                }

                if (status == AttachmentStatus.Failed)
                {
                    await alerts.HoldingAsync(Condition, null, new Dictionary<string, string>(StringComparer.Ordinal) { ["attachment"] = row.Id.ToString("D") }, cancellationToken).ConfigureAwait(false);
                }
            }

            if (!await renew(cancellationToken).ConfigureAwait(false))
            {
                return false;
            }
        }

        if (decided)
        {
            await alerts.NotHoldingAsync(Condition, null, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>
    /// Scans <paramref name="row"/>'s content as it is opened; a content that cannot be read,
    /// or a scanner that throws, answers <c>Failed</c>, and a chunk that does not open under its
    /// binding is answered as rejected too.
    /// </summary>
    private async Task<(ScanResult Result, bool Rejected)> ScanOneAsync(IAttachmentStore store, IAttachmentScanner scanner, AttachmentRow row, MessagingOptions current, CancellationToken cancellationToken)
    {
        bool held = false;
        AttachmentFiles.Content? content;
        try
        {
            content = await AttachmentOpening.ContentAsync(protection, store, row, current.Dispatch.Timeout, clock, _ => held = true, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception failure) when (failure is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            ScanFailed(logger, failure.GetType().Name);
            return (ScanResult.Failed, false);
        }

        if (content is null)
        {
            return (ScanResult.Failed, false);
        }

        await using (content.ConfigureAwait(false))
        {
            try
            {
                return (await scanner.ScanAsync(content, row.MediaType, cancellationToken).ConfigureAwait(false), false);
            }
            catch (Exception failure) when (failure is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                ScanFailed(logger, failure.GetType().Name);
                return (ScanResult.Failed, content.Refused && !held);
            }
        }
    }

    /// <summary>Deletes an infected attachment's bytes, keeping its row; a deletion that fails is tried again at the next pass.</summary>
    private async Task EmptyAsync(IAttachmentStore store, AttachmentRow row, MessagingOptions current, CancellationToken cancellationToken)
    {
        try
        {
            await store.DeleteAsync(row.StoreKey!, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception failure) when (failure is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            DeleteFailed(logger, failure.GetType().Name);
            return;
        }

        await LibraryTransactions.RunAsync(
            database,
            current,
            (context, token) => context.Database.ExecuteSqlRawAsync(Emptied, [new NpgsqlParameter("id", row.Id), new NpgsqlParameter("key", row.StoreKey!)], token),
            cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(EventId = 79, EventName = "AttachmentDeleteFailed", Level = LogLevel.Warning, Message = "Deleting an attachment's stored bytes failed with {Failure}; the next pass tries again.")]
    private static partial void DeleteFailed(ILogger logger, string failure);

    [LoggerMessage(EventId = 80, EventName = "AttachmentScanFailed", Level = LogLevel.Warning, Message = "Scanning an attachment failed with {Failure}.")]
    private static partial void ScanFailed(ILogger logger, string failure);
}