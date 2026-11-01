using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Conversations.Attachments;
using Hermes.Conversations.Support;
using Hermes.Core;
using Hermes.Operations;
using Hermes.Sending;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace Hermes.Conversations.Chats;

/// <summary>
/// Attachments (<c>09</c> MSG-CONV-007, MSG-CONV-009; M-037): uploaded on their own into a
/// conversation the writer may write in, their name protected in the row and their bytes in
/// chunks streamed to the store; named by one message of their writer's; opened only clean,
/// after the conversation's access check. An attachment no message names yet is reached by its
/// uploader alone, and a visitor never reaches one in a <c>note</c>.
/// </summary>
internal sealed partial class StaffChats
{
    /// <summary>The most attachments a message names.</summary>
    internal const int MostAttachments = 10;

    private const string LockAttachments = "SELECT * FROM messaging.attachments WHERE id = ANY(@ids) FOR UPDATE";

    private const string Unname = "UPDATE messaging.attachments SET message_id = NULL WHERE message_id = @message";

    private static readonly string PendingStatus = ConversationSpellings.Of(AttachmentStatus.Pending);

    public async ValueTask<Result<AttachmentRecord>> UploadAttachmentAsync(string person, ConversationId conversation, string name, string mediaType, Stream content, CancellationToken cancellationToken)
    {
        Begin(person);
        ArgumentNullException.ThrowIfNull(content);
        if (!settings.Attachments.Enabled || await ReachAsync(person, conversation.Value, cancellationToken).ConfigureAwait(false) is not { } reached)
        {
            return Result.Fail<AttachmentRecord>(NotFound());
        }

        bool support = string.Equals(reached.Type, SupportType, StringComparison.Ordinal);
        if (support && (!string.Equals(reached.Assignee, person, StringComparison.Ordinal) || string.Equals(reached.Status, ClosedStatus, StringComparison.Ordinal)))
        {
            return Result.Fail<AttachmentRecord>(NotFound());
        }

        MessagingOptions current = options.CurrentValue;
        Uploader uploader = new(person, null, null, support ? reached.Customer : person);
        return await UploadAsync(
            reached.Id,
            uploader,
            name,
            mediaType,
            content,
            async (context, token) =>
            {
                await LockAsync(context, current, [person], token).ConfigureAwait(false);
                return await StaysAsync(context, reached, person, token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Result<AttachmentDownload>> OpenAttachmentAsync(string person, ConversationId conversation, AttachmentId attachment, CancellationToken cancellationToken)
    {
        Begin(person);
        if (!settings.Attachments.Enabled || await ReachAsync(person, conversation.Value, cancellationToken).ConfigureAwait(false) is not { } reached)
        {
            return Result.Fail<AttachmentDownload>(NotFound());
        }

        MessagingOptions current = options.CurrentValue;
        (bool stays, AttachmentRow? row) = await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockAsync(context, current, [person], token).ConfigureAwait(false);
                if (!await StaysAsync(context, reached, person, token).ConfigureAwait(false))
                {
                    return (false, null);
                }

                AttachmentRow? found = await context.Attachments.AsNoTracking()
                    .FirstOrDefaultAsync(each => each.Id == attachment.Value && each.ConversationId == reached.Id && each.Name != null && (each.MessageId != null || (each.NamedAt == null && each.Uploader == person)), token)
                    .ConfigureAwait(false);
                return (true, found);
            },
            cancellationToken).ConfigureAwait(false);
        return stays ? await DownloadAsync(row, current, cancellationToken).ConfigureAwait(false) : Result.Fail<AttachmentDownload>(NotFound());
    }

    /// <summary>
    /// Uploads the caller's attachment to their open conversation, where the public group takes
    /// attachments, under the public message write's protections, before any byte is stored: a
    /// blocked caller, or a visitor before the current notice is acknowledged, is refused as a
    /// message is, and each upload counts as one message toward the caller's messages limit.
    /// </summary>
    internal async Task<SupportAnswer<AttachmentRecord>> CallerUploadAsync(SupportCaller caller, Guid conversation, string name, string mediaType, Stream content, CancellationToken cancellationToken)
    {
        ready.ThrowIfNotReady();
        ConversationRow? reached = settings.Attachments.Public ? await CallerConversationAsync(caller, conversation, cancellationToken).ConfigureAwait(false) : null;
        if (reached is null || string.Equals(reached.Status, ClosedStatus, StringComparison.Ordinal))
        {
            return SupportAnswer<AttachmentRecord>.Fail(NotFound());
        }

        if (await RefusedCallerAsync(caller, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return SupportAnswer<AttachmentRecord>.Fail(refused);
        }

        (byte[] hash, int hashVersion) = caller.Counted(hashes);
        Uploader uploader = caller.Customer is { } customer ? new(customer, null, null, customer) : new(null, hash, hashVersion, null);
        MessagingOptions current = options.CurrentValue;
        TimeSpan? retryAfter = null;
        Result<AttachmentRecord> uploaded = await UploadAsync(
            reached.Id,
            uploader,
            name,
            mediaType,
            content,
            async (context, token) =>
            {
                await LockCallerAsync(context, current, caller, token).ConfigureAwait(false);
                return await CallerConversations(context, caller).AnyAsync(each => each.Id == reached.Id && each.Status != ClosedStatus, token).ConfigureAwait(false);
            },
            cancellationToken,
            admit: token => LibraryTransactions.RunAsync(
                database,
                current,
                async (context, inner) =>
                {
                    await LockCallerAsync(context, current, caller, inner).ConfigureAwait(false);
                    retryAfter = await CountAsync(context, VisitorMessagesKind, hash, hashVersion, clock.GetUtcNow(), TimeSpan.FromMinutes(1), settings.Support.VisitorMessages, inner).ConfigureAwait(false);
                    return retryAfter is null ? null : Plain(SupportCodes.Limited);
                },
                token)).ConfigureAwait(false);
        return uploaded.Failure is { } failed
            ? SupportAnswer<AttachmentRecord>.Fail(failed, retryAfter)
            : SupportAnswer<AttachmentRecord>.Of(uploaded.Value!);
    }

    /// <summary>Opens a clean attachment of the caller's conversation, never one in a <c>note</c>, where the public group takes attachments.</summary>
    internal async Task<Result<AttachmentDownload>> CallerOpenAsync(SupportCaller caller, Guid conversation, Guid attachment, CancellationToken cancellationToken)
    {
        ready.ThrowIfNotReady();
        if (!settings.Attachments.Public)
        {
            return Result.Fail<AttachmentDownload>(NotFound());
        }

        MessagingOptions current = options.CurrentValue;
        byte[][] every = caller.Hashes(hashes);
        (bool reached, AttachmentRow? row) = await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockCallerAsync(context, current, caller, token).ConfigureAwait(false);
                if (!await CallerConversations(context, caller).AnyAsync(each => each.Id == conversation, token).ConfigureAwait(false))
                {
                    return (false, null);
                }

                AttachmentRow? found = await context.Attachments.AsNoTracking()
                    .FirstOrDefaultAsync(each => each.Id == attachment && each.ConversationId == conversation && each.Name != null, token)
                    .ConfigureAwait(false);
                if (found is null)
                {
                    return (true, null);
                }

                bool reachable = found.MessageId is { } named
                    ? await context.Messages.AnyAsync(message => message.Id == named && message.Type != Note, token).ConfigureAwait(false)
                    : found.NamedAt is null && Uploaded(found, caller.Customer, every);
                return (true, reachable ? found : null);
            },
            cancellationToken).ConfigureAwait(false);
        return reached ? await DownloadAsync(row, current, cancellationToken).ConfigureAwait(false) : Result.Fail<AttachmentDownload>(NotFound());
    }

    /// <summary>
    /// Forgets an attachment just uploaded, as when the body that carried it held more than one
    /// file: its conversation is set to none, so the <c>attachment-scans</c> job deletes its bytes
    /// and its row.
    /// </summary>
    internal async Task ForgetAsync(Guid attachment, CancellationToken cancellationToken)
    {
        MessagingContext context = database.Context();
        await using (context.ConfigureAwait(false))
        {
            await context.Attachments.Where(each => each.Id == attachment).ExecuteUpdateAsync(set => set.SetProperty(each => each.ConversationId, (Guid?)null), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Whether <paramref name="row"/> was uploaded by <paramref name="customer"/>, or, where none, by the visitor whose hashes are <paramref name="visitorHashes"/>.</summary>
    private static bool Uploaded(AttachmentRow row, string? customer, byte[][] visitorHashes)
    {
        return customer is not null
            ? string.Equals(row.Uploader, customer, StringComparison.Ordinal)
            : row.Uploader is null && row.Visitor is { } visitor && visitorHashes.Any(hash => hash.AsSpan().SequenceEqual(visitor));
    }

    /// <summary>
    /// Checks a file's name and type, streams it to the store in protected chunks, and writes
    /// its row, pending its scan, where <paramref name="stays"/> still finds the conversation
    /// open to the uploader under their locks; its bytes are deleted again where it is refused
    /// after they were stored. Where given, <paramref name="admit"/> is asked once the file's
    /// checks pass and before any byte is stored, its refusal answered.
    /// </summary>
    private async Task<Result<AttachmentRecord>> UploadAsync(
        Guid conversation,
        Uploader uploader,
        string name,
        string mediaType,
        Stream content,
        Func<MessagingContext, CancellationToken, Task<bool>> stays,
        CancellationToken cancellationToken,
        Func<CancellationToken, Task<Failure?>>? admit = null)
    {
        if (!AttachmentFiles.IsName(name))
        {
            return Result.Fail<AttachmentRecord>(Invalid(ChatCodes.AttachmentInvalid, "name"));
        }

        AttachmentSettings bounds = settings.Attachments;
        if (AttachmentFiles.TypeOf(mediaType) is not { } type || !bounds.Types.Contains(type, StringComparer.Ordinal))
        {
            return Result.Fail<AttachmentRecord>(Plain(ChatCodes.AttachmentType));
        }

        byte[] first = new byte[AttachmentFiles.ChunkBytes];
        int read = await AttachmentFiles.FillAsync(content, first, cancellationToken).ConfigureAwait(false);
        if (!AttachmentFiles.Matches(type, first.AsSpan(0, read)))
        {
            return Result.Fail<AttachmentRecord>(Plain(ChatCodes.AttachmentType));
        }

        if (read > bounds.MaxBytes)
        {
            return Result.Fail<AttachmentRecord>(Plain(ChatCodes.AttachmentTooLarge));
        }

        if (admit is not null && await admit(cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return Result.Fail<AttachmentRecord>(refused);
        }

        Guid id = Guid.CreateVersion7();
        MessagingOptions current = options.CurrentValue;
        Protected named = await ProtectValueAsync(uploader.Owner, name, AttachmentFiles.NameContext(id), current, cancellationToken).ConfigureAwait(false);
        if (named.Failure is { } unavailable)
        {
            return Result.Fail<AttachmentRecord>(unavailable);
        }

        // The row records the lowest version of the name and the chunks, so no version they use is retired before they are protected again.
        int? lowest = named.Version;
        Failure? held = null;
        IAttachmentStore store = services.GetRequiredService<IAttachmentStore>();
        string key = AttachmentFiles.NewKey();
        AttachmentFiles.Streamed streamed;
        try
        {
            streamed = await AttachmentFiles.WriteAsync(
                store,
                key,
                first.AsMemory(0, read),
                content,
                bounds.MaxBytes,
                async (chunk, plaintext, token) =>
                {
                    Protected made = await ProtectBytesAsync(uploader.Owner, plaintext, AttachmentFiles.ChunkContext(id, chunk), current, token).ConfigureAwait(false);
                    if (made.Failure is { } failure)
                    {
                        held = failure;
                        return null;
                    }

                    if (made.Version is { } version)
                    {
                        lowest = lowest is { } low ? Math.Min(low, version) : version;
                    }

                    return made.Stored;
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            await DeleteStoredAsync(store, key).ConfigureAwait(false);
            throw;
        }

        if (streamed.Result != AttachmentFiles.Streamed.Outcome.Written)
        {
            await DeleteStoredAsync(store, key).ConfigureAwait(false);
            return Result.Fail<AttachmentRecord>(held ?? Plain(ChatCodes.AttachmentTooLarge));
        }

        AttachmentRow? row = null;
        try
        {
            row = await LibraryTransactions.RunAsync(
                database,
                current,
                async (context, token) =>
                {
                    if (!await stays(context, token).ConfigureAwait(false))
                    {
                        return null;
                    }

                    DateTimeOffset now = clock.GetUtcNow();
                    AttachmentRow written = new()
                    {
                        Id = id,
                        ConversationId = conversation,
                        Uploader = uploader.Person,
                        Visitor = uploader.Visitor,
                        HashVersion = uploader.HashVersion,
                        Owner = uploader.Owner,
                        Name = named.Stored,
                        ContentKeyVersion = lowest,
                        MediaType = type,
                        Size = streamed.Size,
                        Chunks = streamed.Chunks,
                        StoreKey = key,
                        Status = PendingStatus,
                        ScanAt = now,
                        CreatedAt = now,
                    };
                    context.Attachments.Add(written);
                    await context.SaveChangesAsync(token).ConfigureAwait(false);
                    return written;
                },
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (row is null)
            {
                await DeleteStoredAsync(store, key).ConfigureAwait(false);
            }
        }

        return row is null
            ? Result.Fail<AttachmentRecord>(NotFound())
            : Result.Success(new AttachmentRecord { Attachment = new AttachmentId(id), Name = name, MediaType = type, Size = streamed.Size, Status = AttachmentStatus.Pending });
    }

    /// <summary>
    /// Deletes stored bytes no row will name; where the store fails, a row without a
    /// conversation keeps the key, so the <c>attachment-scans</c> job deletes them at its next pass.
    /// </summary>
    private async Task DeleteStoredAsync(IAttachmentStore store, string key)
    {
        try
        {
            await store.DeleteAsync(key, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            await AttachmentWork.LeaveForTheJobAsync(database, options.CurrentValue, key, clock.GetUtcNow(), CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// An attachment's download, after the access check that found <paramref name="row"/>:
    /// refused while pending, infected or failed; its name and first chunk opened before it
    /// answers, a value that does not open under its binding answering it as not held and
    /// raising <c>content-rejected</c>.
    /// </summary>
    private async Task<Result<AttachmentDownload>> DownloadAsync(AttachmentRow? row, MessagingOptions current, CancellationToken cancellationToken)
    {
        if (row is null)
        {
            return Result.Fail<AttachmentDownload>(Plain(ChatCodes.AttachmentNotFound));
        }

        AttachmentStatus status = ConversationSpellings.AttachmentStatusOf(row.Status);
        if (status == AttachmentStatus.Pending)
        {
            return Result.Fail<AttachmentDownload>(Plain(ChatCodes.AttachmentPending));
        }

        if (status != AttachmentStatus.Clean)
        {
            return Result.Fail<AttachmentDownload>(Plain(ChatCodes.AttachmentRefused));
        }

        ContentOpening opening = await AttachmentOpening.OpenAsync(protection, row.Owner, row.Name!, AttachmentFiles.NameContext(row.Id), current.Dispatch.Timeout, clock, cancellationToken).ConfigureAwait(false);
        if (opening.Status == ContentOpeningStatus.Held)
        {
            return Result.Fail<AttachmentDownload>(HeldFailure(opening.Key));
        }

        if (opening.Status != ContentOpeningStatus.Opened)
        {
            if (opening.Status == ContentOpeningStatus.Rejected)
            {
                await RejectedAsync(ConversationTables.Attachments, cancellationToken).ConfigureAwait(false);
            }

            return Result.Fail<AttachmentDownload>(Plain(ChatCodes.AttachmentNotFound));
        }

        bool heldChunk = false;
        string? heldKey = null;
        IAttachmentStore store = services.GetRequiredService<IAttachmentStore>();
        AttachmentFiles.Content? content = await AttachmentOpening.ContentAsync(
            protection,
            store,
            row,
            current.Dispatch.Timeout,
            clock,
            key =>
            {
                heldChunk = true;
                heldKey = key;
            },
            cancellationToken).ConfigureAwait(false);
        if (content is null)
        {
            return Result.Fail<AttachmentDownload>(Plain(ChatCodes.AttachmentNotFound));
        }

        if (!await content.PrimeAsync(cancellationToken).ConfigureAwait(false))
        {
            await content.DisposeAsync().ConfigureAwait(false);
            if (heldChunk)
            {
                return Result.Fail<AttachmentDownload>(HeldFailure(heldKey));
            }

            await RejectedAsync(ConversationTables.Attachments, cancellationToken).ConfigureAwait(false);
            return Result.Fail<AttachmentDownload>(Plain(ChatCodes.AttachmentNotFound));
        }

        return Result.Success(new AttachmentDownload
        {
            Attachment = Record(row, System.Text.Encoding.UTF8.GetString(opening.Plaintext.Span)),
            Content = content,
        });
    }

    /// <summary>
    /// Locks the attachments <paramref name="named"/> in the writer's transaction, answering them
    /// where each is of the conversation, its writer's, and named by no message yet; none
    /// otherwise.
    /// </summary>
    private static async Task<List<AttachmentRow>?> NamedAsync(MessagingContext context, Guid conversation, Guid[] named, string? person, byte[][] visitorHashes, CancellationToken cancellationToken)
    {
        if (named.Length == 0)
        {
            return [];
        }

        List<AttachmentRow> rows = await context.Attachments.FromSqlRaw(LockAttachments, new NpgsqlParameter("ids", named)).ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.Count == named.Length && rows.All(row => row.ConversationId == conversation && row.NamedAt is null && row.Name is not null && Uploaded(row, person, visitorHashes))
            ? rows
            : null;
    }

    /// <summary>Names the locked rows <paramref name="naming"/> by <paramref name="message"/>, written already in the same transaction.</summary>
    private static async Task NameAsync(MessagingContext context, List<AttachmentRow> naming, Guid message, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (naming.Count == 0)
        {
            return;
        }

        foreach (AttachmentRow row in naming)
        {
            row.MessageId = message;
            row.NamedAt = now;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The attachments a write names, distinct and at most 10, or none where they are outside that rule or attachments are not taken.</summary>
    private static Guid[]? AttachmentsOf(IReadOnlyList<AttachmentId>? attachments, bool open)
    {
        if (attachments is not { Count: > 0 })
        {
            return [];
        }

        Guid[] ids = [.. attachments.Select(each => each.Value).Distinct()];
        return open && ids.Length == attachments.Count && ids.Length <= MostAttachments ? ids : null;
    }

    /// <summary>The attachments the messages <paramref name="messages"/> name, oldest first, each with its name opened; or the failure of a name that cannot open yet.</summary>
    private async Task<(ILookup<Guid, AttachmentRecord> ByMessage, Failure? Held)> AttachmentsOfAsync(IReadOnlyCollection<Guid> messages, MessagingOptions current, CancellationToken cancellationToken)
    {
        List<AttachmentRow> rows = [];
        if (messages.Count > 0)
        {
            Guid[] ids = [.. messages];
            MessagingContext context = database.Context();
            await using (context.ConfigureAwait(false))
            {
                rows = await context.Attachments.AsNoTracking()
                    .Where(each => each.MessageId != null && ids.Contains(each.MessageId.Value) && each.Name != null)
                    .OrderBy(each => each.CreatedAt).ThenBy(each => each.Id)
                    .ToListAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        List<(Guid Message, AttachmentRecord Record)> opened = [];
        bool rejected = false;
        foreach (AttachmentRow row in rows)
        {
            ContentOpening opening = await AttachmentOpening.OpenAsync(protection, row.Owner, row.Name!, AttachmentFiles.NameContext(row.Id), current.Dispatch.Timeout, clock, cancellationToken).ConfigureAwait(false);
            switch (opening.Status)
            {
                case ContentOpeningStatus.Opened:
                    opened.Add((row.MessageId!.Value, Record(row, System.Text.Encoding.UTF8.GetString(opening.Plaintext.Span))));
                    break;
                case ContentOpeningStatus.Held:
                    return (Array.Empty<(Guid, AttachmentRecord)>().ToLookup(each => each.Item1, each => each.Item2), HeldFailure(opening.Key));
                case ContentOpeningStatus.Rejected:
                    rejected = true;
                    break;
                default:
                    break;
            }
        }

        if (rejected)
        {
            await RejectedAsync(ConversationTables.Attachments, cancellationToken).ConfigureAwait(false);
        }

        return (opened.ToLookup(each => each.Message, each => each.Record), null);
    }

    private static AttachmentRecord Record(AttachmentRow row, string name)
    {
        return new AttachmentRecord
        {
            Attachment = new AttachmentId(row.Id),
            Name = name,
            MediaType = row.MediaType,
            Size = row.Size,
            Status = ConversationSpellings.AttachmentStatusOf(row.Status),
        };
    }

    private static Failure HeldFailure(string? key)
    {
        return new Failure
        {
            Code = Codes.KeyUnavailable,
            Details = key is { } held
                ? new Dictionary<string, string>(StringComparer.Ordinal) { ["key"] = held }
                : new Dictionary<string, string>(StringComparer.Ordinal),
        };
    }

    /// <summary>Protects <paramref name="value"/> for <paramref name="person"/>, or as an anonymous recipient's value where none, bound to <paramref name="context"/>.</summary>
    private async Task<Protected> ProtectBytesAsync(string? person, ReadOnlyMemory<byte> value, string context, MessagingOptions current, CancellationToken cancellationToken)
    {
        try
        {
            using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            bound.CancelAfter(current.Dispatch.Timeout);
            ContentProtecting made = await protection.ProtectAsync(person, value, context, bound.Token)
                .AsTask()
                .WaitAsync(current.Dispatch.Timeout, clock, cancellationToken)
                .ConfigureAwait(false);
            return made.Stored is { } stored
                ? new Protected(stored, Sending.Messages.LibraryVersion(stored), null)
                : new Protected(null, null, await UnavailableAsync(made.Key!, made.Version).ConfigureAwait(false));
        }
        catch (Exception failure) when (failure is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new Protected(null, null, await UnavailableAsync(protection.WriterId(), version: null).ConfigureAwait(false));
        }
    }

    /// <summary>Who uploads: a person, or a visitor by their keyed hash, and whose key protects it.</summary>
    private sealed record Uploader(string? Person, byte[]? Visitor, int? HashVersion, string? Owner);
}