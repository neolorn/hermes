using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace Hermes.Operations;

/// <summary>
/// A job's claim on its row of the job table (<c>06</c> MSG-OPS-004): taken where the row is
/// free or its claim expired and its last start is at least the interval, less one second,
/// before <c>@now</c>; renewed after each unit of work while it is still the holder's; and
/// released with the instant the run completed.
/// </summary>
internal static class JobClaims
{
    // EF Core cannot express an insert whose conflict updates under a condition.
    private const string Claim = """
        INSERT INTO messaging.jobs AS j (name, claimed_by, claimed_at, claim_expires_at, first_started_at, last_started_at)
        VALUES (@name, @holder, @now, @now + @claimTimeout, @now, @now)
        ON CONFLICT (name) DO UPDATE
        SET claimed_by = excluded.claimed_by, claimed_at = excluded.claimed_at, claim_expires_at = excluded.claim_expires_at,
            first_started_at = COALESCE(j.first_started_at, excluded.first_started_at), last_started_at = excluded.last_started_at
        WHERE (j.claimed_by IS NULL OR j.claim_expires_at <= @now)
          AND (j.last_started_at IS NULL OR j.last_started_at <= @now - @interval + interval '1 second')
        """;

    /// <summary>Takes the claim of the job <paramref name="name"/> for <paramref name="holder"/>, answering whether it did.</summary>
    internal static async Task<bool> ClaimAsync(MessagingDatabase database, string name, Guid holder, TimeSpan interval, TimeSpan claimTimeout, DateTimeOffset now, CancellationToken cancellationToken)
    {
        MessagingContext context = database.Context();
        await using (context.ConfigureAwait(false))
        {
            int taken = await context.Database.ExecuteSqlRawAsync(
                Claim,
                [
                    new NpgsqlParameter("name", name),
                    new NpgsqlParameter("holder", holder),
                    new NpgsqlParameter("now", now),
                    new NpgsqlParameter("claimTimeout", claimTimeout),
                    new NpgsqlParameter("interval", interval),
                ],
                cancellationToken).ConfigureAwait(false);
            return taken == 1;
        }
    }

    /// <summary>Renews the claim with a new expiry, answering whether it was still <paramref name="holder"/>'s.</summary>
    internal static async Task<bool> RenewAsync(MessagingDatabase database, string name, Guid holder, TimeSpan claimTimeout, DateTimeOffset now, CancellationToken cancellationToken)
    {
        MessagingContext context = database.Context();
        await using (context.ConfigureAwait(false))
        {
            int renewed = await context.Jobs
                .Where(job => job.Name == name && job.ClaimedBy == holder)
                .ExecuteUpdateAsync(set => set.SetProperty(job => job.ClaimExpiresAt, now + claimTimeout).SetProperty(job => job.LastRenewedAt, now), cancellationToken)
                .ConfigureAwait(false);
            return renewed == 1;
        }
    }

    /// <summary>Releases the claim, where it is still <paramref name="holder"/>'s, with the instant of completion.</summary>
    internal static async Task ReleaseAsync(MessagingDatabase database, string name, Guid holder, DateTimeOffset now, CancellationToken cancellationToken)
    {
        MessagingContext context = database.Context();
        await using (context.ConfigureAwait(false))
        {
            await context.Jobs
                .Where(job => job.Name == name && job.ClaimedBy == holder)
                .ExecuteUpdateAsync(
                    set => set
                        .SetProperty(job => job.ClaimedBy, (Guid?)null)
                        .SetProperty(job => job.ClaimedAt, (DateTimeOffset?)null)
                        .SetProperty(job => job.ClaimExpiresAt, (DateTimeOffset?)null)
                        .SetProperty(job => job.LastCompletedAt, now),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }
}