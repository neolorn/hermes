using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Conversations.Live;
using Hermes.Core;
using Hermes.Operations;
using Hermes.Sending;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Hermes.Conversations.Chats;

/// <summary>
/// The staff side's direct and group conversations (<c>09</c> MSG-CONV-001, MSG-CONV-006,
/// MSG-CONV-009). A conversation is reached before any rule of the request is read, so a
/// person who may not read it learns nothing of it: a group conversation by asking the
/// membership seam on every call, a direct one by the person's participant row. Every call
/// runs in one transaction under the locks, taken shared, of the person and of each person
/// it writes a row for, each wait bounded by <c>Hermes:Dispatch:Timeout</c>; a text is
/// protected under its author's key before the transaction, and opened after it. A scoped
/// service, resolving the content protector from the scope it was called in.
/// </summary>
internal sealed partial class StaffChats(
    MessagingReady ready,
    IOptionsMonitor<MessagingOptions> options,
    ContentProtection protection,
    KeyedHashes hashes,
    MessagingDatabase database,
    Alerts alerts,
    IConversationMembership membership,
    IMessages messages,
    ConversationsSettings settings,
    TimeProvider clock,
    ILogger<StaffChats> logger) : IConversations
{
    /// <summary>The most characters of a text, counted in UTF-16 code units.</summary>
    internal const int MostTextCharacters = 4_000;

    /// <summary>The most distinct persons a message mentions.</summary>
    internal const int MostMentions = 32;

    /// <summary>The most participants of a direct conversation.</summary>
    internal const int MostParticipants = 16;

    private const int MostReferenceCharacters = 128;

    private const int DefaultConversations = 20;

    private const int DefaultMessages = 50;

    private const int MostSize = 100;

    private static readonly string Direct = ConversationSpellings.Of(ConversationType.Direct);

    private static readonly string Group = ConversationSpellings.Of(ConversationType.Group);

    private static readonly string Person = ConversationSpellings.Of(AuthorKind.Person);

    private const string Page = """
        SELECT * FROM messaging.conversations AS c
        WHERE ((c.type = 'direct' AND EXISTS (SELECT 1 FROM messaging.participants AS p WHERE p.conversation_id = c.id AND p.person = @person AND p.left_at IS NULL))
            OR (c.type = 'group' AND (c.scope_kind, c.scope_reference) IN (SELECT kind, reference FROM unnest(@kinds, @references) AS scopes(kind, reference))))
          AND (NOT @after OR (COALESCE(c.last_message_at, c.created_at), c.id) < (@last_at, @id))
        ORDER BY COALESCE(c.last_message_at, c.created_at) DESC, c.id DESC
        LIMIT @limit
        """;

    private const string Unread = """
        SELECT m.conversation_id AS "Conversation", count(*)::integer AS "Count"
        FROM messaging.messages AS m
        LEFT JOIN messaging.participants AS p ON p.conversation_id = m.conversation_id AND p.person = @person
        LEFT JOIN messaging.messages AS r ON r.id = p.read_position
        WHERE m.conversation_id = ANY(@ids) AND m.deleted_at IS NULL AND m.author IS DISTINCT FROM @person
          AND (r.id IS NULL OR (m.sent_at, m.id) > (r.sent_at, r.id))
        GROUP BY m.conversation_id
        """;

    private const string Messages = """
        SELECT * FROM messaging.messages
        WHERE conversation_id = @conversation AND (NOT @before OR (sent_at, id) < (@sent_at, @id))
        ORDER BY sent_at DESC, id DESC
        LIMIT @limit
        """;

    private const string MessagesAfter = """
        SELECT * FROM messaging.messages
        WHERE conversation_id = @conversation AND (sent_at, id) > (@sent_at, @id)
        ORDER BY sent_at, id
        LIMIT @limit
        """;

    private const string OpenScope = """
        INSERT INTO messaging.conversations (id, type, origin, status, scope_kind, scope_reference, created_at)
        VALUES (@id, 'group', 'web', 'open', @kind, @reference, @now)
        ON CONFLICT (scope_kind, scope_reference) DO NOTHING
        """;

    private const string Join = """
        INSERT INTO messaging.participants (id, conversation_id, person, muted, joined_at)
        VALUES (@id, @conversation, @person, false, @now)
        ON CONFLICT (conversation_id, person) DO NOTHING
        """;

    private const string LockMessage = "SELECT * FROM messaging.messages WHERE id = @id AND conversation_id = @conversation FOR UPDATE";

    private const string MoveRead = """
        UPDATE messaging.participants AS p SET read_position = @message
        FROM messaging.messages AS m
        WHERE p.conversation_id = @conversation AND p.person = @person AND m.id = @message
          AND (p.read_position IS NULL OR (m.sent_at, m.id) > (SELECT r.sent_at, r.id FROM messaging.messages AS r WHERE r.id = p.read_position))
        """;

    public async ValueTask<Result<ConversationPage>> ListAsync(string person, string? cursor, int? size, CancellationToken cancellationToken)
    {
        Begin(person);
        int limit = size ?? DefaultConversations;
        if (limit is < 1 or > MostSize)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "A page holds 1 to 100 conversations.");
        }

        (DateTimeOffset LastAt, Guid Conversation)? after = null;
        if (cursor is not null)
        {
            if (InAppCursor.Read(cursor) is not { } read)
            {
                return Result.Fail<ConversationPage>(Malformed("cursor"));
            }

            after = read;
        }

        List<ConversationScope> scopes = [];
        await foreach (ConversationScope scope in membership.ScopesAsync(person, cancellationToken).ConfigureAwait(false))
        {
            scopes.Add(scope);
        }

        MessagingOptions current = options.CurrentValue;
        (List<ConversationRow> Rows, List<ConversationRecord> Records) page = await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockAsync(context, current, [person], token).ConfigureAwait(false);
                List<ConversationRow> rows = await context.Conversations.FromSqlRaw(
                    Page,
                    new NpgsqlParameter("person", person),
                    new NpgsqlParameter("kinds", scopes.Select(scope => ConversationSpellings.Of(scope.Kind)).ToArray()),
                    new NpgsqlParameter("references", scopes.Select(scope => scope.Reference).ToArray()),
                    new NpgsqlParameter("after", after is not null),
                    new NpgsqlParameter("last_at", after?.LastAt ?? DateTimeOffset.UnixEpoch),
                    new NpgsqlParameter("id", after?.Conversation ?? Guid.Empty),
                    new NpgsqlParameter("limit", limit + 1)).AsNoTracking().ToListAsync(token).ConfigureAwait(false);
                List<ConversationRow> shown = [.. rows.Take(limit)];
                return (rows, await RecordsAsync(context, person, clock.GetUtcNow(), shown, token).ConfigureAwait(false));
            },
            cancellationToken).ConfigureAwait(false);
        ConversationRow? last = page.Rows.Count > limit ? page.Rows[limit - 1] : null;
        return Result.Success(new ConversationPage
        {
            Conversations = page.Records,
            Next = last is null ? null : InAppCursor.Write(last.LastMessageAt ?? last.CreatedAt, last.Id),
        });
    }

    public async ValueTask<Result<ConversationRecord>> StartDirectAsync(string person, IReadOnlyList<string> participants, CancellationToken cancellationToken)
    {
        Begin(person);
        ArgumentNullException.ThrowIfNull(participants);
        HashSet<string> distinct = new(StringComparer.Ordinal);
        if (participants.Count is < 1 or > MostParticipants - 1
            || participants.Any(other => !IsReference(other) || string.Equals(other, person, StringComparison.Ordinal) || !distinct.Add(other)))
        {
            return Result.Fail<ConversationRecord>(Invalid(ChatCodes.ConversationInvalid, "participants"));
        }

        MessagingOptions current = options.CurrentValue;
        ConversationRecord record = await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockAsync(context, current, [person, .. participants], token).ConfigureAwait(false);
                DateTimeOffset now = clock.GetUtcNow();
                ConversationRow row = new()
                {
                    Id = Guid.CreateVersion7(),
                    Type = Direct,
                    Origin = ConversationSpellings.Web,
                    Status = ConversationSpellings.Of(ConversationStatus.Open),
                    CreatedAt = now,
                };
                context.Conversations.Add(row);
                foreach (string member in (string[])[person, .. participants])
                {
                    context.Participants.Add(new ParticipantRow { Id = Guid.CreateVersion7(), ConversationId = row.Id, Person = member, JoinedAt = now });
                }

                await context.SaveChangesAsync(token).ConfigureAwait(false);
                return (await RecordsAsync(context, person, clock.GetUtcNow(), [row], token).ConfigureAwait(false))[0];
            },
            cancellationToken).ConfigureAwait(false);
        return Result.Success(record);
    }

    public async ValueTask<Result<ConversationRecord>> OpenGroupAsync(string person, ConversationScope scope, CancellationToken cancellationToken)
    {
        Begin(person);
        ArgumentNullException.ThrowIfNull(scope);
        if (!IsReference(scope.Reference))
        {
            return Result.Fail<ConversationRecord>(Malformed("reference"));
        }

        if (!await membership.IsMemberAsync(person, scope, cancellationToken).ConfigureAwait(false))
        {
            return Result.Fail<ConversationRecord>(NotFound());
        }

        MessagingOptions current = options.CurrentValue;
        string kind = ConversationSpellings.Of(scope.Kind);
        ConversationRecord record = await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockAsync(context, current, [person], token).ConfigureAwait(false);
                DateTimeOffset now = clock.GetUtcNow();
                await context.Database.ExecuteSqlRawAsync(
                    OpenScope,
                    [
                        new NpgsqlParameter("id", Guid.CreateVersion7()),
                        new NpgsqlParameter("kind", kind),
                        new NpgsqlParameter("reference", scope.Reference),
                        new NpgsqlParameter("now", now),
                    ],
                    token).ConfigureAwait(false);
                ConversationRow row = await context.Conversations.AsNoTracking()
                    .SingleAsync(each => each.ScopeKind == kind && each.ScopeReference == scope.Reference, token).ConfigureAwait(false);
                await JoinAsync(context, row.Id, person, now, token).ConfigureAwait(false);
                return (await RecordsAsync(context, person, clock.GetUtcNow(), [row], token).ConfigureAwait(false))[0];
            },
            cancellationToken).ConfigureAwait(false);
        return Result.Success(record);
    }

    public async ValueTask<Result<ConversationRecord>> ReadAsync(string person, ConversationId conversation, CancellationToken cancellationToken)
    {
        Begin(person);
        if (await ReachAsync(person, conversation.Value, cancellationToken).ConfigureAwait(false) is not { } reached)
        {
            return Result.Fail<ConversationRecord>(NotFound());
        }

        MessagingOptions current = options.CurrentValue;
        ConversationRecord? record = await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockAsync(context, current, [person], token).ConfigureAwait(false);
                if (!await StaysAsync(context, reached, person, token).ConfigureAwait(false))
                {
                    return null;
                }

                return (await RecordsAsync(context, person, clock.GetUtcNow(), [reached], token).ConfigureAwait(false))[0];
            },
            cancellationToken).ConfigureAwait(false);
        return record is null ? Result.Fail<ConversationRecord>(NotFound()) : Result.Success(record);
    }

    public async ValueTask<Result<MessagePage>> MessagesAsync(string person, ConversationId conversation, MessageId? before, MessageId? after, int? size, CancellationToken cancellationToken)
    {
        Begin(person);
        int limit = size ?? DefaultMessages;
        if (limit is < 1 or > MostSize)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "A page holds 1 to 100 messages.");
        }

        if (await ReachAsync(person, conversation.Value, cancellationToken).ConfigureAwait(false) is not { } reached)
        {
            return Result.Fail<MessagePage>(NotFound());
        }

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
                await LockAsync(context, current, [person], token).ConfigureAwait(false);
                if (!await StaysAsync(context, reached, person, token).ConfigureAwait(false))
                {
                    return new Read(null, [], [], null);
                }

                MessageRow? end = null;
                if ((before ?? after) is { } named)
                {
                    end = await context.Messages.AsNoTracking().FirstOrDefaultAsync(row => row.Id == named.Value && row.ConversationId == reached.Id, token).ConfigureAwait(false);
                    if (end is null)
                    {
                        return new Read(reached, [], [], before is null ? "after" : "before");
                    }
                }

                List<MessageRow> rows = await context.Messages.FromSqlRaw(
                    after is null ? Messages : MessagesAfter,
                    new NpgsqlParameter("conversation", reached.Id),
                    new NpgsqlParameter("before", end is not null),
                    new NpgsqlParameter("sent_at", end?.SentAt ?? DateTimeOffset.UnixEpoch),
                    new NpgsqlParameter("id", end?.Id ?? Guid.Empty),
                    new NpgsqlParameter("limit", limit)).AsNoTracking().ToListAsync(token).ConfigureAwait(false);
                Guid[] ids = [.. rows.Select(row => row.Id)];
                List<MentionRow> mentions = await context.Mentions.AsNoTracking().Where(mention => ids.Contains(mention.MessageId)).ToListAsync(token).ConfigureAwait(false);
                return new Read(reached, rows, mentions, null);
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
            messages.Add(Of(row, opened.Text, opened.Erased, [.. read.Mentions.Where(mention => mention.MessageId == row.Id).Select(mention => mention.Person).Order(StringComparer.Ordinal)]));
        }

        if (rejected)
        {
            await RejectedAsync(cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(new MessagePage { Messages = messages });
    }

    public async ValueTask<Result<MessageRecord>> WriteAsync(string person, ConversationId conversation, string text, MessageType type, MessageId? replyTo, IReadOnlyList<string>? mentions, CancellationToken cancellationToken)
    {
        Begin(person);
        ArgumentNullException.ThrowIfNull(text);
        if (await ReachAsync(person, conversation.Value, cancellationToken).ConfigureAwait(false) is not { } reached)
        {
            return Result.Fail<MessageRecord>(NotFound());
        }

        if (!IsText(text))
        {
            return Result.Fail<MessageRecord>(Invalid(ChatCodes.MessageInvalid, "text"));
        }

        if (type != MessageType.Text)
        {
            return Result.Fail<MessageRecord>(Invalid(ChatCodes.MessageInvalid, "type"));
        }

        string[] mentioned = [.. (mentions ?? []).Distinct(StringComparer.Ordinal)];
        if (mentioned.Length > MostMentions || !mentioned.All(IsReference))
        {
            return Result.Fail<MessageRecord>(Invalid(ChatCodes.MessageInvalid, "mentions"));
        }

        ConversationScope? scope = ScopeOf(reached);
        if (scope is not null)
        {
            foreach (string other in mentioned)
            {
                if (!await membership.IsMemberAsync(other, scope, cancellationToken).ConfigureAwait(false))
                {
                    return Result.Fail<MessageRecord>(Invalid(ChatCodes.MessageInvalid, "mentions"));
                }
            }
        }

        MessagingOptions current = options.CurrentValue;
        Guid id = Guid.CreateVersion7();
        Protected made = await ProtectAsync(person, text, id, current, cancellationToken).ConfigureAwait(false);
        if (made.Failure is { } unavailable)
        {
            return Result.Fail<MessageRecord>(unavailable);
        }

        Written written = await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockAsync(context, current, [person, .. mentioned], token).ConfigureAwait(false);
                if (!await StaysAsync(context, reached, person, token).ConfigureAwait(false))
                {
                    return new Written(null, NotFound());
                }

                if (scope is null)
                {
                    int present = await context.Participants.CountAsync(row => row.ConversationId == reached.Id && row.LeftAt == null && mentioned.Contains(row.Person), token).ConfigureAwait(false);
                    if (present != mentioned.Length)
                    {
                        return new Written(null, Invalid(ChatCodes.MessageInvalid, "mentions"));
                    }
                }

                if (replyTo is { } reply && !await context.Messages.AnyAsync(row => row.Id == reply.Value && row.ConversationId == reached.Id, token).ConfigureAwait(false))
                {
                    return new Written(null, Invalid(ChatCodes.MessageInvalid, "replyTo"));
                }

                DateTimeOffset now = clock.GetUtcNow();
                MessageRow row = new()
                {
                    Id = id,
                    ConversationId = reached.Id,
                    AuthorKind = Person,
                    Author = person,
                    Type = ConversationSpellings.Of(MessageType.Text),
                    Text = made.Stored,
                    ContentKeyVersion = made.Version,
                    ReplyTo = replyTo?.Value,
                    SentAt = now,
                };
                context.Messages.Add(row);
                foreach (string other in mentioned)
                {
                    context.Mentions.Add(new MentionRow { MessageId = id, Person = other });
                }

                ConversationRow tracked = await context.Conversations.SingleAsync(each => each.Id == reached.Id, token).ConfigureAwait(false);
                tracked.LastMessageAt = now;
                await context.SaveChangesAsync(token).ConfigureAwait(false);
                await LiveNotices.TellAsync(context, LiveNotices.Message, reached.Id, id, token).ConfigureAwait(false);
                return new Written(row, null);
            },
            cancellationToken).ConfigureAwait(false);
        if (written.Failure is { } refused)
        {
            return Result.Fail<MessageRecord>(refused);
        }

        await AwayAsync(person, reached, id, mentioned, cancellationToken).ConfigureAwait(false);

        return Result.Success(Of(written.Row!, text, erased: false, [.. mentioned.Order(StringComparer.Ordinal)]));
    }

    public async ValueTask<Result> EditAsync(string person, ConversationId conversation, MessageId message, string text, CancellationToken cancellationToken)
    {
        Begin(person);
        ArgumentNullException.ThrowIfNull(text);
        if (await ReachAsync(person, conversation.Value, cancellationToken).ConfigureAwait(false) is not { } reached)
        {
            return Result.Fail(NotFound());
        }

        MessageRow? found = await FindAsync(reached.Id, message.Value, cancellationToken).ConfigureAwait(false);
        if (Refused(found, person, editing: true) is { } refused)
        {
            return Result.Fail(refused);
        }

        if (!IsText(text))
        {
            return Result.Fail(Invalid(ChatCodes.MessageInvalid, "text"));
        }

        MessagingOptions current = options.CurrentValue;
        Protected made = await ProtectAsync(person, text, message.Value, current, cancellationToken).ConfigureAwait(false);
        if (made.Failure is { } unavailable)
        {
            return Result.Fail(unavailable);
        }

        Failure? failure = await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockAsync(context, current, [person], token).ConfigureAwait(false);
                if (!await StaysAsync(context, reached, person, token).ConfigureAwait(false))
                {
                    return NotFound();
                }

                MessageRow? row = await LockedAsync(context, reached.Id, message.Value, token).ConfigureAwait(false);
                if (Refused(row, person, editing: true) is { } again)
                {
                    return again;
                }

                row!.Text = made.Stored;
                row.ContentKeyVersion = made.Version;
                row.EditedAt = clock.GetUtcNow();
                await context.SaveChangesAsync(token).ConfigureAwait(false);
                await LiveNotices.TellAsync(context, LiveNotices.Changed, reached.Id, row.Id, token).ConfigureAwait(false);
                return null;
            },
            cancellationToken).ConfigureAwait(false);
        return failure is null ? Result.Success() : Result.Fail(failure);
    }

    public async ValueTask<Result> DeleteAsync(string person, ConversationId conversation, MessageId message, CancellationToken cancellationToken)
    {
        Begin(person);
        if (await ReachAsync(person, conversation.Value, cancellationToken).ConfigureAwait(false) is not { } reached)
        {
            return Result.Fail(NotFound());
        }

        MessagingOptions current = options.CurrentValue;
        Failure? failure = await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockAsync(context, current, [person], token).ConfigureAwait(false);
                if (!await StaysAsync(context, reached, person, token).ConfigureAwait(false))
                {
                    return NotFound();
                }

                MessageRow? row = await LockedAsync(context, reached.Id, message.Value, token).ConfigureAwait(false);
                if (Refused(row, person, editing: false) is { } refused)
                {
                    return refused;
                }

                if (row!.DeletedAt is not null)
                {
                    return null;
                }

                row.Text = null;
                row.ContentKeyVersion = null;
                row.DeletedAt = clock.GetUtcNow();
                context.Mentions.RemoveRange(await context.Mentions.Where(mention => mention.MessageId == row.Id).ToListAsync(token).ConfigureAwait(false));
                await context.SaveChangesAsync(token).ConfigureAwait(false);
                await LiveNotices.TellAsync(context, LiveNotices.Changed, reached.Id, row.Id, token).ConfigureAwait(false);
                return null;
            },
            cancellationToken).ConfigureAwait(false);
        return failure is null ? Result.Success() : Result.Fail(failure);
    }

    public async ValueTask<Result> MarkReadAsync(string person, ConversationId conversation, MessageId message, CancellationToken cancellationToken)
    {
        Begin(person);
        if (await ReachAsync(person, conversation.Value, cancellationToken).ConfigureAwait(false) is not { } reached)
        {
            return Result.Fail(NotFound());
        }

        MessagingOptions current = options.CurrentValue;
        Failure? failure = await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockAsync(context, current, [person], token).ConfigureAwait(false);
                if (!await StaysAsync(context, reached, person, token).ConfigureAwait(false))
                {
                    return NotFound();
                }

                if (!await context.Messages.AnyAsync(row => row.Id == message.Value && row.ConversationId == reached.Id, token).ConfigureAwait(false))
                {
                    return Plain(ChatCodes.MessageNotFound);
                }

                int moved = await context.Database.ExecuteSqlRawAsync(
                    MoveRead,
                    [new NpgsqlParameter("message", message.Value), new NpgsqlParameter("conversation", reached.Id), new NpgsqlParameter("person", person)],
                    token).ConfigureAwait(false);
                if (moved > 0)
                {
                    Guid participant = await context.Participants.Where(each => each.ConversationId == reached.Id && each.Person == person).Select(each => each.Id).SingleAsync(token).ConfigureAwait(false);
                    await LiveNotices.TellAsync(context, LiveNotices.Moved, reached.Id, participant, token).ConfigureAwait(false);
                }

                return null;
            },
            cancellationToken).ConfigureAwait(false);
        return failure is null ? Result.Success() : Result.Fail(failure);
    }

    public async ValueTask<Result> MuteAsync(string person, ConversationId conversation, bool muted, CancellationToken cancellationToken)
    {
        Begin(person);
        if (await ReachAsync(person, conversation.Value, cancellationToken).ConfigureAwait(false) is not { } reached)
        {
            return Result.Fail(NotFound());
        }

        MessagingOptions current = options.CurrentValue;
        bool done = await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockAsync(context, current, [person], token).ConfigureAwait(false);
                if (!await StaysAsync(context, reached, person, token).ConfigureAwait(false))
                {
                    return false;
                }

                ParticipantRow row = await context.Participants.SingleAsync(each => each.ConversationId == reached.Id && each.Person == person, token).ConfigureAwait(false);
                row.Muted = muted;
                await context.SaveChangesAsync(token).ConfigureAwait(false);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
        return done ? Result.Success() : Result.Fail(NotFound());
    }

    public async ValueTask<Result> AddParticipantsAsync(string person, ConversationId conversation, IReadOnlyList<string> participants, CancellationToken cancellationToken)
    {
        Begin(person);
        ArgumentNullException.ThrowIfNull(participants);
        if (await ReachAsync(person, conversation.Value, cancellationToken).ConfigureAwait(false) is not { } reached)
        {
            return Result.Fail(NotFound());
        }

        if (!string.Equals(reached.Type, Direct, StringComparison.Ordinal))
        {
            return Result.Fail(Invalid(ChatCodes.ConversationInvalid, "type"));
        }

        if (!participants.All(IsReference))
        {
            return Result.Fail(Invalid(ChatCodes.ConversationInvalid, "participants"));
        }

        string[] added = [.. participants.Distinct(StringComparer.Ordinal)];
        MessagingOptions current = options.CurrentValue;
        Failure? failure = await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockAsync(context, current, [person, .. added], token).ConfigureAwait(false);
                if (!await StaysAsync(context, reached, person, token).ConfigureAwait(false))
                {
                    return NotFound();
                }

                List<ParticipantRow> rows = await context.Participants.Where(row => row.ConversationId == reached.Id).ToListAsync(token).ConfigureAwait(false);
                DateTimeOffset now = clock.GetUtcNow();
                foreach (string other in added)
                {
                    if (rows.FirstOrDefault(row => string.Equals(row.Person, other, StringComparison.Ordinal)) is { } row)
                    {
                        if (row.LeftAt is not null)
                        {
                            row.LeftAt = null;
                            row.JoinedAt = now;
                        }
                    }
                    else
                    {
                        ParticipantRow joined = new() { Id = Guid.CreateVersion7(), ConversationId = reached.Id, Person = other, JoinedAt = now };
                        rows.Add(joined);
                        context.Participants.Add(joined);
                    }
                }

                if (rows.Count(row => row.LeftAt is null) > MostParticipants)
                {
                    return Plain(ChatCodes.ConversationFull);
                }

                await context.SaveChangesAsync(token).ConfigureAwait(false);
                return null;
            },
            cancellationToken).ConfigureAwait(false);
        return failure is null ? Result.Success() : Result.Fail(failure);
    }

    public async ValueTask<Result> LeaveAsync(string person, ConversationId conversation, CancellationToken cancellationToken)
    {
        Begin(person);
        if (await ReachAsync(person, conversation.Value, cancellationToken).ConfigureAwait(false) is not { } reached)
        {
            return Result.Fail(NotFound());
        }

        if (!string.Equals(reached.Type, Direct, StringComparison.Ordinal))
        {
            return Result.Fail(Invalid(ChatCodes.ConversationInvalid, "type"));
        }

        MessagingOptions current = options.CurrentValue;
        bool done = await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockAsync(context, current, [person], token).ConfigureAwait(false);
                ParticipantRow? row = await context.Participants.FirstOrDefaultAsync(each => each.ConversationId == reached.Id && each.Person == person && each.LeftAt == null, token).ConfigureAwait(false);
                if (row is null)
                {
                    return false;
                }

                row.LeftAt = clock.GetUtcNow();
                await context.SaveChangesAsync(token).ConfigureAwait(false);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
        return done ? Result.Success() : Result.Fail(NotFound());
    }

    /// <summary>
    /// Tells the processes that <paramref name="person"/> is typing in <paramref name="conversation"/>,
    /// where they may read it, writing their participant row first where it does not exist yet;
    /// nothing of it is stored (<c>09</c> MSG-CONV-005).
    /// </summary>
    internal async Task TypingAsync(string person, Guid conversation, CancellationToken cancellationToken)
    {
        if (!ready.IsReady || !IsReference(person) || await ReachAsync(person, conversation, cancellationToken).ConfigureAwait(false) is not { } reached)
        {
            return;
        }

        MessagingOptions current = options.CurrentValue;
        await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockAsync(context, current, [person], token).ConfigureAwait(false);
                if (!await StaysAsync(context, reached, person, token).ConfigureAwait(false))
                {
                    return false;
                }

                Guid participant = await context.Participants.Where(each => each.ConversationId == reached.Id && each.Person == person).Select(each => each.Id).SingleAsync(token).ConfigureAwait(false);
                await LiveNotices.TellAsync(context, LiveNotices.Typing, reached.Id, participant, token).ConfigureAwait(false);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The message <paramref name="message"/> of <paramref name="conversation"/> as a read answers it, or none where it is gone or its text cannot open yet.</summary>
    internal async Task<MessageRecord?> LiveMessageAsync(Guid message, Guid conversation, CancellationToken cancellationToken)
    {
        MessageRow? row;
        List<string> mentions;
        MessagingContext context = database.Context();
        await using (context.ConfigureAwait(false))
        {
            row = await context.Messages.AsNoTracking().FirstOrDefaultAsync(each => each.Id == message && each.ConversationId == conversation, cancellationToken).ConfigureAwait(false);
            mentions = await context.Mentions.AsNoTracking().Where(each => each.MessageId == message).Select(each => each.Person).ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        if (row is null)
        {
            return null;
        }

        Opened opened = await OpenAsync(row, options.CurrentValue, cancellationToken).ConfigureAwait(false);
        if (opened.Rejected)
        {
            await RejectedAsync(cancellationToken).ConfigureAwait(false);
        }

        return opened.Held is null ? Of(row, opened.Text, opened.Erased, [.. mentions.Order(StringComparer.Ordinal)]) : null;
    }

    /// <summary>
    /// Tells through sending, as the kind <c>Hermes:Conversations:AwayKind</c> names, each person
    /// away from <paramref name="written"/>: a direct conversation's participants, or a group
    /// conversation's persons mentioned, who hold no staff-hub connection and have not muted the
    /// conversation, a mention told through a mute; once a person and conversation until their
    /// read position passes the message told (<c>09</c> MSG-CONV-008).
    /// </summary>
    private async Task AwayAsync(string author, ConversationRow reached, Guid written, string[] mentioned, CancellationToken cancellationToken)
    {
        if (settings.AwayKind is not { } kind)
        {
            return;
        }

        bool direct = string.Equals(reached.Type, Direct, StringComparison.Ordinal);
        MessagingOptions current = options.CurrentValue;
        List<(string Person, Guid Participant, bool Mentioned)> away = await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                string[] candidates = direct
                    ? [.. await context.Participants.Where(each => each.ConversationId == reached.Id && each.LeftAt == null && each.Person != author).Select(each => each.Person).ToListAsync(token).ConfigureAwait(false)]
                    : [.. mentioned.Where(each => !string.Equals(each, author, StringComparison.Ordinal))];
                await LockAsync(context, current, candidates, token).ConfigureAwait(false);
                DateTimeOffset now = clock.GetUtcNow();
                List<(string Person, Guid Participant, bool Mentioned)> told = [];
                foreach (string candidate in candidates)
                {
                    await JoinAsync(context, reached.Id, candidate, now, token).ConfigureAwait(false);
                    ParticipantRow row = await context.Participants.SingleAsync(each => each.ConversationId == reached.Id && each.Person == candidate, token).ConfigureAwait(false);
                    bool isMentioned = mentioned.Contains(candidate, StringComparer.Ordinal);
                    if ((row.Muted && !isMentioned)
                        || await LivePresence.OnlineAsync(context, candidate, now, token).ConfigureAwait(false)
                        || (row.AwayTold is { } before && !await PassedAsync(context, row.ReadPosition, before, token).ConfigureAwait(false)))
                    {
                        continue;
                    }

                    row.AwayTold = written;
                    told.Add((candidate, row.Id, isMentioned));
                }

                await context.SaveChangesAsync(token).ConfigureAwait(false);
                return told;
            },
            cancellationToken).ConfigureAwait(false);
        foreach ((string person, Guid participant, bool isMentioned) in away)
        {
            Result<SendReceipt> sent = await messages.SendAsync(
                new Send
                {
                    Kind = kind,
                    Recipient = new Recipient.Person(person),
                    Values = new Dictionary<string, SendValue>(StringComparer.Ordinal)
                    {
                        ["conversation"] = new SendValue.PlainText(reached.Id.ToString("D")),
                        ["conversationType"] = new SendValue.PlainText(reached.Type),
                        ["message"] = new SendValue.PlainText(written.ToString("D")),
                        ["mentioned"] = new SendValue.Flag(isMentioned),
                    },
                    IdempotencyKey = "conversation:" + reached.Id.ToString("D") + ":away:" + participant.ToString("D") + ":" + written.ToString("D"),
                },
                cancellationToken).ConfigureAwait(false);
            if (sent.Failure is { } refused)
            {
                AwayRefused(logger, refused.Code);
            }
        }
    }

    /// <summary>Whether the read position <paramref name="position"/> is at or past the message <paramref name="told"/>.</summary>
    private static async Task<bool> PassedAsync(MessagingContext context, Guid? position, Guid told, CancellationToken cancellationToken)
    {
        if (position is not { } read)
        {
            return false;
        }

        List<MessageRow> rows = await context.Messages.AsNoTracking().Where(each => each.Id == read || each.Id == told).ToListAsync(cancellationToken).ConfigureAwait(false);
        MessageRow? reachedRow = rows.FirstOrDefault(each => each.Id == read);
        MessageRow? toldRow = rows.FirstOrDefault(each => each.Id == told);
        return reachedRow is not null && (toldRow is null || (reachedRow.SentAt, reachedRow.Id).CompareTo((toldRow.SentAt, toldRow.Id)) >= 0);
    }

    [LoggerMessage(EventId = 74, EventName = "ConversationsAwayRefused", Level = LogLevel.Warning, Message = "An away notice was refused with {Code}.")]
    private static partial void AwayRefused(ILogger logger, string code);

    /// <summary>Whether <paramref name="text"/> is a text's length, 1 to 4,000 UTF-16 code units.</summary>
    internal static bool IsText(string text)
    {
        return text.Length is >= 1 and <= MostTextCharacters;
    }

    /// <summary>Whether <paramref name="reference"/> is a person reference's length, 1 to 128 characters.</summary>
    internal static bool IsReference(string? reference)
    {
        return reference is { Length: >= 1 and <= MostReferenceCharacters };
    }

    private void Begin(string person)
    {
        ready.ThrowIfNotReady();
        if (!IsReference(person))
        {
            throw new ArgumentException("A person reference is 1 to 128 characters.", nameof(person));
        }
    }

    /// <summary>
    /// The conversation, where <paramref name="person"/> may read it: a group conversation's
    /// scope answering them as a member, asked now, or a direct one holding them as a
    /// participant who has not left; none otherwise, or where it is unknown.
    /// </summary>
    private async Task<ConversationRow?> ReachAsync(string person, Guid conversation, CancellationToken cancellationToken)
    {
        MessagingContext context = database.Context();
        await using (context.ConfigureAwait(false))
        {
            ConversationRow? row = await context.Conversations.AsNoTracking().FirstOrDefaultAsync(each => each.Id == conversation, cancellationToken).ConfigureAwait(false);
            if (row is null)
            {
                return null;
            }

            if (ScopeOf(row) is { } scope)
            {
                return await membership.IsMemberAsync(person, scope, cancellationToken).ConfigureAwait(false) ? row : null;
            }

            return string.Equals(row.Type, Direct, StringComparison.Ordinal)
                && await context.Participants.AnyAsync(each => each.ConversationId == conversation && each.Person == person && each.LeftAt == null, cancellationToken).ConfigureAwait(false)
                ? row
                : null;
        }
    }

    /// <summary>
    /// Whether <paramref name="person"/> may still read <paramref name="reached"/> in the
    /// transaction: a direct conversation's participant who has not left; a group
    /// conversation's member, whose participant row is written the first time they read or
    /// write it.
    /// </summary>
    private async Task<bool> StaysAsync(MessagingContext context, ConversationRow reached, string person, CancellationToken cancellationToken)
    {
        if (string.Equals(reached.Type, Group, StringComparison.Ordinal))
        {
            await JoinAsync(context, reached.Id, person, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
            return true;
        }

        return await context.Participants.AnyAsync(each => each.ConversationId == reached.Id && each.Person == person && each.LeftAt == null, cancellationToken).ConfigureAwait(false);
    }

    private static async Task JoinAsync(MessagingContext context, Guid conversation, string person, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlRawAsync(
            Join,
            [new NpgsqlParameter("id", Guid.CreateVersion7()), new NpgsqlParameter("conversation", conversation), new NpgsqlParameter("person", person), new NpgsqlParameter("now", now)],
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<MessageRow?> FindAsync(Guid conversation, Guid message, CancellationToken cancellationToken)
    {
        MessagingContext context = database.Context();
        await using (context.ConfigureAwait(false))
        {
            return await context.Messages.AsNoTracking().FirstOrDefaultAsync(row => row.Id == message && row.ConversationId == conversation, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<MessageRow?> LockedAsync(MessagingContext context, Guid conversation, Guid message, CancellationToken cancellationToken)
    {
        List<MessageRow> rows = await context.Messages.FromSqlRaw(LockMessage, new NpgsqlParameter("id", message), new NpgsqlParameter("conversation", conversation)).ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.FirstOrDefault();
    }

    /// <summary>
    /// The refusal of editing or deleting <paramref name="row"/> for <paramref name="person"/>:
    /// none held, another's or not a text, or, for an edit, deleted; none where it may.
    /// </summary>
    private static Failure? Refused(MessageRow? row, string person, bool editing)
    {
        if (row is null)
        {
            return Plain(ChatCodes.MessageNotFound);
        }

        if (!string.Equals(row.AuthorKind, Person, StringComparison.Ordinal)
            || !string.Equals(row.Author, person, StringComparison.Ordinal)
            || !string.Equals(row.Type, ConversationSpellings.Of(MessageType.Text), StringComparison.Ordinal))
        {
            return Plain(ChatCodes.MessageNotAuthor);
        }

        return editing && row.DeletedAt is not null ? Plain(ChatCodes.MessageDeleted) : null;
    }

    private static ConversationScope? ScopeOf(ConversationRow row)
    {
        return row.ScopeKind is { } kind && row.ScopeReference is { } reference
            ? new ConversationScope { Kind = ConversationSpellings.ScopeKindOf(kind), Reference = reference }
            : null;
    }

    /// <summary>The records of <paramref name="rows"/> for <paramref name="person"/>, in their order.</summary>
    /// <param name="context">The transaction's context.</param>
    /// <param name="person">The person reading.</param>
    /// <param name="now">The instant a connection row is fresh from, less 90 seconds.</param>
    /// <param name="rows">The conversations.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    private static async Task<List<ConversationRecord>> RecordsAsync(MessagingContext context, string person, DateTimeOffset now, IReadOnlyList<ConversationRow> rows, CancellationToken cancellationToken)
    {
        Guid[] ids = [.. rows.Select(row => row.Id)];
        List<ParticipantRow> participants = await context.Participants.AsNoTracking()
            .Where(row => ids.Contains(row.ConversationId) && (row.LeftAt == null || row.Person == person))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        List<UnreadCount> unread = await context.Database.SqlQueryRaw<UnreadCount>(Unread, new NpgsqlParameter("person", person), new NpgsqlParameter("ids", ids))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        string[] persons = [.. participants.Select(each => each.Person).Distinct(StringComparer.Ordinal)];
        DateTimeOffset fresh = now - LivePresence.Lifetime;
        HashSet<string> online = [.. await context.Connections.AsNoTracking()
            .Where(each => persons.Contains(each.Person) && each.LastSeenAt >= fresh)
            .Select(each => each.Person)
            .Distinct()
            .ToListAsync(cancellationToken).ConfigureAwait(false)];
        List<ConversationRecord> records = [];
        foreach (ConversationRow row in rows)
        {
            bool direct = string.Equals(row.Type, Direct, StringComparison.Ordinal);
            ParticipantRow? mine = participants.FirstOrDefault(each => each.ConversationId == row.Id && string.Equals(each.Person, person, StringComparison.Ordinal));
            records.Add(new ConversationRecord
            {
                Conversation = new ConversationId(row.Id),
                Type = ConversationSpellings.TypeOf(row.Type),
                Scope = ScopeOf(row),
                Participants = direct
                    ? [.. participants.Where(each => each.ConversationId == row.Id && each.LeftAt is null).OrderBy(each => each.JoinedAt).ThenBy(each => each.Person, StringComparer.Ordinal).Select(each => new ParticipantRecord { Participant = new ParticipantId(each.Id), Person = each.Person, Online = online.Contains(each.Person) })]
                    : [],
                Unread = unread.FirstOrDefault(each => each.Conversation == row.Id)?.Count ?? 0,
                Muted = mine?.Muted ?? false,
                LastAt = row.LastMessageAt ?? row.CreatedAt,
            });
        }

        return records;
    }

    private async Task LockAsync(MessagingContext context, MessagingOptions current, IEnumerable<string> persons, CancellationToken cancellationToken)
    {
        await DeliveryClaims.LockTimeoutAsync(context, current.Dispatch.Timeout, cancellationToken).ConfigureAwait(false);
        long[] keys = [.. persons.Distinct(StringComparer.Ordinal).SelectMany(each => AdvisoryLocks.Person(hashes, each)).Distinct().Order()];
        await AdvisoryLocks.TakeAsync(context, keys, shared: true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Protects <paramref name="text"/> under <paramref name="person"/>'s key, bound to the message's row, bounded by <c>Hermes:Dispatch:Timeout</c>.</summary>
    private async Task<Protected> ProtectAsync(string person, string text, Guid message, MessagingOptions current, CancellationToken cancellationToken)
    {
        string context = ContentContext.Of(ContentContext.Messages, message, ConversationTables.Text);
        try
        {
            using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            bound.CancelAfter(current.Dispatch.Timeout);
            ContentProtecting made = await protection.ProtectAsync(person, Encoding.UTF8.GetBytes(text), context, bound.Token)
                .AsTask()
                .WaitAsync(current.Dispatch.Timeout, clock, cancellationToken)
                .ConfigureAwait(false);
            if (made.Stored is { } stored)
            {
                return new Protected(stored, Sending.Messages.LibraryVersion(stored), null);
            }

            return new Protected(null, null, await UnavailableAsync(made.Key!, made.Version).ConfigureAwait(false));
        }
        catch (Exception failure) when (failure is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new Protected(null, null, await UnavailableAsync(protection.WriterId(), version: null).ConfigureAwait(false));
        }
    }

    private async Task<Failure> UnavailableAsync(string key, int? version)
    {
        Dictionary<string, string> alerted = new(StringComparer.Ordinal);
        if (version is { } held)
        {
            alerted["version"] = held.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        await alerts.HoldsUntilAsync("key-unavailable", key, alerted, CancellationToken.None).ConfigureAwait(false);
        return new Failure { Code = Codes.KeyUnavailable, Details = new Dictionary<string, string>(alerted, StringComparer.Ordinal) { ["key"] = key } };
    }

    /// <summary>Opens a message's text, bounded by <c>Hermes:Dispatch:Timeout</c>, for its author; a text deleted or erased opens to none.</summary>
    private async Task<Opened> OpenAsync(MessageRow row, MessagingOptions current, CancellationToken cancellationToken)
    {
        bool erased = string.Equals(row.AuthorKind, Person, StringComparison.Ordinal) && row.Author is null;
        if (row.Text is not { } stored)
        {
            return new Opened(null, erased, Rejected: false, Held: null);
        }

        ContentOpening opening;
        try
        {
            using CancellationTokenSource bound = new(current.Dispatch.Timeout, clock);
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(bound.Token, cancellationToken);
            opening = await protection.OpenAsync(row.Author, stored, ContentContext.Of(ContentContext.Messages, row.Id, ConversationTables.Text), linked.Token)
                .AsTask()
                .WaitAsync(current.Dispatch.Timeout, clock, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception failure) when (failure is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            opening = ContentOpening.Held(ContentEnvelope.Read(stored).Id);
        }

        return opening.Status switch
        {
            ContentOpeningStatus.Opened => new Opened(Encoding.UTF8.GetString(opening.Plaintext.Span), Erased: false, Rejected: false, Held: null),
            ContentOpeningStatus.Erased => new Opened(null, Erased: true, Rejected: false, Held: null),
            ContentOpeningStatus.Rejected => new Opened(null, Erased: false, Rejected: true, Held: null),
            _ => new Opened(null, Erased: false, Rejected: false, Held: new Failure
            {
                Code = Codes.KeyUnavailable,
                Details = opening.Key is { } key
                    ? new Dictionary<string, string>(StringComparer.Ordinal) { ["key"] = key }
                    : new Dictionary<string, string>(StringComparer.Ordinal),
            }),
        };
    }

    private Task RejectedAsync(CancellationToken cancellationToken)
    {
        return alerts.HoldsUntilAsync(
            "content-rejected",
            ContentContext.Messages,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["table"] = ContentContext.Messages },
            cancellationToken);
    }

    private static MessageRecord Of(MessageRow row, string? text, bool erased, IReadOnlyList<string> mentions)
    {
        AuthorKind kind = ConversationSpellings.AuthorKindOf(row.AuthorKind);
        return new MessageRecord
        {
            Message = new MessageId(row.Id),
            AuthorKind = kind,
            Author = kind == AuthorKind.Person ? row.Author : null,
            Type = ConversationSpellings.MessageTypeOf(row.Type),
            Text = text,
            ReplyTo = row.ReplyTo is { } reply ? new MessageId(reply) : null,
            Mentions = mentions,
            SentAt = row.SentAt,
            EditedAt = row.EditedAt,
            DeletedAt = row.DeletedAt,
            Erased = erased,
        };
    }

    private static Failure NotFound()
    {
        return Plain(ChatCodes.ConversationNotFound);
    }

    private static Failure Plain(string code)
    {
        return new Failure { Code = code, Details = new Dictionary<string, string>(StringComparer.Ordinal) };
    }

    private static Failure Invalid(string code, string field)
    {
        return new Failure { Code = code, Details = new Dictionary<string, string>(StringComparer.Ordinal) { ["field"] = field } };
    }

    private static Failure Malformed(string member)
    {
        return new Failure { Code = Codes.RequestMalformed, Details = new Dictionary<string, string>(StringComparer.Ordinal) { ["member"] = member } };
    }

    private sealed class UnreadCount
    {
        public Guid Conversation { get; init; }

        public int Count { get; init; }
    }

    private sealed record Read(ConversationRow? Conversation, List<MessageRow> Rows, List<MentionRow> Mentions, string? Malformed);

    private sealed record Written(MessageRow? Row, Failure? Failure);

    private sealed record Protected(byte[]? Stored, int? Version, Failure? Failure);

    private sealed record Opened(string? Text, bool Erased, bool Rejected, Failure? Held);
}