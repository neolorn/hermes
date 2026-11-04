using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hermes.Operations;

/// <summary>
/// The key-table timer every process that registers the engine runs, whether or not it runs
/// jobs: it writes the write versions' last-made instant and the token lifetime every
/// <c>Hermes:Dispatch:Interval</c> and when the process stops (<c>06</c> MSG-OPS-008).
/// </summary>
internal sealed partial class KeyTableTimer(KeyTable table, TimerChecks checks, IOptionsMonitor<MessagingOptions> options, TimeProvider clock, ILogger<KeyTableTimer> logger) : BackgroundService
{
    private PeriodicTimer? timer;

    /// <summary>
    /// The first stop's task, kept from its call: set atomically by that call, so that two stops
    /// racing write once between them, and returned to every later call.
    /// </summary>
    private TaskCompletionSource<Task>? stopping;

    /// <summary>Starts the timer before the loop, which the host runs apart, so the first tick falls one interval after the start.</summary>
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        timer = new PeriodicTimer(options.CurrentValue.Dispatch.Interval, clock);
        return base.StartAsync(cancellationToken);
    }

    /// <summary>
    /// Stops the loop and waits for a tick in flight to end, whatever the host's token says, so
    /// that no tick runs on while the host disposes the data source; the tick honours its
    /// stopping token, so the wait is short. Then writes once more, unless the host's time to
    /// stop is already over. Only the first call does so: a host may stop its hosted services
    /// more than once, concurrently, and dispose the data source once a stop returns, so every
    /// later call writes nothing and returns the first call's task, whatever its own token
    /// says, so that no stop returns before the first call's write has ended (M-041, M-042).
    /// </summary>
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        TaskCompletionSource<Task> first = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<Task>? earlier = Interlocked.CompareExchange(ref stopping, first, null);
        if (earlier is not null)
        {
            return earlier.Task.Unwrap();
        }

        Task stop = StopOnceAsync(cancellationToken);
        first.SetResult(stop);
        return stop;
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
            TimeSpan interval = options.CurrentValue.Dispatch.Interval;
            if (timer.Period != interval)
            {
                timer.Period = interval;
            }

            try
            {
                await checks.RunAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (DbException failure)
            {
                ChecksFailed(failure);
            }

            await WriteAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    /// <summary>The first stop's work: the loop stopped, a tick in flight ended, and the one write.</summary>
    private async Task StopOnceAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(CancellationToken.None).ConfigureAwait(false);
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            await WriteAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The host's time to stop ran out during the write; the next process's timer writes.
        }
    }

    private async Task WriteAsync(CancellationToken cancellationToken)
    {
        try
        {
            await table.WriteMadeAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbException failure)
        {
            WriteFailed(failure);
        }
    }

    [LoggerMessage(EventId = 10, EventName = "KeyTableWriteFailed", Level = LogLevel.Warning, Message = "The key table's last-made instants were not written; the next interval writes them.")]
    private partial void WriteFailed(Exception failure);

    [LoggerMessage(EventId = 11, EventName = "TimerChecksFailed", Level = LogLevel.Warning, Message = "The timer's alert conditions were not judged; the next interval judges them.")]
    private partial void ChecksFailed(Exception failure);
}