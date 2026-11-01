using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Conversations.Live;
using Hermes.Conversations.Support;
using Hermes.Core;
using Hermes.Operations;
using Hermes.Sending;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace Hermes.Conversations.Chats;

/// <summary>
/// The public side of support (<c>09</c> MSG-CONV-001, MSG-CONV-002, MSG-CONV-010,
/// MSG-CONV-011; <c>05</c> MSG-API-010), for a visitor or a signed-in customer: the topics,
/// the notice, starting a conversation or leaving one through the offline form, and reading
/// and writing their own. A conversation that is not the caller's answers as one unknown. A
/// visitor's values are protected as an anonymous recipient's are; a customer's under their key.
/// </summary>
internal sealed partial class StaffChats
{
    private static readonly string Visitor = ConversationSpellings.Of(AuthorKind.Visitor);

    private static readonly string Note = ConversationSpellings.Of(MessageType.Note);

    private const string CountUnder = """
        INSERT INTO messaging.support_counters (id, kind, hash, hash_version, window_start, count)
        VALUES (@id, @kind, @hash, @version, @window, 1)
        ON CONFLICT (kind, hash, window_start) DO UPDATE SET count = messaging.support_counters.count + 1
        WHERE messaging.support_counters.count < @limit
        RETURNING count AS "Value"
        """;

    private const string Counted = """
        SELECT COALESCE(max(count), 0)::integer AS "Value" FROM messaging.support_counters
        WHERE kind = @kind AND hash = @hash AND window_start = @window
        """;

    private const string VisitorMessages = """
        SELECT * FROM messaging.messages
        WHERE conversation_id = @conversation AND type <> 'note' AND (NOT @before OR (sent_at, id) < (@sent_at, @id))
        ORDER BY sent_at DESC, id DESC
        LIMIT @limit
        """;

    private const string VisitorMessagesAfter = """
        SELECT * FROM messaging.messages
        WHERE conversation_id = @conversation AND type <> 'note' AND (sent_at, id) > (@sent_at, @id)
        ORDER BY sent_at, id
        LIMIT @limit
        """;

    private const string CallerUnread = """
        SELECT count(*)::integer AS "Value"
        FROM messaging.messages AS m
        LEFT JOIN messaging.messages AS r ON r.id = @position
        WHERE m.conversation_id = @conversation AND m.deleted_at IS NULL AND m.type <> 'note'
          AND NOT ((@visitor AND m.author_kind = 'visitor') OR (NOT @visitor AND m.author IS NOT DISTINCT FROM @person))
          AND (r.id IS NULL OR (m.sent_at, m.id) > (r.sent_at, r.id))
        """;

    private const string MoveCallerRead = """
        UPDATE messaging.participants AS p SET read_position = @message
        FROM messaging.messages AS m
        WHERE p.id = @participant AND m.id = @message
          AND (p.read_position IS NULL OR (m.sent_at, m.id) > (SELECT r.sent_at, r.id FROM messaging.messages AS r WHERE r.id = p.read_position))
        """;

    private const string VisitorStartsKind = "visitor-starts";

    private const string VisitorMessagesKind = "visitor-messages";

    private const string SourceStartsKind = "source-starts";

    /// <summary>Each topic, and whether it is open now: within its hours while a member is available.</summary>
    internal async Task<IReadOnlyList<(string Name, bool Open)>> TopicsAsync(CancellationToken cancellationToken)
    {
        ready.ThrowIfNotReady();
        List<(string Name, bool Open)> topics = [];
        foreach (SupportTopic topic in settings.Support.Topics.Values.OrderBy(each => each.Name, StringComparer.Ordinal))
        {
            topics.Add((topic.Name, await assigner.IsOpenAsync(topic, cancellationToken).ConfigureAwait(false)));
        }

        return topics;
    }

    /// <summary>Records a visitor's acknowledgement of <paramref name="version"/>; a customer is never asked, and nothing is recorded for one.</summary>
    internal async Task<SupportAnswer<bool>> AcknowledgeAsync(SupportCaller caller, string version, string language, byte[]? source, CancellationToken cancellationToken)
    {
        ready.ThrowIfNotReady();
        MessagingOptions current = options.CurrentValue;
        return await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockCallerAsync(context, current, caller, token).ConfigureAwait(false);
                DateTimeOffset now = clock.GetUtcNow();
                if (await BlockedAsync(context, caller, now, token).ConfigureAwait(false))
                {
                    return SupportAnswer<bool>.Fail(Plain(SupportCodes.Blocked));
                }

                if (await AtStartsLimitAsync(context, caller, source, now, token).ConfigureAwait(false) is { } wait)
                {
                    return SupportAnswer<bool>.Fail(Plain(SupportCodes.Limited), wait);
                }

                if (caller.Visitor is not null)
                {
                    (byte[] hash, int hashVersion) = caller.Counted(hashes);
                    context.SupportAcknowledgements.Add(new SupportAcknowledgementRow
                    {
                        Id = Guid.CreateVersion7(),
                        Visitor = hash,
                        HashVersion = hashVersion,
                        Version = version,
                        AcknowledgedAt = now,
                        Language = language,
                    });
                    await context.SaveChangesAsync(token).ConfigureAwait(false);
                }

                return SupportAnswer<bool>.Of(true);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Starts a support conversation of <paramref name="topic"/> with <paramref name="text"/>, queued, or one through the offline form with <paramref name="email"/>.</summary>
    internal async Task<SupportAnswer<ConversationRecord>> StartSupportAsync(SupportCaller caller, string topic, string text, string? email, byte[]? source, string language, CancellationToken cancellationToken)
    {
        ready.ThrowIfNotReady();
        if (!settings.Support.Topics.TryGetValue(topic, out SupportTopic? configured))
        {
            return SupportAnswer<ConversationRecord>.Fail(Malformed("topic"));
        }

        if (!IsText(text))
        {
            return SupportAnswer<ConversationRecord>.Fail(Invalid(ChatCodes.MessageInvalid, "text"));
        }

        bool offline = email is not null;
        string? address = email is null ? null : Destinations.Email(email);
        if (offline && address is null)
        {
            return SupportAnswer<ConversationRecord>.Fail(Malformed("email"));
        }

        if (await RefusedCallerAsync(caller, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return SupportAnswer<ConversationRecord>.Fail(refused);
        }

        if (await OpenOfAsync(caller, topic, cancellationToken).ConfigureAwait(false) is { } open)
        {
            return SupportAnswer<ConversationRecord>.Fail(new Failure
            {
                Code = SupportCodes.AlreadyOpen,
                Details = new Dictionary<string, string>(StringComparer.Ordinal) { ["conversation"] = open.ToString("D") },
            });
        }

        bool staffed = await assigner.IsOpenAsync(configured, cancellationToken).ConfigureAwait(false);
        if (!offline && !staffed)
        {
            return SupportAnswer<ConversationRecord>.Fail(Plain(SupportCodes.Offline));
        }

        // Every conversation is created queued, so started fires; an offline form's also fires
        // outsidehours outside the topic's hours, or unavailable within them (09 MSG-CONV-003).
        List<string> triggers = [ReplyTriggers.Started];
        if (offline && !configured.IsWithinHours(clock.GetUtcNow()))
        {
            triggers.Add(ReplyTriggers.OutsideHours);
        }
        else if (offline && !staffed)
        {
            triggers.Add(ReplyTriggers.Unavailable);
        }

        Guid conversation = Guid.CreateVersion7();
        Guid message = Guid.CreateVersion7();
        MessagingOptions current = options.CurrentValue;
        Protected made = await ProtectAsync(caller.Customer, text, message, current, cancellationToken).ConfigureAwait(false);
        if (made.Failure is { } textUnavailable)
        {
            return SupportAnswer<ConversationRecord>.Fail(textUnavailable);
        }

        Protected? madeEmail = null;
        if (address is not null)
        {
            madeEmail = await ProtectValueAsync(caller.Customer, address, ContentContext.Of(ContentContext.Conversations, conversation, ConversationTables.Email), current, cancellationToken).ConfigureAwait(false);
            if (madeEmail.Failure is { } emailUnavailable)
            {
                return SupportAnswer<ConversationRecord>.Fail(emailUnavailable);
            }
        }

        SupportAnswer<bool> written = await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockCallerAsync(context, current, caller, token).ConfigureAwait(false);
                DateTimeOffset now = clock.GetUtcNow();
                if (await CountStartAsync(context, caller, source, now, token).ConfigureAwait(false) is { } wait)
                {
                    return SupportAnswer<bool>.Fail(Plain(SupportCodes.Limited), wait);
                }

                ConversationRow created = new()
                {
                    Id = conversation,
                    Type = SupportType,
                    Origin = ConversationSpellings.Web,
                    Status = OpenStatus,
                    CreatedAt = now,
                    LastMessageAt = now,
                    Topic = topic,
                    QueuedAt = now,
                    Offline = offline,
                    Customer = caller.Customer,
                    Email = madeEmail?.Stored,
                    EmailKeyVersion = madeEmail?.Version,
                    Language = language,
                };
                Silence(created, now);
                created.WaitingAt = WaitingOf(created);
                context.Conversations.Add(created);
                (byte[] hash, int hashVersion) = caller.Visitor is null ? (null!, 0) : caller.Counted(hashes);
                context.Participants.Add(new ParticipantRow
                {
                    Id = Guid.CreateVersion7(),
                    ConversationId = conversation,
                    Person = caller.Customer,
                    Visitor = caller.Visitor is null ? null : hash,
                    HashVersion = caller.Visitor is null ? null : hashVersion,
                    JoinedAt = now,
                });
                context.Messages.Add(CallerMessage(caller, conversation, message, made, now));
                await context.SaveChangesAsync(token).ConfigureAwait(false);
                await LiveNotices.TellQueueAsync(context, topic, token).ConfigureAwait(false);
                return SupportAnswer<bool>.Of(true);
            },
            cancellationToken).ConfigureAwait(false);
        if (written.Failure is { } limited)
        {
            return SupportAnswer<ConversationRecord>.Fail(limited, written.RetryAfter);
        }

        metrics.ConversationsStarted.Add(1, new KeyValuePair<string, object?>("topic", topic));
        await assigner.AssignAsync([topic], cancellationToken).ConfigureAwait(false);
        await FireAsync(conversation, triggers, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return SupportAnswer<ConversationRecord>.Of((await CallerRecordAsync(caller, conversation, cancellationToken).ConfigureAwait(false))!);
    }

    /// <summary>The caller's newest open conversation, with its place; none where none is open.</summary>
    internal async Task<ConversationRecord?> CurrentAsync(SupportCaller caller, CancellationToken cancellationToken)
    {
        ready.ThrowIfNotReady();
        Guid? newest;
        MessagingContext context = database.Context();
        await using (context.ConfigureAwait(false))
        {
            newest = await CallerConversations(context, caller)
                .Where(each => each.Status != ClosedStatus)
                .OrderByDescending(each => each.CreatedAt).ThenByDescending(each => each.Id)
                .Select(each => (Guid?)each.Id)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        }

        return newest is { } found ? await CallerRecordAsync(caller, found, cancellationToken).ConfigureAwait(false) : null;
    }

    /// <summary>A page of the caller's conversation's messages, never a <c>note</c>, as the staff side's are read.</summary>
    internal async Task<Result<MessagePage>> CallerMessagesAsync(SupportCaller caller, Guid conversation, MessageId? before, MessageId? after, int? size, CancellationToken cancellationToken)
    {
        ready.ThrowIfNotReady();
        int limit = size ?? DefaultMessages;
        if (before is not null && after is not null)
        {
            return Result.Fail<MessagePage>(Malformed("after"));
        }

        MessagingOptions current = options.CurrentValue;
        Read read = await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockCallerAsync(context, current, caller, token).ConfigureAwait(false);
                ConversationRow? reached = await CallerConversations(context, caller).AsNoTracking().FirstOrDefaultAsync(each => each.Id == conversation, token).ConfigureAwait(false);
                if (reached is null)
                {
                    return new Read(null, [], [], null);
                }

                MessageRow? end = null;
                if ((before ?? after) is { } named)
                {
                    end = await context.Messages.AsNoTracking().FirstOrDefaultAsync(row => row.Id == named.Value && row.ConversationId == reached.Id && row.Type != Note, token).ConfigureAwait(false);
                    if (end is null)
                    {
                        return new Read(reached, [], [], before is null ? "after" : "before");
                    }
                }

                List<MessageRow> rows = await context.Messages.FromSqlRaw(
                    after is null ? VisitorMessages : VisitorMessagesAfter,
                    new NpgsqlParameter("conversation", reached.Id),
                    new NpgsqlParameter("before", end is not null),
                    new NpgsqlParameter("sent_at", end?.SentAt ?? DateTimeOffset.UnixEpoch),
                    new NpgsqlParameter("id", end?.Id ?? Guid.Empty),
                    new NpgsqlParameter("limit", limit)).AsNoTracking().ToListAsync(token).ConfigureAwait(false);
                return new Read(reached, rows, [], null);
            },
            cancellationToken).ConfigureAwait(false);
        if (read.Malformed is { } member)
        {
            return Result.Fail<MessagePage>(Malformed(member));
        }

        if (read.Conversation is null)
        {
            return Result.Fail<MessagePage>(NotFound());
        }

        List<MessageRecord> messages = [];
        bool rejected = false;
        foreach (MessageRow row in read.Rows)
        {
            Opened opened = await OpenAsync(row, current, cancellationToken).ConfigureAwait(false);
            if (opened.Held is { } held)
            {
                return Result.Fail<MessagePage>(held);
            }

            rejected |= opened.Rejected;
            messages.Add(Of(row, opened.Text, opened.Erased, []));
        }

        if (rejected)
        {
            await RejectedAsync(cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(new MessagePage { Messages = messages });
    }

    /// <summary>Writes the caller's message in their open conversation, counted against the messages limit.</summary>
    internal async Task<SupportAnswer<MessageRecord>> CallerWriteAsync(SupportCaller caller, Guid conversation, string text, CancellationToken cancellationToken)
    {
        ready.ThrowIfNotReady();
        ConversationRow? reached = await CallerConversationAsync(caller, conversation, cancellationToken).ConfigureAwait(false);
        if (reached is null || string.Equals(reached.Status, ClosedStatus, StringComparison.Ordinal))
        {
            return SupportAnswer<MessageRecord>.Fail(NotFound());
        }

        if (!IsText(text))
        {
            return SupportAnswer<MessageRecord>.Fail(Invalid(ChatCodes.MessageInvalid, "text"));
        }

        if (await RefusedCallerAsync(caller, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return SupportAnswer<MessageRecord>.Fail(refused);
        }

        Guid message = Guid.CreateVersion7();
        MessagingOptions current = options.CurrentValue;
        Protected made = await ProtectAsync(caller.Customer, text, message, current, cancellationToken).ConfigureAwait(false);
        if (made.Failure is { } unavailable)
        {
            return SupportAnswer<MessageRecord>.Fail(unavailable);
        }

        SupportAnswer<MessageRow> written = await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockCallerAsync(context, current, caller, token).ConfigureAwait(false);
                DateTimeOffset now = clock.GetUtcNow();
                (byte[] hash, int hashVersion) = caller.Counted(hashes);
                if (await CountAsync(context, VisitorMessagesKind, hash, hashVersion, now, TimeSpan.FromMinutes(1), settings.Support.VisitorMessages, token).ConfigureAwait(false) is { } wait)
                {
                    return SupportAnswer<MessageRow>.Fail(Plain(SupportCodes.Limited), wait);
                }

                MessageRow row = CallerMessage(caller, reached.Id, message, made, now);
                context.Messages.Add(row);
                ConversationRow tracked = await context.Conversations.SingleAsync(each => each.Id == reached.Id, token).ConfigureAwait(false);
                tracked.LastMessageAt = now;
                Silence(tracked, now);

                // The visitor's or customer's message wakes a snoozed conversation at once (09 MSG-CONV-005).
                if (string.Equals(tracked.Status, SnoozedStatus, StringComparison.Ordinal))
                {
                    tracked.Status = OpenStatus;
                    tracked.SnoozedUntil = null;
                }
                await context.SaveChangesAsync(token).ConfigureAwait(false);
                await LiveNotices.TellAsync(context, LiveNotices.Message, reached.Id, message, token).ConfigureAwait(false);
                return SupportAnswer<MessageRow>.Of(row);
            },
            cancellationToken).ConfigureAwait(false);
        return written.Failure is { } failed
            ? SupportAnswer<MessageRecord>.Fail(failed, written.RetryAfter)
            : SupportAnswer<MessageRecord>.Of(Of(written.Value!, text, erased: false, []));
    }

    /// <summary>Moves the caller's read position forward to <paramref name="message"/>, which is not a <c>note</c>.</summary>
    internal async Task<Result> CallerMarkReadAsync(SupportCaller caller, Guid conversation, Guid message, CancellationToken cancellationToken)
    {
        ready.ThrowIfNotReady();
        MessagingOptions current = options.CurrentValue;
        Failure? failure = await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockCallerAsync(context, current, caller, token).ConfigureAwait(false);
                ConversationRow? reached = await CallerConversations(context, caller).AsNoTracking().FirstOrDefaultAsync(each => each.Id == conversation, token).ConfigureAwait(false);
                if (reached is null || await CallerParticipantAsync(context, caller, reached.Id, token).ConfigureAwait(false) is not { } participant)
                {
                    return NotFound();
                }

                if (!await context.Messages.AnyAsync(row => row.Id == message && row.ConversationId == reached.Id && row.Type != Note, token).ConfigureAwait(false))
                {
                    return Plain(ChatCodes.MessageNotFound);
                }

                int moved = await context.Database.ExecuteSqlRawAsync(
                    MoveCallerRead,
                    [new NpgsqlParameter("message", message), new NpgsqlParameter("participant", participant.Id)],
                    token).ConfigureAwait(false);
                if (moved > 0)
                {
                    await LiveNotices.TellAsync(context, LiveNotices.Moved, reached.Id, participant.Id, token).ConfigureAwait(false);
                }

                return null;
            },
            cancellationToken).ConfigureAwait(false);
        return failure is null ? Result.Success() : Result.Fail(failure);
    }

    /// <summary>Closes the caller's conversation; one already closed changes nothing.</summary>
    internal async Task<Result> CallerCloseAsync(SupportCaller caller, Guid conversation, CancellationToken cancellationToken)
    {
        ready.ThrowIfNotReady();
        if (await CallerConversationAsync(caller, conversation, cancellationToken).ConfigureAwait(false) is not { } reached)
        {
            return Result.Fail(NotFound());
        }

        if (await CloseSupportAsync(reached.Id, by: null, cancellationToken).ConfigureAwait(false))
        {
            await assigner.AssignAsync([reached.Topic!], cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }

    /// <summary>Tells the processes that the caller is typing in their open conversation; nothing of it is stored.</summary>
    internal async Task CallerTypingAsync(SupportCaller caller, Guid conversation, CancellationToken cancellationToken)
    {
        if (!ready.IsReady)
        {
            return;
        }

        MessagingOptions current = options.CurrentValue;
        await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockCallerAsync(context, current, caller, token).ConfigureAwait(false);
                ConversationRow? reached = await CallerConversations(context, caller).AsNoTracking().FirstOrDefaultAsync(each => each.Id == conversation && each.Status != ClosedStatus, token).ConfigureAwait(false);
                if (reached is not null && await CallerParticipantAsync(context, caller, reached.Id, token).ConfigureAwait(false) is { } participant)
                {
                    await LiveNotices.TellAsync(context, LiveNotices.Typing, reached.Id, participant.Id, token).ConfigureAwait(false);
                }

                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The caller's conversation <paramref name="conversation"/>, as a read answers it for them; none where it is not theirs.</summary>
    internal async Task<ConversationRecord?> CallerRecordAsync(SupportCaller caller, Guid conversation, CancellationToken cancellationToken)
    {
        MessagingContext context = database.Context();
        await using (context.ConfigureAwait(false))
        {
            ConversationRow? row = await CallerConversations(context, caller).AsNoTracking().FirstOrDefaultAsync(each => each.Id == conversation, cancellationToken).ConfigureAwait(false);
            if (row is null)
            {
                return null;
            }

            ParticipantRow? participant = await CallerParticipantAsync(context, caller, row.Id, cancellationToken).ConfigureAwait(false);
            int unread = await context.Database.SqlQueryRaw<int>(
                CallerUnread,
                new NpgsqlParameter("conversation", row.Id),
                new NpgsqlParameter("position", NpgsqlTypes.NpgsqlDbType.Uuid) { Value = participant?.ReadPosition is { } position ? position : DBNull.Value },
                new NpgsqlParameter("visitor", caller.Visitor is not null),
                new NpgsqlParameter("person", NpgsqlTypes.NpgsqlDbType.Text) { Value = caller.Customer is { } customer ? customer : DBNull.Value }).SingleAsync(cancellationToken).ConfigureAwait(false);
            return new ConversationRecord
            {
                Conversation = new ConversationId(row.Id),
                Type = ConversationType.Support,
                Participants = [],
                Unread = unread,
                Muted = participant?.Muted ?? false,
                LastAt = row.LastMessageAt ?? row.CreatedAt,
                Topic = row.Topic,
                Status = ConversationSpellings.StatusOf(row.Status),
                Place = await PlaceAsync(context, row, cancellationToken).ConfigureAwait(false),
                Offline = row.Offline,
            };
        }
    }

    /// <summary>Whether a support conversation is the one of the visitor whose hashes are <paramref name="visitorHashes"/>, or of <paramref name="customer"/>.</summary>
    internal static async Task<bool> IsCallersAsync(MessagingContext context, Guid conversation, string? customer, byte[][] visitorHashes, CancellationToken cancellationToken)
    {
        return customer is not null
            ? await context.Conversations.AnyAsync(each => each.Id == conversation && each.Customer == customer, cancellationToken).ConfigureAwait(false)
            : await context.Participants.AnyAsync(each => each.ConversationId == conversation && each.Visitor != null && visitorHashes.Contains(each.Visitor), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The caller's support conversations.</summary>
    private IQueryable<ConversationRow> CallerConversations(MessagingContext context, SupportCaller caller)
    {
        if (caller.Customer is { } customer)
        {
            return context.Conversations.Where(each => each.Type == SupportType && each.Customer == customer);
        }

        byte[][] every = caller.Hashes(hashes);
        return context.Conversations.Where(each => each.Type == SupportType && each.Customer == null
            && context.Participants.Any(participant => participant.ConversationId == each.Id && participant.Visitor != null && every.Contains(participant.Visitor)));
    }

    private async Task<ConversationRow?> CallerConversationAsync(SupportCaller caller, Guid conversation, CancellationToken cancellationToken)
    {
        MessagingContext context = database.Context();
        await using (context.ConfigureAwait(false))
        {
            return await CallerConversations(context, caller).AsNoTracking().FirstOrDefaultAsync(each => each.Id == conversation, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<ParticipantRow?> CallerParticipantAsync(MessagingContext context, SupportCaller caller, Guid conversation, CancellationToken cancellationToken)
    {
        if (caller.Customer is { } customer)
        {
            return await context.Participants.AsNoTracking().FirstOrDefaultAsync(each => each.ConversationId == conversation && each.Person == customer, cancellationToken).ConfigureAwait(false);
        }

        byte[][] every = caller.Hashes(hashes);
        return await context.Participants.AsNoTracking().FirstOrDefaultAsync(each => each.ConversationId == conversation && each.Visitor != null && every.Contains(each.Visitor), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The caller's open conversation of <paramref name="topic"/>, or none.</summary>
    private async Task<Guid?> OpenOfAsync(SupportCaller caller, string topic, CancellationToken cancellationToken)
    {
        MessagingContext context = database.Context();
        await using (context.ConfigureAwait(false))
        {
            return await CallerConversations(context, caller)
                .Where(each => each.Topic == topic && each.Status != ClosedStatus)
                .Select(each => (Guid?)each.Id)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>The refusal of a blocked caller, or of a visitor who has not acknowledged the current notice; none where they may write.</summary>
    private async Task<Failure?> RefusedCallerAsync(SupportCaller caller, CancellationToken cancellationToken)
    {
        MessagingContext context = database.Context();
        await using (context.ConfigureAwait(false))
        {
            if (await BlockedAsync(context, caller, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false))
            {
                return Plain(SupportCodes.Blocked);
            }

            if (caller.Visitor is not null && settings.Support.NoticeVersion is { } version)
            {
                byte[][] every = caller.Hashes(hashes);
                if (!await context.SupportAcknowledgements.AnyAsync(each => each.Version == version && every.Contains(each.Visitor), cancellationToken).ConfigureAwait(false))
                {
                    return Plain(SupportCodes.NoticeRequired);
                }
            }

            return null;
        }
    }

    private async Task<bool> BlockedAsync(MessagingContext context, SupportCaller caller, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (caller.Customer is { } customer)
        {
            return await context.SupportBlocks.AnyAsync(each => each.Person == customer && (each.Until == null || each.Until > now), cancellationToken).ConfigureAwait(false);
        }

        byte[][] every = caller.Hashes(hashes);
        return await context.SupportBlocks.AnyAsync(each => each.Visitor != null && every.Contains(each.Visitor) && (each.Until == null || each.Until > now), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Counts a start against the caller's and the source's limits, answering the wait where either is at its limit; both or neither count.</summary>
    private async Task<TimeSpan?> CountStartAsync(MessagingContext context, SupportCaller caller, byte[]? source, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (await AtStartsLimitAsync(context, caller, source, now, cancellationToken).ConfigureAwait(false) is { } wait)
        {
            return wait;
        }

        (byte[] hash, int hashVersion) = caller.Counted(hashes);
        if (await CountAsync(context, VisitorStartsKind, hash, hashVersion, now, TimeSpan.FromDays(1), settings.Support.VisitorStarts, cancellationToken).ConfigureAwait(false) is { } visitorWait)
        {
            return visitorWait;
        }

        return source is null
            ? null
            : await CountAsync(context, SourceStartsKind, hashes.Write(KeyedHashes.Source, source), hashes.WriteVersion, now, TimeSpan.FromHours(1), settings.Support.SourceStarts, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The wait where the caller or the source is already at its starts limit, counting nothing; none otherwise.</summary>
    private async Task<TimeSpan?> AtStartsLimitAsync(MessagingContext context, SupportCaller caller, byte[]? source, DateTimeOffset now, CancellationToken cancellationToken)
    {
        (byte[] hash, int _) = caller.Counted(hashes);
        if (await PeekAsync(context, VisitorStartsKind, hash, now, TimeSpan.FromDays(1), settings.Support.VisitorStarts, cancellationToken).ConfigureAwait(false) is { } wait)
        {
            return wait;
        }

        return source is null
            ? null
            : await PeekAsync(context, SourceStartsKind, hashes.Write(KeyedHashes.Source, source), now, TimeSpan.FromHours(1), settings.Support.SourceStarts, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Counts one against <paramref name="kind"/>'s counter of <paramref name="hash"/> in the window holding <paramref name="now"/>, under its row's lock, answering the wait to the window's end where it is at <paramref name="limit"/> already.</summary>
    private static async Task<TimeSpan?> CountAsync(MessagingContext context, string kind, byte[] hash, int hashVersion, DateTimeOffset now, TimeSpan window, int limit, CancellationToken cancellationToken)
    {
        DateTimeOffset start = WindowOf(now, window);
        List<int> counted = await context.Database.SqlQueryRaw<int>(
            CountUnder,
            new NpgsqlParameter("id", Guid.CreateVersion7()),
            new NpgsqlParameter("kind", kind),
            new NpgsqlParameter("hash", hash),
            new NpgsqlParameter("version", hashVersion),
            new NpgsqlParameter("window", start),
            new NpgsqlParameter("limit", limit)).ToListAsync(cancellationToken).ConfigureAwait(false);
        return counted.Count == 0 ? start + window - now : null;
    }

    private static async Task<TimeSpan?> PeekAsync(MessagingContext context, string kind, byte[] hash, DateTimeOffset now, TimeSpan window, int limit, CancellationToken cancellationToken)
    {
        DateTimeOffset start = WindowOf(now, window);
        int count = (await context.Database.SqlQueryRaw<int>(Counted, new NpgsqlParameter("kind", kind), new NpgsqlParameter("hash", hash), new NpgsqlParameter("window", start)).ToListAsync(cancellationToken).ConfigureAwait(false)).Single();
        return count >= limit ? start + window - now : null;
    }

    private static DateTimeOffset WindowOf(DateTimeOffset now, TimeSpan window)
    {
        long ticks = now.UtcTicks;
        return new DateTimeOffset(ticks - (ticks % window.Ticks), TimeSpan.Zero);
    }

    /// <summary>Takes a customer's locks shared, bounded by <c>Hermes:Dispatch:Timeout</c>; a visitor holds none.</summary>
    private async Task LockCallerAsync(MessagingContext context, MessagingOptions current, SupportCaller caller, CancellationToken cancellationToken)
    {
        if (caller.Customer is { } customer)
        {
            await LockAsync(context, current, [customer], cancellationToken).ConfigureAwait(false);
        }
    }

    private static MessageRow CallerMessage(SupportCaller caller, Guid conversation, Guid message, Protected made, DateTimeOffset now)
    {
        return new MessageRow
        {
            Id = message,
            ConversationId = conversation,
            AuthorKind = caller.Customer is null ? Visitor : Person,
            Author = caller.Customer,
            Owner = caller.Customer,
            Type = ConversationSpellings.Of(MessageType.Text),
            Text = made.Stored,
            ContentKeyVersion = made.Version,
            SentAt = now,
        };
    }

    /// <summary>Protects <paramref name="value"/> for <paramref name="person"/>, or as an anonymous recipient's value where none, bound to <paramref name="context"/>.</summary>
    private async Task<Protected> ProtectValueAsync(string? person, string value, string context, MessagingOptions current, CancellationToken cancellationToken)
    {
        try
        {
            using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            bound.CancelAfter(current.Dispatch.Timeout);
            ContentProtecting made = await protection.ProtectAsync(person, Encoding.UTF8.GetBytes(value), context, bound.Token)
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
}