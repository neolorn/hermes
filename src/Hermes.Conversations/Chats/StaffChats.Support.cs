using System;
using System.Collections.Generic;
using System.Linq;
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
/// The staff side of support (<c>09</c> MSG-CONV-002, MSG-CONV-011): availability, a topic's
/// queue, transfer, close and blocks. A member of the topic's scope works a support
/// conversation of the topic; anyone else is answered as for a conversation they may not read.
/// </summary>
internal sealed partial class StaffChats
{
    private static readonly string SupportType = ConversationSpellings.Of(ConversationType.Support);

    private static readonly string OpenStatus = ConversationSpellings.Of(ConversationStatus.Open);

    private static readonly string ClosedStatus = ConversationSpellings.Of(ConversationStatus.Closed);

    private const string Queued = """
        SELECT * FROM messaging.conversations
        WHERE type = 'support' AND topic = @topic AND status = 'open' AND assignee IS NULL
        ORDER BY queued_at, id
        """;

    private const string LockConversation = "SELECT * FROM messaging.conversations WHERE id = @id FOR UPDATE";

    public async ValueTask<Result> SetAvailableAsync(string person, bool available, CancellationToken cancellationToken)
    {
        Begin(person);
        MessagingOptions current = options.CurrentValue;
        await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockAsync(context, current, [person], token).ConfigureAwait(false);
                DateTimeOffset now = clock.GetUtcNow();
                SupportAgentRow? agent = await context.SupportAgents.FirstOrDefaultAsync(each => each.Person == person, token).ConfigureAwait(false);
                if (agent is null)
                {
                    context.SupportAgents.Add(new SupportAgentRow { Id = Guid.CreateVersion7(), Person = person, Available = available, ChangedAt = now });
                }
                else
                {
                    agent.Available = available;
                    agent.ChangedAt = now;
                }

                await context.SaveChangesAsync(token).ConfigureAwait(false);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
        if (available)
        {
            await assigner.AssignAsync(null, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }

    public async ValueTask<Result<ConversationPage>> QueueAsync(string person, string topic, CancellationToken cancellationToken)
    {
        Begin(person);
        ArgumentNullException.ThrowIfNull(topic);
        if (!settings.Support.Topics.TryGetValue(topic, out SupportTopic? configured))
        {
            return Result.Fail<ConversationPage>(Malformed("topic"));
        }

        if (!await membership.IsMemberAsync(person, configured.Scope, cancellationToken).ConfigureAwait(false))
        {
            return Result.Success(new ConversationPage { Conversations = [] });
        }

        MessagingOptions current = options.CurrentValue;
        List<ConversationRecord> records = await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockAsync(context, current, [person], token).ConfigureAwait(false);
                List<ConversationRow> rows = await context.Conversations.FromSqlRaw(Queued, new NpgsqlParameter("topic", topic)).AsNoTracking().ToListAsync(token).ConfigureAwait(false);
                return await RecordsAsync(context, person, clock.GetUtcNow(), rows, token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
        return Result.Success(new ConversationPage { Conversations = records });
    }

    public async ValueTask<Result> TransferAsync(string person, ConversationId conversation, string? toPerson, string? topic, CancellationToken cancellationToken)
    {
        Begin(person);
        if ((toPerson is null) == (topic is null))
        {
            throw new ArgumentException("A transfer names a person or a topic, one of the two.", nameof(toPerson));
        }

        if (await ReachSupportAsync(person, conversation.Value, cancellationToken).ConfigureAwait(false) is not { } reached)
        {
            return Result.Fail(NotFound());
        }

        SupportTopic from = settings.Support.Topics[reached.Topic!];
        SupportTopic? to = null;
        if (topic is not null && !settings.Support.Topics.TryGetValue(topic, out to))
        {
            return Result.Fail(Invalid(ChatCodes.ConversationInvalid, "topic"));
        }

        if (toPerson is not null && (!IsReference(toPerson) || !await membership.IsMemberAsync(toPerson, from.Scope, cancellationToken).ConfigureAwait(false)))
        {
            return Result.Fail(Invalid(ChatCodes.ConversationInvalid, "person"));
        }

        MessagingOptions current = options.CurrentValue;
        bool moved = await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockAsync(context, current, toPerson is null ? [person] : [person, toPerson], token).ConfigureAwait(false);
                ConversationRow? row = await LockedConversationAsync(context, reached.Id, token).ConfigureAwait(false);
                if (row is null || string.Equals(row.Status, ClosedStatus, StringComparison.Ordinal))
                {
                    return false;
                }

                DateTimeOffset now = clock.GetUtcNow();
                if (toPerson is not null)
                {
                    await SupportAssigner.AssignToAsync(context, row, toPerson, now, token).ConfigureAwait(false);
                    return true;
                }

                bool wasQueued = row.Assignee is null;
                string left = row.Topic!;
                row.Topic = to!.Name;
                row.Assignee = null;
                row.AssignedAt = null;
                row.QueuedAt = now;
                row.WaitingAt = WaitingOf(row);
                await context.SaveChangesAsync(token).ConfigureAwait(false);
                await LiveNotices.TellQueueAsync(context, to.Name, token).ConfigureAwait(false);
                if (wasQueued && !string.Equals(left, to.Name, StringComparison.Ordinal))
                {
                    await LiveNotices.TellQueueAsync(context, left, token).ConfigureAwait(false);
                }

                return true;
            },
            cancellationToken).ConfigureAwait(false);
        if (!moved)
        {
            return Result.Fail(NotFound());
        }

        await assigner.AssignAsync(to is null ? [from.Name] : [to.Name, from.Name], cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async ValueTask<Result> CloseAsync(string person, ConversationId conversation, CancellationToken cancellationToken)
    {
        Begin(person);
        if (await ReachSupportAsync(person, conversation.Value, cancellationToken).ConfigureAwait(false) is not { } reached)
        {
            return Result.Fail(NotFound());
        }

        bool closed = await CloseSupportAsync(reached.Id, person, cancellationToken).ConfigureAwait(false);
        if (closed)
        {
            await assigner.AssignAsync([reached.Topic!], cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }

    public async ValueTask<Result> BlockAsync(string person, ConversationId conversation, DateTimeOffset? until, CancellationToken cancellationToken)
    {
        Begin(person);
        if (await ReachSupportAsync(person, conversation.Value, cancellationToken).ConfigureAwait(false) is not { } reached)
        {
            return Result.Fail(NotFound());
        }

        MessagingOptions current = options.CurrentValue;
        await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockAsync(context, current, reached.Customer is { } customer ? [person, customer] : [person], token).ConfigureAwait(false);
                ParticipantRow? visitor = reached.Customer is null
                    ? await context.Participants.AsNoTracking().FirstOrDefaultAsync(each => each.ConversationId == reached.Id && each.Visitor != null, token).ConfigureAwait(false)
                    : null;
                context.SupportBlocks.Add(new SupportBlockRow
                {
                    Id = Guid.CreateVersion7(),
                    Person = reached.Customer,
                    Visitor = visitor?.Visitor,
                    HashVersion = visitor?.HashVersion,
                    Until = until,
                    BlockedAt = clock.GetUtcNow(),
                });
                await context.SaveChangesAsync(token).ConfigureAwait(false);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async ValueTask<Result> UnblockAsync(string person, ConversationId conversation, CancellationToken cancellationToken)
    {
        Begin(person);
        if (await ReachSupportAsync(person, conversation.Value, cancellationToken).ConfigureAwait(false) is not { } reached)
        {
            return Result.Fail(NotFound());
        }

        MessagingOptions current = options.CurrentValue;
        await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockAsync(context, current, reached.Customer is { } customer ? [person, customer] : [person], token).ConfigureAwait(false);
                if (reached.Customer is { } blocked)
                {
                    await context.SupportBlocks.Where(each => each.Person == blocked).ExecuteDeleteAsync(token).ConfigureAwait(false);
                }
                else if (await context.Participants.AsNoTracking().FirstOrDefaultAsync(each => each.ConversationId == reached.Id && each.Visitor != null, token).ConfigureAwait(false) is { Visitor: { } hash })
                {
                    await context.SupportBlocks.Where(each => each.Visitor == hash).ExecuteDeleteAsync(token).ConfigureAwait(false);
                }

                return true;
            },
            cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <summary>
    /// Closes the support conversation <paramref name="conversation"/>, by the staff member
    /// <paramref name="by"/> or by its visitor or customer where none, telling the processes;
    /// false where it was closed already.
    /// </summary>
    internal async Task<bool> CloseSupportAsync(Guid conversation, string? by, CancellationToken cancellationToken)
    {
        MessagingOptions current = options.CurrentValue;
        return await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                if (by is not null)
                {
                    await LockAsync(context, current, [by], token).ConfigureAwait(false);
                }

                ConversationRow? row = await LockedConversationAsync(context, conversation, token).ConfigureAwait(false);
                if (row is null || string.Equals(row.Status, ClosedStatus, StringComparison.Ordinal))
                {
                    return false;
                }

                bool wasQueued = row.Assignee is null;
                Closed(row, clock.GetUtcNow(), by);
                await context.SaveChangesAsync(token).ConfigureAwait(false);
                await LiveNotices.TellAsync(context, LiveNotices.Closed, row.Id, row.Id, token).ConfigureAwait(false);
                if (wasQueued)
                {
                    await LiveNotices.TellQueueAsync(context, row.Topic!, token).ConfigureAwait(false);
                }

                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The support conversation <paramref name="conversation"/>, where <paramref name="person"/> is a member of its topic's scope; none otherwise, or where its topic is no longer configured.</summary>
    private async Task<ConversationRow?> ReachSupportAsync(string person, Guid conversation, CancellationToken cancellationToken)
    {
        ConversationRow? row;
        MessagingContext context = database.Context();
        await using (context.ConfigureAwait(false))
        {
            row = await context.Conversations.AsNoTracking().FirstOrDefaultAsync(each => each.Id == conversation && each.Type == SupportType, cancellationToken).ConfigureAwait(false);
        }

        return row is not null && await IsSupportMemberAsync(person, row, cancellationToken).ConfigureAwait(false) ? row : null;
    }

    /// <summary>Whether <paramref name="person"/> is a member of the scope of <paramref name="row"/>'s topic, asked now.</summary>
    private async Task<bool> IsSupportMemberAsync(string person, ConversationRow row, CancellationToken cancellationToken)
    {
        return row.Topic is { } name
            && settings.Support.Topics.TryGetValue(name, out SupportTopic? topic)
            && await membership.IsMemberAsync(person, topic.Scope, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ConversationRow?> LockedConversationAsync(MessagingContext context, Guid conversation, CancellationToken cancellationToken)
    {
        return (await context.Conversations.FromSqlRaw(LockConversation, new NpgsqlParameter("id", conversation)).ToListAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault();
    }

    /// <summary>
    /// Sends a staff reply of an offline conversation by email, as the kind
    /// <c>Hermes:Conversations:Support:OfflineReplyKind</c> names, to the address the form took
    /// (<c>09</c> MSG-CONV-002): its values the conversation's and the reply's identifiers, the
    /// topic and the reply's text. A send refused, or an address that does not open, is logged,
    /// one that does not open under its binding raising <c>content-rejected</c> for <c>conversations</c>,
    /// and never fails the write.
    /// </summary>
    private async Task OfflineReplyAsync(ConversationRow reached, Guid reply, string text, CancellationToken cancellationToken)
    {
        if (settings.Support.OfflineReplyKind is not { } kind || reached.Email is not { } stored)
        {
            return;
        }

        ContentOpening opening = await protection.OpenAsync(reached.Customer, stored, ContentContext.Of(ContentContext.Conversations, reached.Id, ConversationTables.Email), cancellationToken).ConfigureAwait(false);
        if (opening.Status == ContentOpeningStatus.Rejected)
        {
            await alerts.HoldsUntilAsync("content-rejected", ContentContext.Conversations, new Dictionary<string, string>(StringComparer.Ordinal) { ["table"] = ContentContext.Conversations }, cancellationToken).ConfigureAwait(false);
        }

        if (opening.Status != ContentOpeningStatus.Opened)
        {
            OfflineReplyRefused(logger, opening.Status == ContentOpeningStatus.Rejected ? "content-rejected" : "messaging.key.unavailable");
            return;
        }

        Result<SendReceipt> sent = await messages.SendAsync(
            new Send
            {
                Kind = kind,
                Recipient = new Recipient.Anonymous(),
                Email = System.Text.Encoding.UTF8.GetString(opening.Plaintext.Span),
                Values = new Dictionary<string, SendValue>(StringComparer.Ordinal)
                {
                    ["conversation"] = new SendValue.PlainText(reached.Id.ToString("D")),
                    ["message"] = new SendValue.PlainText(reply.ToString("D")),
                    ["topic"] = new SendValue.PlainText(reached.Topic!),
                    ["text"] = new SendValue.PlainText(text),
                },
                IdempotencyKey = "conversation:" + reached.Id.ToString("D") + ":reply:" + reply.ToString("D"),
            },
            cancellationToken).ConfigureAwait(false);
        if (sent.Failure is { } refused)
        {
            OfflineReplyRefused(logger, refused.Code);
        }
    }

    [Microsoft.Extensions.Logging.LoggerMessage(EventId = 75, EventName = "ConversationsOfflineReplyRefused", Level = Microsoft.Extensions.Logging.LogLevel.Warning, Message = "An offline conversation's reply was refused with {Code}.")]
    private static partial void OfflineReplyRefused(Microsoft.Extensions.Logging.ILogger logger, string code);

    /// <summary>A queued support conversation's place in its topic's queue, from 1; none once it is assigned or closed.</summary>
    internal static async Task<int?> PlaceAsync(MessagingContext context, ConversationRow row, CancellationToken cancellationToken)
    {
        if (row.Assignee is not null || !string.Equals(row.Status, OpenStatus, StringComparison.Ordinal) || row.QueuedAt is not { } queuedAt)
        {
            return null;
        }

        return await context.Database.SqlQueryRaw<int>(
            """
            SELECT count(*)::integer AS "Value" FROM messaging.conversations
            WHERE type = 'support' AND topic = @topic AND status = 'open' AND assignee IS NULL AND (queued_at, id) <= (@queued_at, @id)
            """,
            new NpgsqlParameter("topic", row.Topic),
            new NpgsqlParameter("queued_at", queuedAt),
            new NpgsqlParameter("id", row.Id)).SingleAsync(cancellationToken).ConfigureAwait(false);
    }
}