using System;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Conversations.Chats;
using Hermes.Operations;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hermes.Conversations.Support;

/// <summary>
/// The <c>conversation-timers</c> job (<c>09</c> MSG-CONV-005; <c>06</c> MSG-OPS-004): where
/// <c>Hermes:Jobs:Enabled</c> is on, every 30 seconds it takes the job's claim and runs one pass
/// of support's timers, renewing the claim between units, in a scope of its own.
/// </summary>
internal sealed partial class ConversationTimersJob(
    IServiceScopeFactory scopes,
    MessagingDatabase database,
    IOptionsMonitor<MessagingOptions> options,
    TimeProvider clock,
    ILogger<ConversationTimersJob> logger) : BackgroundService
{
    private PeriodicTimer? timer;

    /// <summary>Starts the timer before the loop, so the first run falls one interval after the start.</summary>
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.CurrentValue.Jobs.Enabled)
        {
            return Task.CompletedTask;
        }

        timer = new PeriodicTimer(JobRow.ConversationTimersInterval, clock);
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
        if (!await JobClaims.ClaimAsync(database, JobRow.ConversationTimers, holder, JobRow.ConversationTimersInterval, current.Jobs.ClaimTimeout, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        bool held = true;
        AsyncServiceScope scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            await scope.ServiceProvider.GetRequiredService<StaffChats>().TimersAsync(
                clock.GetUtcNow(),
                async token => held = await JobClaims.RenewAsync(database, JobRow.ConversationTimers, holder, options.CurrentValue.Jobs.ClaimTimeout, clock.GetUtcNow(), token).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);
        }

        if (held)
        {
            await JobClaims.ReleaseAsync(database, JobRow.ConversationTimers, holder, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
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

    [LoggerMessage(EventId = 78, EventName = "ConversationTimersFailed", Level = LogLevel.Error, Message = "A pass of the conversation timers failed with {Failure}.")]
    private static partial void RunFailed(ILogger logger, string failure);
}