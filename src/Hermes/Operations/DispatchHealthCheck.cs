using System;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Sending;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Hermes.Operations;

/// <summary>
/// The health check <c>hermes.dispatch</c> (<c>06</c> MSG-OPS-005): unhealthy while the oldest
/// delivery <c>waiting</c> or <c>inflight</c> whose became-due instant has passed became due
/// longer ago than <c>Hermes:Dispatch:LagThreshold</c>, a row this release does not know left out.
/// </summary>
internal sealed class DispatchHealthCheck(MessagingDatabase database, Declarations declarations, IOptionsMonitor<MessagingOptions> options, TimeProvider clock) : IHealthCheck
{
    internal const string Name = "hermes.dispatch";

    private readonly string[] kinds = [.. declarations.Kinds.Select(kind => kind.Name).Concat(declarations.Retired).Distinct(StringComparer.Ordinal)];

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = clock.GetUtcNow();
        string[] channels = [.. Spellings.AllChannels];
        string[] reasons = [.. Spellings.AllReasons];
        DateTimeOffset? oldest;
        try
        {
            MessagingContext messaging = database.Context();
            await using (messaging.ConfigureAwait(false))
            {
                oldest = await messaging.Deliveries
                    .Where(delivery => (delivery.Status == "waiting" || delivery.Status == "inflight")
                        && delivery.BecameDueAt <= now
                        && kinds.Contains(delivery.Kind)
                        && channels.Contains(delivery.Channel)
                        && (delivery.Reason == null || reasons.Contains(delivery.Reason)))
                    .MinAsync(delivery => (DateTimeOffset?)delivery.BecameDueAt, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (DbException)
        {
            return HealthCheckResult.Unhealthy("The deliveries could not be read.");
        }

        return oldest is { } due && now - due > options.CurrentValue.Dispatch.LagThreshold
            ? HealthCheckResult.Unhealthy("A due delivery has waited longer than Hermes:Dispatch:LagThreshold.")
            : HealthCheckResult.Healthy();
    }
}