using System;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Operations;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Hermes.Sending;

/// <summary>
/// The <c>subscriptions</c> job (<c>06</c> MSG-OPS-004; <c>02</c> MSG-PUSH-006): where
/// <c>Hermes:Jobs:Enabled</c> is on, every <c>Hermes:Jobs:SubscriptionsInterval</c> it takes the
/// job's claim and removes the subscriptions past their expiry in batches of
/// <c>Hermes:Jobs:BatchSize</c>, each selecting its rows <c>FOR UPDATE SKIP LOCKED</c> and
/// renewing the claim after it.
/// </summary>
internal sealed partial class SubscriptionsJob(
    MessagingDatabase database,
    IOptionsMonitor<MessagingOptions> options,
    TimeProvider clock,
    ILogger<SubscriptionsJob> logger) : BackgroundService
{
    internal const string Name = "subscriptions";

    private const string RemoveExpired = """
        DELETE FROM messaging.subscriptions WHERE id IN (
            SELECT id FROM messaging.subscriptions WHERE expires_at <= @now
            ORDER BY expires_at LIMIT @batch FOR UPDATE SKIP LOCKED)
        """;

    private PeriodicTimer? timer;

    /// <summary>Starts the timer before the loop, so the first run falls one interval after the start.</summary>
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.CurrentValue.Jobs.Enabled)
        {
            return Task.CompletedTask;
        }

        timer = new PeriodicTimer(options.CurrentValue.Jobs.SubscriptionsInterval, clock);
        return base.StartAsync(cancellationToken);
    }

    public override void Dispose()
    {
        timer?.Dispose();
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        PeriodicTimer timer = this.timer!;
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await RunAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception failure) when (failure is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                RunFailed(logger, failure.GetType().Name);
            }
        }
    }

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        MessagingOptions current = options.CurrentValue;
        Guid holder = Guid.CreateVersion7();
        if (!await JobClaims.ClaimAsync(database, Name, holder, current.Jobs.SubscriptionsInterval, current.Jobs.ClaimTimeout, clock.GetUtcNow(), stoppingToken).ConfigureAwait(false))
        {
            return;
        }

        int removed;
        int total = 0;
        do
        {
            removed = await LibraryTransactions.RunAsync(
                database,
                current,
                (context, token) => context.Database.ExecuteSqlRawAsync(RemoveExpired, [new NpgsqlParameter("now", clock.GetUtcNow()), new NpgsqlParameter("batch", current.Jobs.BatchSize)], token),
                stoppingToken).ConfigureAwait(false);
            total += removed;
            if (!await JobClaims.RenewAsync(database, Name, holder, options.CurrentValue.Jobs.ClaimTimeout, clock.GetUtcNow(), stoppingToken).ConfigureAwait(false))
            {
                return;
            }
        }
        while (removed == current.Jobs.BatchSize);

        Removed(logger, total);
        await JobClaims.ReleaseAsync(database, Name, holder, clock.GetUtcNow(), stoppingToken).ConfigureAwait(false);
    }

    [LoggerMessage(EventId = 55, EventName = "SubscriptionsRemoved", Level = LogLevel.Information, Message = "The subscriptions job removed {Count} subscriptions past their expiry.")]
    private static partial void Removed(ILogger logger, int count);

    [LoggerMessage(EventId = 56, EventName = "SubscriptionsJobFailed", Level = LogLevel.Error, Message = "A run of the subscriptions job failed with {Exception}; it runs again at its next interval.")]
    private static partial void RunFailed(ILogger logger, string exception);
}