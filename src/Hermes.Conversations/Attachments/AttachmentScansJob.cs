using System;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Operations;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hermes.Conversations.Attachments;

/// <summary>
/// The <c>attachment-scans</c> job (<c>09</c> MSG-CONV-007; <c>06</c> MSG-OPS-004): where
/// <c>Hermes:Jobs:Enabled</c> is on and attachments are enabled, every 30 seconds it takes the
/// job's claim and runs one pass of <see cref="AttachmentWork"/>, renewing the claim between
/// units, in a scope of its own.
/// </summary>
internal sealed partial class AttachmentScansJob(
    IServiceScopeFactory scopes,
    MessagingDatabase database,
    ConversationsSettings settings,
    IOptionsMonitor<MessagingOptions> options,
    TimeProvider clock,
    ILogger<AttachmentScansJob> logger) : BackgroundService
{
    private PeriodicTimer? timer;

    /// <summary>Starts the timer before the loop, so the first run falls one interval after the start; the package's checks have read the settings by then.</summary>
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.CurrentValue.Jobs.Enabled || !settings.Attachments.Enabled)
        {
            return Task.CompletedTask;
        }

        timer = new PeriodicTimer(JobRow.AttachmentScansInterval, clock);
        return base.StartAsync(cancellationToken);
    }

    public override void Dispose()
    {
        timer?.Dispose();
        base.Dispose();
    }

    /// <summary>Takes the claim and runs one pass, answering whether it held the claim to the end.</summary>
    internal async Task<bool> RunAsync(CancellationToken cancellationToken)
    {
        MessagingOptions current = options.CurrentValue;
        Guid holder = Guid.CreateVersion7();
        if (!await JobClaims.ClaimAsync(database, JobRow.AttachmentScans, holder, JobRow.AttachmentScansInterval, current.Jobs.ClaimTimeout, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        bool held = true;
        AsyncServiceScope scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            await scope.ServiceProvider.GetRequiredService<AttachmentWork>().PassAsync(
                clock.GetUtcNow(),
                async token => held = await JobClaims.RenewAsync(database, JobRow.AttachmentScans, holder, options.CurrentValue.Jobs.ClaimTimeout, clock.GetUtcNow(), token).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);
        }

        if (held)
        {
            await JobClaims.ReleaseAsync(database, JobRow.AttachmentScans, holder, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        }

        return held;
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

    [LoggerMessage(EventId = 81, EventName = "AttachmentScansFailed", Level = LogLevel.Error, Message = "A pass of the attachment scans failed with {Failure}.")]
    private static partial void RunFailed(ILogger logger, string failure);
}