using System;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Operations;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Hermes.Conversations.Support;

/// <summary>
/// The retention job's step for conversations (<c>09</c> MSG-CONV-010; <c>06</c>
/// MSG-OPS-004; M-038): a visitor's support conversation goes
/// <c>Hermes:Conversations:Retention:Visitor</c> after it closes, with every row of it, the
/// visitor's acknowledgement going with their last conversation; and, where
/// <c>Hermes:Conversations:Retention:Staff</c> is set, staff-chat messages older than it go;
/// at most 1,000 conversations a run for each, in batches with the claim renewed between them.
/// The attachments of the rows it deletes stay, without their conversation or message, until
/// the <c>attachment-scans</c> job deletes their bytes and then their rows.
/// </summary>
internal sealed partial class ConversationRetention(
    MessagingDatabase database,
    ConversationsSettings settings,
    IOptionsMonitor<MessagingOptions> options,
    ILogger<ConversationRetention> logger) : IRetentionStep
{
    /// <summary>The most conversations a run deletes, or deletes messages of, for each rule.</summary>
    internal const int MostConversations = 1_000;

    private const string VisitorsDue = """
        SELECT id FROM messaging.conversations
        WHERE type = 'support' AND customer IS NULL AND status = 'closed' AND closed_at <= @cutoff
        ORDER BY closed_at, id
        LIMIT @limit
        """;

    private const string DeleteVisitors = """
        WITH visitors AS (
            SELECT DISTINCT visitor FROM messaging.participants WHERE conversation_id = ANY(@ids) AND visitor IS NOT NULL),
        gone AS (
            DELETE FROM messaging.conversations WHERE id = ANY(@ids) AND type = 'support' AND customer IS NULL AND status = 'closed' AND closed_at <= @cutoff
            RETURNING id)
        DELETE FROM messaging.support_acknowledgements AS a
        WHERE a.visitor IN (SELECT visitor FROM visitors)
          AND NOT EXISTS (
              SELECT 1 FROM messaging.participants AS p
              WHERE p.visitor = a.visitor AND p.conversation_id <> ALL(SELECT id FROM gone))
        """;

    private const string StaffDue = """
        SELECT DISTINCT conversation_id FROM messaging.messages AS m
        JOIN messaging.conversations AS c ON c.id = m.conversation_id
        WHERE c.type IN ('direct', 'group') AND m.sent_at <= @cutoff
        ORDER BY conversation_id
        LIMIT @limit
        """;

    private const string DeleteStaff = """
        DELETE FROM messaging.messages AS m
        USING messaging.conversations AS c
        WHERE c.id = m.conversation_id AND c.type IN ('direct', 'group') AND m.conversation_id = ANY(@ids) AND m.sent_at <= @cutoff
        """;

    public async Task<bool> RunAsync(DateTimeOffset now, Func<Task<bool>> renew, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(renew);
        if (!await RunRuleAsync(VisitorsDue, DeleteVisitors, now - settings.Support.RetentionVisitor, renew, count => VisitorsDeleted(logger, count), cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        return settings.Support.RetentionStaff is not { } staff
            || await RunRuleAsync(StaffDue, DeleteStaff, now - staff, renew, count => StaffDeleted(logger, count), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads at most 1,000 conversations <paramref name="due"/> names and runs <paramref name="delete"/> on them a batch at a time.</summary>
    private async Task<bool> RunRuleAsync(string due, string delete, DateTimeOffset cutoff, Func<Task<bool>> renew, Action<int> logged, CancellationToken cancellationToken)
    {
        MessagingOptions current = options.CurrentValue;
        Guid[] ids;
        MessagingContext reading = database.Context();
        await using (reading.ConfigureAwait(false))
        {
            ids = [.. await reading.Database.SqlQueryRaw<Guid>(due, new NpgsqlParameter("cutoff", cutoff), new NpgsqlParameter("limit", MostConversations))
                .ToListAsync(cancellationToken).ConfigureAwait(false)];
        }

        int batch = current.Jobs.BatchSize;
        for (int start = 0; start < ids.Length; start += batch)
        {
            Guid[] slice = ids[start..Math.Min(ids.Length, start + batch)];
            await LibraryTransactions.RunAsync(
                database,
                current,
                (context, token) => context.Database.ExecuteSqlRawAsync(delete, [new NpgsqlParameter("ids", slice), new NpgsqlParameter("cutoff", cutoff)], token),
                cancellationToken).ConfigureAwait(false);
            if (!await renew().ConfigureAwait(false))
            {
                return false;
            }
        }

        if (ids.Length > 0)
        {
            logged(ids.Length);
        }

        return true;
    }

    [LoggerMessage(EventId = 84, EventName = "RetentionVisitorConversationsDeleted", Level = LogLevel.Information, Message = "The retention job deleted {Count} visitors' conversations past their retention.")]
    private static partial void VisitorsDeleted(ILogger logger, int count);

    [LoggerMessage(EventId = 85, EventName = "RetentionStaffMessagesDeleted", Level = LogLevel.Information, Message = "The retention job deleted the old messages of {Count} staff chats.")]
    private static partial void StaffDeleted(ILogger logger, int count);
}