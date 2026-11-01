using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Conversations.Live;
using Hermes.Core;
using Hermes.Operations;
using Hermes.Sending;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Hermes.Conversations.Support;

/// <summary>
/// Assigns queued support conversations (<c>09</c> MSG-CONV-002): within a topic's hours, its
/// oldest queued conversation goes to the available member of its scope with the fewest open
/// conversations, ties to the one longest since their last assignment, until the queue or the
/// members run out. A member is available while they hold a fresh staff-hub connection, have
/// set themselves available, and hold fewer open support conversations than the limit. Each
/// assignment locks the conversation, skipping one another process holds, and the member's
/// availability row, counting their open conversations again under it, so two processes never
/// assign past the limit.
/// </summary>
internal sealed class SupportAssigner(
    MessagingDatabase database,
    IOptionsMonitor<MessagingOptions> options,
    KeyedHashes hashes,
    ConversationsSettings settings,
    IServiceScopeFactory scopes,
    TimeProvider clock)
{
    private const string Oldest = """
        SELECT * FROM messaging.conversations
        WHERE type = 'support' AND topic = @topic AND status = 'open' AND assignee IS NULL
        ORDER BY queued_at, id
        LIMIT 1
        FOR UPDATE SKIP LOCKED
        """;

    private const string Candidates = """
        SELECT a.person AS "Person",
               (SELECT count(*) FROM messaging.conversations AS c WHERE c.type = 'support' AND c.status = 'open' AND c.assignee = a.person)::integer AS "Open",
               a.last_assigned_at AS "LastAssignedAt"
        FROM messaging.support_agents AS a
        WHERE a.available AND a.person = ANY(@members)
          AND EXISTS (SELECT 1 FROM messaging.connections AS k WHERE k.person = a.person AND k.last_seen_at >= @fresh)
        """;

    private const string LockAgent = "SELECT * FROM messaging.support_agents WHERE person = @person FOR UPDATE";

    private const string OpenOf = "SELECT count(*)::integer AS \"Value\" FROM messaging.conversations WHERE type = 'support' AND status = 'open' AND assignee = @person";

    private const string Join = """
        INSERT INTO messaging.participants (id, conversation_id, person, muted, joined_at)
        VALUES (@id, @conversation, @person, false, @now)
        ON CONFLICT (conversation_id, person) DO NOTHING
        """;

    /// <summary>Assigns what can be assigned in <paramref name="topics"/>, or in every topic where none are named.</summary>
    internal async Task AssignAsync(IEnumerable<string>? topics, CancellationToken cancellationToken)
    {
        foreach (string name in (topics ?? settings.Support.Topics.Keys).Distinct(StringComparer.Ordinal))
        {
            if (!settings.Support.Topics.TryGetValue(name, out SupportTopic? topic) || !topic.IsWithinHours(clock.GetUtcNow()))
            {
                continue;
            }

            string[] members = [.. await MembersAsync(topic.Scope, cancellationToken).ConfigureAwait(false)];
            while (members.Length > 0 && await AssignOneAsync(topic, members, cancellationToken).ConfigureAwait(false))
            {
                // Each pass assigns the topic's oldest queued conversation, until none is queued or nobody can take one.
            }
        }
    }

    /// <summary>Removes support's counters whose window ended, the longest window being a day.</summary>
    internal async Task PruneAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset ended = clock.GetUtcNow() - TimeSpan.FromDays(1);
        MessagingContext context = database.Context();
        await using (context.ConfigureAwait(false))
        {
            await context.SupportCounters.Where(each => each.WindowStart < ended).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Whether <paramref name="topic"/> is open now: within its hours while a member is available.</summary>
    internal async Task<bool> IsOpenAsync(SupportTopic topic, CancellationToken cancellationToken)
    {
        if (!topic.IsWithinHours(clock.GetUtcNow()))
        {
            return false;
        }

        string[] members = [.. await MembersAsync(topic.Scope, cancellationToken).ConfigureAwait(false)];
        if (members.Length == 0)
        {
            return false;
        }

        MessagingContext context = database.Context();
        await using (context.ConfigureAwait(false))
        {
            return (await AvailableAsync(context, members, cancellationToken).ConfigureAwait(false)).Count > 0;
        }
    }

    /// <summary>The members of <paramref name="scope"/>, as the membership seam answers them now.</summary>
    internal async Task<HashSet<string>> MembersAsync(ConversationScope scope, CancellationToken cancellationToken)
    {
        AsyncServiceScope scoped = scopes.CreateAsyncScope();
        await using (scoped.ConfigureAwait(false))
        {
            HashSet<string> members = new(StringComparer.Ordinal);
            await foreach (string member in scoped.ServiceProvider.GetRequiredService<IConversationMembership>().MembersAsync(scope, cancellationToken).ConfigureAwait(false))
            {
                members.Add(member);
            }

            return members;
        }
    }

    /// <summary>Assigns <paramref name="conversation"/> to <paramref name="person"/> in the transaction, writing their participant row, and tells the processes.</summary>
    internal static async Task AssignToAsync(MessagingContext context, ConversationRow conversation, string person, DateTimeOffset now, CancellationToken cancellationToken)
    {
        bool queued = conversation.Assignee is null;
        conversation.Assignee = person;
        conversation.AssignedAt = now;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await context.Database.ExecuteSqlRawAsync(
            Join,
            [new NpgsqlParameter("id", Guid.CreateVersion7()), new NpgsqlParameter("conversation", conversation.Id), new NpgsqlParameter("person", person), new NpgsqlParameter("now", now)],
            cancellationToken).ConfigureAwait(false);
        await LiveNotices.TellAsync(context, LiveNotices.Assigned, conversation.Id, conversation.Id, cancellationToken).ConfigureAwait(false);
        await LiveNotices.TellAsync(context, LiveNotices.Conversation, conversation.Id, conversation.Id, cancellationToken).ConfigureAwait(false);
        if (queued)
        {
            await LiveNotices.TellQueueAsync(context, conversation.Topic!, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<bool> AssignOneAsync(SupportTopic topic, string[] members, CancellationToken cancellationToken)
    {
        MessagingOptions current = options.CurrentValue;
        return await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await DeliveryClaims.LockTimeoutAsync(context, current.Dispatch.Timeout, token).ConfigureAwait(false);
                ConversationRow? queued = (await context.Conversations.FromSqlRaw(Oldest, new NpgsqlParameter("topic", topic.Name)).ToListAsync(token).ConfigureAwait(false)).FirstOrDefault();
                if (queued is null)
                {
                    return false;
                }

                foreach (Candidate candidate in await AvailableAsync(context, members, token).ConfigureAwait(false))
                {
                    await AdvisoryLocks.TakeAsync(context, AdvisoryLocks.Person(hashes, candidate.Person), shared: true, token).ConfigureAwait(false);
                    SupportAgentRow? agent = (await context.SupportAgents.FromSqlRaw(LockAgent, new NpgsqlParameter("person", candidate.Person)).ToListAsync(token).ConfigureAwait(false)).FirstOrDefault();
                    int open = await context.Database.SqlQueryRaw<int>(OpenOf, new NpgsqlParameter("person", candidate.Person)).SingleAsync(token).ConfigureAwait(false);
                    if (agent is not { Available: true } || open >= settings.Support.Limit)
                    {
                        continue;
                    }

                    DateTimeOffset now = clock.GetUtcNow();
                    agent.LastAssignedAt = now;
                    await AssignToAsync(context, queued, candidate.Person, now, token).ConfigureAwait(false);
                    return true;
                }

                return false;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The available members of <paramref name="members"/>, the fewest open conversations first, then the longest since their last assignment.</summary>
    private async Task<List<Candidate>> AvailableAsync(MessagingContext context, string[] members, CancellationToken cancellationToken)
    {
        List<Candidate> candidates = await context.Database.SqlQueryRaw<Candidate>(
            Candidates,
            new NpgsqlParameter("members", members),
            new NpgsqlParameter("fresh", clock.GetUtcNow() - LivePresence.Lifetime)).ToListAsync(cancellationToken).ConfigureAwait(false);
        return
        [
            .. candidates
                .Where(each => each.Open < settings.Support.Limit)
                .OrderBy(each => each.Open)
                .ThenBy(each => each.LastAssignedAt ?? DateTimeOffset.MinValue)
                .ThenBy(each => each.Person, StringComparer.Ordinal),
        ];
    }

    private sealed class Candidate
    {
        public required string Person { get; init; }

        public int Open { get; init; }

        public DateTimeOffset? LastAssignedAt { get; init; }
    }
}