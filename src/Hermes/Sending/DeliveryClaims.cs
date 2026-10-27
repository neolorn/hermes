using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Operations;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace Hermes.Sending;

/// <summary>
/// The statements of a pass (<c>01</c> MSG-SEND-007) that EF Core cannot express: the claim,
/// the rate counter's row and its lock, a delivery's row lock and the hand-back.
/// </summary>
internal static class DeliveryClaims
{
    private const string Claim = """
        WITH due AS MATERIALIZED (
          SELECT id FROM messaging.deliveries
          WHERE kind = ANY(@kinds) AND channel = @channel
            AND (reason IS NULL OR reason = ANY(@reasons))
            AND ((status = 'waiting' AND due_at <= @now)
              OR (status = 'inflight' AND claim_expires_at <= @now))
          ORDER BY due_at LIMIT @limit
          FOR UPDATE SKIP LOCKED)
        UPDATE messaging.deliveries d
        SET status = 'inflight', claimed_by = @holder, claimed_at = @now,
            claim_expires_at = @now + @claimTimeout
        FROM due WHERE d.id = due.id
        RETURNING d.*
        """;

    private const string EnsureCounter = """
        INSERT INTO messaging.rate_counters (channel, second, count) VALUES (@channel, @second, 0)
        ON CONFLICT (channel, second) DO NOTHING
        """;

    private const string LockCounter = """
        SELECT count AS "Value" FROM messaging.rate_counters WHERE channel = @channel AND second = @second FOR UPDATE
        """;

    private const string LockDelivery = "SELECT * FROM messaging.deliveries WHERE id = @id FOR UPDATE";

    private const string HandBack = """
        UPDATE messaging.deliveries
        SET status = 'waiting', due_at = @now, claimed_by = NULL, claimed_at = NULL, claim_expires_at = NULL
        WHERE id = ANY(@ids) AND status = 'inflight' AND claimed_by = @holder AND claim_expires_at > @now
        RETURNING id AS "Value"
        """;

    /// <summary>The second of <paramref name="now"/> a claim takes its places in.</summary>
    internal static DateTimeOffset Second(DateTimeOffset now)
    {
        return new DateTimeOffset(now.UtcTicks - (now.UtcTicks % TimeSpan.TicksPerSecond), TimeSpan.Zero);
    }

    /// <summary>Sets the transaction's <c>lock_timeout</c> to <paramref name="timeout"/>, in whole milliseconds rounded up.</summary>
    internal static Task LockTimeoutAsync(MessagingContext context, TimeSpan timeout, CancellationToken cancellationToken)
    {
        long milliseconds = Math.Max(1, (long)Math.Ceiling(timeout.TotalMilliseconds));
        return context.Database.ExecuteSqlRawAsync(
            "SELECT set_config('lock_timeout', @timeout, true)",
            [new NpgsqlParameter("timeout", milliseconds.ToString(CultureInfo.InvariantCulture))],
            cancellationToken);
    }

    /// <summary>Locks the channel's counter row of <paramref name="second"/>, made at zero where there is none, and answers its count.</summary>
    internal static async Task<int> CounterAsync(MessagingContext context, string channel, DateTimeOffset second, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlRawAsync(EnsureCounter, [new NpgsqlParameter("channel", channel), new NpgsqlParameter("second", second)], cancellationToken).ConfigureAwait(false);
        List<int> count = await context.Database
            .SqlQueryRaw<int>(LockCounter, new NpgsqlParameter("channel", channel), new NpgsqlParameter("second", second))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return count[0];
    }

    /// <summary>Adds <paramref name="places"/> to the counter row of <paramref name="second"/>, a negative number giving places back, never below zero.</summary>
    internal static Task<int> CountAsync(MessagingContext context, string channel, DateTimeOffset second, int places, CancellationToken cancellationToken)
    {
        return context.RateCounters
            .Where(counter => counter.Channel == channel && counter.Second == second)
            .ExecuteUpdateAsync(set => set.SetProperty(counter => counter.Count, counter => counter.Count + places > 0 ? counter.Count + places : 0), cancellationToken);
    }

    /// <summary>Claims up to <paramref name="limit"/> due deliveries of <paramref name="kinds"/> on <paramref name="channel"/> for <paramref name="holder"/>.</summary>
    internal static Task<List<DeliveryRow>> ClaimAsync(
        MessagingContext context,
        string channel,
        IReadOnlyList<string> kinds,
        int limit,
        Guid holder,
        TimeSpan claimTimeout,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return context.Deliveries
            .FromSqlRaw(
                Claim,
                new NpgsqlParameter("kinds", kinds.ToArray()),
                new NpgsqlParameter("channel", channel),
                new NpgsqlParameter("reasons", Spellings.AllReasons.ToArray()),
                new NpgsqlParameter("now", now),
                new NpgsqlParameter("limit", limit),
                new NpgsqlParameter("holder", holder),
                new NpgsqlParameter("claimTimeout", claimTimeout))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    /// <summary>The delivery's row, locked for the rest of the transaction and tracked, or none.</summary>
    internal static async Task<DeliveryRow?> LockAsync(MessagingContext context, Guid delivery, CancellationToken cancellationToken)
    {
        List<DeliveryRow> rows = await context.Deliveries.FromSqlRaw(LockDelivery, new NpgsqlParameter("id", delivery)).ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.Count == 0 ? null : rows[0];
    }

    /// <summary>Returns each of <paramref name="deliveries"/> still under <paramref name="holder"/>'s claim to <c>waiting</c>, due at once, answering those it returned.</summary>
    internal static Task<List<Guid>> HandBackAsync(MessagingContext context, IReadOnlyList<Guid> deliveries, Guid holder, DateTimeOffset now, CancellationToken cancellationToken)
    {
        return context.Database
            .SqlQueryRaw<Guid>(HandBack, new NpgsqlParameter("ids", deliveries.ToArray()), new NpgsqlParameter("holder", holder), new NpgsqlParameter("now", now))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Whether <paramref name="row"/> is still <c>inflight</c> under <paramref name="holder"/>'s claim, not yet expired.</summary>
    internal static bool IsHeld(DeliveryRow row, Guid holder, DateTimeOffset now)
    {
        return row.Status == "inflight" && row.ClaimedBy == holder && row.ClaimExpiresAt > now;
    }

    /// <summary>Clears the claim of <paramref name="row"/>.</summary>
    internal static void Release(DeliveryRow row)
    {
        row.ClaimedBy = null;
        row.ClaimedAt = null;
        row.ClaimExpiresAt = null;
    }
}