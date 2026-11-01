using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Conversations.Chats;
using Hermes.Operations;
using Hermes.Sending;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Hermes.Conversations.Live;

/// <summary>
/// Presence (<c>09</c> MSG-CONV-006): a person is online while they hold a staff-hub
/// connection on any process, as a fresh row of <c>connections</c>. Each process refreshes its
/// rows every 30 seconds, and a row older than 90 is gone; when a person goes online or
/// offline, the processes are told for each direct conversation the person is in.
/// </summary>
internal sealed class LivePresence(
    MessagingDatabase database,
    IOptionsMonitor<MessagingOptions> options,
    KeyedHashes hashes,
    LiveConnections connections,
    TimeProvider clock)
{
    /// <summary>How long a connection's row is fresh after its process last saw it.</summary>
    internal static TimeSpan Lifetime { get; } = TimeSpan.FromSeconds(90);

    /// <summary>How often a process refreshes its rows.</summary>
    internal static TimeSpan Refresh { get; } = TimeSpan.FromSeconds(30);

    private const string Stale = "DELETE FROM messaging.connections WHERE last_seen_at < @fresh RETURNING person AS \"Value\"";

    private const string Seen = "UPDATE messaging.connections SET last_seen_at = @now WHERE process = @process";

    /// <summary>Records <paramref name="connection"/> of <paramref name="person"/>, telling the processes where the person was offline.</summary>
    internal async Task ConnectAsync(string connection, string person, CancellationToken cancellationToken)
    {
        Guid row = Guid.CreateVersion7();
        MessagingOptions current = options.CurrentValue;
        await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockAsync(context, current, person, token).ConfigureAwait(false);
                DateTimeOffset now = clock.GetUtcNow();
                bool online = await OnlineAsync(context, person, now, token).ConfigureAwait(false);
                context.Connections.Add(new ConnectionRow { Id = row, Person = person, Process = connections.Process, LastSeenAt = now });
                await context.SaveChangesAsync(token).ConfigureAwait(false);
                if (!online)
                {
                    await TellAsync(context, person, token).ConfigureAwait(false);
                }

                return true;
            },
            cancellationToken).ConfigureAwait(false);
        connections.Add(connection, person, row);
    }

    /// <summary>Removes <paramref name="connection"/>, telling the processes where its person is offline now.</summary>
    internal async Task DisconnectAsync(string connection, CancellationToken cancellationToken)
    {
        if (connections.Remove(connection) is not { } removed)
        {
            return;
        }

        MessagingOptions current = options.CurrentValue;
        await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockAsync(context, current, removed.Person, token).ConfigureAwait(false);
                await context.Connections.Where(each => each.Id == removed.Row).ExecuteDeleteAsync(token).ConfigureAwait(false);
                if (!await OnlineAsync(context, removed.Person, clock.GetUtcNow(), token).ConfigureAwait(false))
                {
                    await TellAsync(context, removed.Person, token).ConfigureAwait(false);
                }

                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Refreshes this process's rows and removes every row past its lifetime, telling the processes of each person offline now.</summary>
    internal async Task RefreshAsync(CancellationToken cancellationToken)
    {
        MessagingOptions current = options.CurrentValue;
        await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                DateTimeOffset now = clock.GetUtcNow();
                await context.Database.ExecuteSqlRawAsync(Seen, [new NpgsqlParameter("now", now), new NpgsqlParameter("process", connections.Process)], token).ConfigureAwait(false);
                List<string> gone = await context.Database.SqlQueryRaw<string>(Stale, new NpgsqlParameter("fresh", now - Lifetime)).ToListAsync(token).ConfigureAwait(false);
                foreach (string person in gone.Distinct(StringComparer.Ordinal))
                {
                    if (!await OnlineAsync(context, person, now, token).ConfigureAwait(false))
                    {
                        await TellAsync(context, person, token).ConfigureAwait(false);
                    }
                }

                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Whether <paramref name="person"/> holds a fresh row at <paramref name="now"/>.</summary>
    internal static Task<bool> OnlineAsync(MessagingContext context, string person, DateTimeOffset now, CancellationToken cancellationToken)
    {
        DateTimeOffset fresh = now - Lifetime;
        return context.Connections.AnyAsync(each => each.Person == person && each.LastSeenAt >= fresh, cancellationToken);
    }

    /// <summary>Tells the processes of <paramref name="person"/>'s presence in each direct conversation they are in.</summary>
    private static async Task TellAsync(MessagingContext context, string person, CancellationToken cancellationToken)
    {
        string direct = ConversationSpellings.Of(Core.ConversationType.Direct);
        var rows = await context.Participants.AsNoTracking()
            .Where(each => each.Person == person && each.LeftAt == null)
            .Join(context.Conversations.Where(each => each.Type == direct), participant => participant.ConversationId, conversation => conversation.Id, (participant, _) => new { participant.ConversationId, participant.Id })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var row in rows)
        {
            await LiveNotices.TellAsync(context, LiveNotices.Presence, row.ConversationId, row.Id, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task LockAsync(MessagingContext context, MessagingOptions current, string person, CancellationToken cancellationToken)
    {
        await DeliveryClaims.LockTimeoutAsync(context, current.Dispatch.Timeout, cancellationToken).ConfigureAwait(false);
        await AdvisoryLocks.TakeAsync(context, AdvisoryLocks.Person(hashes, person), shared: true, cancellationToken).ConfigureAwait(false);
    }
}