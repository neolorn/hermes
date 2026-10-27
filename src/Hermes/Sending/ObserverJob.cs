using System;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Operations;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hermes.Sending;

/// <summary>
/// The <c>observer</c> job (<c>06</c> MSG-OPS-004; <c>01</c> MSG-SEND-017): where
/// <c>Hermes:Dispatch:Enabled</c> is on, every <c>Hermes:Dispatch:Interval</c> it takes the job's
/// claim, tells the observer of the pending notices, renewing the claim after each, and releases
/// it; where <c>Hermes:Observer:Enabled</c> is off, a run completes at once, telling nothing.
/// </summary>
internal sealed partial class ObserverJob(
    MessagingDatabase database,
    NoticeDelivery notices,
    IOptionsMonitor<MessagingOptions> options,
    TimeProvider clock,
    ILogger<ObserverJob> logger) : BackgroundService
{
    internal const string Name = "observer";

    private PeriodicTimer? timer;

    /// <summary>Starts the timer before the loop, which the host runs apart, so the first run falls one interval after the start.</summary>
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.CurrentValue.Dispatch.Enabled)
        {
            return Task.CompletedTask;
        }

        timer = new PeriodicTimer(options.CurrentValue.Dispatch.Interval, clock);
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
        if (!await JobClaims.ClaimAsync(database, Name, holder, current.Dispatch.Interval, current.Jobs.ClaimTimeout, clock.GetUtcNow(), stoppingToken).ConfigureAwait(false))
        {
            return;
        }

        if (current.Observer.Enabled)
        {
            await notices.DeliverAsync(
                token => JobClaims.RenewAsync(database, Name, holder, options.CurrentValue.Jobs.ClaimTimeout, clock.GetUtcNow(), token),
                stoppingToken).ConfigureAwait(false);
        }

        await JobClaims.ReleaseAsync(database, Name, holder, clock.GetUtcNow(), stoppingToken).ConfigureAwait(false);
    }

    [LoggerMessage(EventId = 44, EventName = "ObserverJobFailed", Level = LogLevel.Error, Message = "A run of the observer job failed with {Exception}; it runs again at its next interval.")]
    private static partial void RunFailed(ILogger logger, string exception);
}