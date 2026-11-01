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
using Microsoft.Extensions.Logging;

namespace Hermes.Conversations.Chats;

/// <summary>
/// Automatic replies and the timers of a support conversation (<c>09</c> MSG-CONV-003,
/// MSG-CONV-005): each rule fires once per conversation, in name order where two fire at once,
/// writing an <c>automatic</c> message from its template in the conversation's language, never
/// emailed; a reply that fails to render or to be protected writes nothing, is logged, and
/// counts as fired. The timers' instants are columns of the conversation, moved in the
/// transaction that fires them: <c>waiting_at</c>, <c>closing_at</c>, <c>close_at</c> and
/// <c>snoozed_until</c>.
/// </summary>
internal sealed partial class StaffChats
{
    private static readonly string Library = ConversationSpellings.Of(AuthorKind.Library);

    private static readonly string AutomaticType = ConversationSpellings.Of(MessageType.Automatic);

    private static readonly string SystemType = ConversationSpellings.Of(MessageType.System);

    /// <summary>The most conversations one unit of the timers' pass takes at once.</summary>
    private const int TimersBatch = 100;

    /// <summary>
    /// Moves the silence of <paramref name="row"/> to <paramref name="now"/>, as a
    /// <c>text</c> of a staff member, a visitor or a customer does: its automatic close
    /// <c>CloseAfter</c> later, and the <c>closing</c> rules' instant <c>CloseWarning</c> before
    /// it, where one serves its topic and has not fired.
    /// </summary>
    private void Silence(ConversationRow row, DateTimeOffset now)
    {
        row.CloseAt = now + settings.Support.CloseAfter;
        row.ClosingAt = ClosingOf(row);
    }

    /// <summary>The instant the <c>closing</c> rules fire for <paramref name="row"/>, none where none serves it unfired or it has no close.</summary>
    private DateTimeOffset? ClosingOf(ConversationRow row)
    {
        return row.CloseAt is { } close && Unfired(row, ReplyTriggers.Closing).Any() ? close - settings.Support.CloseWarning : null;
    }

    /// <summary>The first instant a <c>waiting</c> rule fires for <paramref name="row"/>, none where it is not queued or none serves it unfired.</summary>
    private DateTimeOffset? WaitingOf(ConversationRow row)
    {
        if (row.Assignee is not null || !string.Equals(row.Status, OpenStatus, StringComparison.Ordinal) || row.QueuedAt is not { } queuedAt)
        {
            return null;
        }

        List<ReplyRule> waiting = [.. Unfired(row, ReplyTriggers.Waiting)];
        return waiting.Count > 0 ? queuedAt + waiting.Min(rule => rule.After!.Value) : null;
    }

    /// <summary>The rules of <paramref name="trigger"/> serving <paramref name="row"/>'s topic that have not fired on it, in name order.</summary>
    private IEnumerable<ReplyRule> Unfired(ConversationRow row, string trigger)
    {
        return settings.Support.Rules.Where(rule => string.Equals(rule.Trigger, trigger, StringComparison.Ordinal)
            && row.Topic is { } topic && rule.Serves(topic)
            && !row.RepliesFired.Contains(rule.Name, StringComparer.Ordinal));
    }

    /// <summary>
    /// Fires the rules of <paramref name="triggers"/> on the support conversation
    /// <paramref name="conversation"/> at <paramref name="now"/>, in one transaction under its
    /// row's lock: nothing on a closed one; a <c>waiting</c> rule only on a queued one that has
    /// waited its <c>After</c>. Moves its <c>waiting</c> and <c>closing</c> instants after.
    /// </summary>
    internal async Task FireAsync(Guid conversation, IReadOnlyCollection<string> triggers, DateTimeOffset now, CancellationToken cancellationToken)
    {
        MessagingOptions current = options.CurrentValue;
        await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                ConversationRow? row = await LockedConversationAsync(context, conversation, token).ConfigureAwait(false);
                if (row is null || string.Equals(row.Status, ClosedStatus, StringComparison.Ordinal))
                {
                    return [];
                }

                DateTimeOffset? waitingFrom = WaitingOf(row) is null ? null : row.QueuedAt;
                List<ReplyRule> due = [.. triggers
                    .SelectMany(trigger => Unfired(row, trigger))
                    .Where(rule => !string.Equals(rule.Trigger, ReplyTriggers.Waiting, StringComparison.Ordinal) || waitingFrom + rule.After <= now)
                    .OrderBy(rule => rule.Name, StringComparer.Ordinal)];
                List<Guid> messages = [];
                if (due.Count > 0)
                {
                    IReadOnlyDictionary<string, SendValue?> values = await ReplyValuesAsync(context, row, now, token).ConfigureAwait(false);
                    SupportTopic topic = settings.Support.Topics[row.Topic!];
                    string language = row.Language ?? current.DefaultLanguage ?? current.Languages[0];
                    foreach (ReplyRule rule in due)
                    {
                        row.RepliesFired = [.. row.RepliesFired, rule.Name];
                        string? text = await replyTemplates.RenderAsync(rule.Name, language, values, topic.Zone, current.Templates.MaxSteps).ConfigureAwait(false);
                        if (text is null)
                        {
                            ReplyNotRendered(logger, rule.Name, language);
                            continue;
                        }

                        // Replies fired at once keep their name order in a read, a microsecond apart.
                        DateTimeOffset sentAt = now.AddTicks(TimeSpan.TicksPerMicrosecond * messages.Count);
                        Guid id = Guid.CreateVersion7();
                        Protected made = await ProtectAsync(row.Customer, text, id, current, token).ConfigureAwait(false);
                        if (made.Failure is { } unavailable)
                        {
                            ReplyNotProtected(logger, rule.Name, unavailable.Code);
                            continue;
                        }

                        context.Messages.Add(new MessageRow
                        {
                            Id = id,
                            ConversationId = row.Id,
                            AuthorKind = Library,
                            Owner = row.Customer,
                            Type = AutomaticType,
                            Text = made.Stored,
                            ContentKeyVersion = made.Version,
                            SentAt = sentAt,
                        });
                        messages.Add(id);
                        row.LastMessageAt = sentAt;
                    }
                }

                row.WaitingAt = WaitingOf(row);
                row.ClosingAt = ClosingOf(row);
                await context.SaveChangesAsync(token).ConfigureAwait(false);
                foreach (Guid message in messages)
                {
                    await LiveNotices.TellAsync(context, LiveNotices.Message, row.Id, message, token).ConfigureAwait(false);
                }

                return messages;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// One pass of the timers at <paramref name="now"/> (<c>09</c> MSG-CONV-005): wakes the
    /// snoozed conversations whose instant has come, fires <c>waiting</c>, then <c>closing</c>,
    /// then closes the conversations silent past <c>CloseAfter</c>, each conversation in a
    /// transaction of its own; <paramref name="renew"/> runs between units, and the pass stops
    /// where it answers false.
    /// </summary>
    internal async Task TimersAsync(DateTimeOffset now, Func<CancellationToken, Task<bool>> renew, CancellationToken cancellationToken)
    {
        if (!ready.IsReady)
        {
            return;
        }

        foreach (Guid snoozed in await DueAsync(row => row.Status == SnoozedStatus && row.SnoozedUntil <= now, cancellationToken).ConfigureAwait(false))
        {
            await WakeAsync(snoozed, now, cancellationToken).ConfigureAwait(false);
        }

        if (!await renew(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        foreach (Guid waiting in await DueAsync(row => row.Status == OpenStatus && row.WaitingAt <= now, cancellationToken).ConfigureAwait(false))
        {
            await FireAsync(waiting, [ReplyTriggers.Waiting], now, cancellationToken).ConfigureAwait(false);
        }

        if (!await renew(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        foreach (Guid closing in await DueAsync(row => (row.Status == OpenStatus || row.Status == SnoozedStatus) && row.ClosingAt <= now, cancellationToken).ConfigureAwait(false))
        {
            await FireAsync(closing, [ReplyTriggers.Closing], now, cancellationToken).ConfigureAwait(false);
        }

        if (!await renew(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        HashSet<string> topics = new(StringComparer.Ordinal);
        foreach (Guid closed in await DueAsync(row => (row.Status == OpenStatus || row.Status == SnoozedStatus) && row.CloseAt <= now, cancellationToken).ConfigureAwait(false))
        {
            if (await CloseSilentAsync(closed, now, cancellationToken).ConfigureAwait(false) is { } topic)
            {
                topics.Add(topic);
                metrics.ConversationsClosed.Add(1, new KeyValuePair<string, object?>("topic", topic));
            }
        }

        if (topics.Count > 0)
        {
            await assigner.AssignAsync(topics, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Wakes the snoozed conversation <paramref name="conversation"/> whose instant has come by
    /// <paramref name="now"/>, under its row's lock, and pushes its assignee the conversation as
    /// it now reads, the staff hub's push of a conversation, so their list shows it open.
    /// </summary>
    private async Task WakeAsync(Guid conversation, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await LibraryTransactions.RunAsync(
            database,
            options.CurrentValue,
            async (context, token) =>
            {
                ConversationRow? row = await LockedConversationAsync(context, conversation, token).ConfigureAwait(false);
                if (row is null || !string.Equals(row.Status, SnoozedStatus, StringComparison.Ordinal) || row.SnoozedUntil is not { } until || until > now)
                {
                    return false;
                }

                bool queued = row.Assignee is null;
                row.Status = OpenStatus;
                row.SnoozedUntil = null;
                row.WaitingAt = WaitingOf(row);
                await context.SaveChangesAsync(token).ConfigureAwait(false);
                await LiveNotices.TellAsync(context, LiveNotices.Conversation, row.Id, row.Id, token).ConfigureAwait(false);
                if (queued)
                {
                    await LiveNotices.TellQueueAsync(context, row.Topic!, token).ConfigureAwait(false);
                }

                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The support conversations <paramref name="due"/> selects, oldest first, at most a batch.</summary>
    private async Task<List<Guid>> DueAsync(System.Linq.Expressions.Expression<Func<ConversationRow, bool>> due, CancellationToken cancellationToken)
    {
        MessagingContext context = database.Context();
        await using (context.ConfigureAwait(false))
        {
            return await context.Conversations.AsNoTracking()
                .Where(row => row.Type == SupportType)
                .Where(due)
                .OrderBy(row => row.Id)
                .Select(row => row.Id)
                .Take(TimersBatch)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Closes the support conversation <paramref name="conversation"/> silent past
    /// <c>CloseAfter</c> at <paramref name="now"/>: a <c>system</c> message without text and the
    /// status <c>closed</c>, telling the processes; answers its topic, or none where a
    /// <c>text</c> moved its close or it is closed already.
    /// </summary>
    private async Task<string?> CloseSilentAsync(Guid conversation, DateTimeOffset now, CancellationToken cancellationToken)
    {
        MessagingOptions current = options.CurrentValue;
        return await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                ConversationRow? row = await LockedConversationAsync(context, conversation, token).ConfigureAwait(false);
                if (row is null || string.Equals(row.Status, ClosedStatus, StringComparison.Ordinal) || row.CloseAt is not { } close || close > now)
                {
                    return null;
                }

                await CloseRowAsync(context, row, now, by: null, token).ConfigureAwait(false);
                return row.Topic;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Closes <paramref name="row"/>, locked in the transaction, at <paramref name="now"/> by
    /// <paramref name="by"/>, none for its visitor or customer or for its silence (<c>09</c>
    /// MSG-CONV-002): the status <c>closed</c>, its timers stopped, and one <c>system</c>
    /// message without text, telling the processes of the message, the close and, where it was
    /// queued, its topic's queue.
    /// </summary>
    private static async Task CloseRowAsync(MessagingContext context, ConversationRow row, DateTimeOffset now, string? by, CancellationToken cancellationToken)
    {
        bool wasQueued = row.Assignee is null && string.Equals(row.Status, OpenStatus, StringComparison.Ordinal);
        Guid message = Guid.CreateVersion7();
        context.Messages.Add(new MessageRow
        {
            Id = message,
            ConversationId = row.Id,
            AuthorKind = Library,
            Owner = row.Customer,
            Type = SystemType,
            SentAt = now,
        });
        Closed(row, now, by);
        row.LastMessageAt = now;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await LiveNotices.TellAsync(context, LiveNotices.Message, row.Id, message, cancellationToken).ConfigureAwait(false);
        await LiveNotices.TellAsync(context, LiveNotices.Closed, row.Id, row.Id, cancellationToken).ConfigureAwait(false);
        if (wasQueued)
        {
            await LiveNotices.TellQueueAsync(context, row.Topic!, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Marks <paramref name="row"/> closed at <paramref name="now"/> by <paramref name="by"/>, its timers stopped.</summary>
    private static void Closed(ConversationRow row, DateTimeOffset now, string? by)
    {
        row.Status = ClosedStatus;
        row.ClosedAt = now;
        row.ClosedBy = by;
        row.SnoozedUntil = null;
        row.WaitingAt = null;
        row.ClosingAt = null;
        row.CloseAt = null;
    }

    /// <summary>
    /// A reply's four values (<c>09</c> MSG-CONV-003): <c>topic</c>; <c>place</c>, the
    /// visitor's place, or none; <c>wait</c>, the whole minutes since it was queued; and
    /// <c>opens</c>, the topic's next opening in its time zone, none within its hours.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, SendValue?>> ReplyValuesAsync(MessagingContext context, ConversationRow row, DateTimeOffset now, CancellationToken cancellationToken)
    {
        SupportTopic topic = settings.Support.Topics[row.Topic!];
        int? place = await PlaceAsync(context, row, cancellationToken).ConfigureAwait(false);
        DateTimeOffset since = row.QueuedAt ?? row.CreatedAt;
        long wait = now > since ? (long)Math.Floor((now - since).TotalMinutes) : 0;
        DateTimeOffset? opens = topic.IsWithinHours(now) ? null : topic.NextOpening(now);
        return new Dictionary<string, SendValue?>(StringComparer.Ordinal)
        {
            ["topic"] = new SendValue.PlainText(topic.Name),
            ["place"] = place is { } held ? new SendValue.WholeNumber(held) : null,
            ["wait"] = new SendValue.WholeNumber(wait),
            ["opens"] = opens is { } instant ? new SendValue.Instant(instant) : null,
        };
    }

    [LoggerMessage(EventId = 76, EventName = "ConversationsReplyNotRendered", Level = LogLevel.Warning, Message = "The automatic reply {Rule} did not render in {Language}, or rendered past its bound; it counts as fired.")]
    private static partial void ReplyNotRendered(ILogger logger, string rule, string language);

    [LoggerMessage(EventId = 77, EventName = "ConversationsReplyNotProtected", Level = LogLevel.Warning, Message = "The automatic reply {Rule} could not be protected ({Code}); it counts as fired.")]
    private static partial void ReplyNotProtected(ILogger logger, string rule, string code);
}