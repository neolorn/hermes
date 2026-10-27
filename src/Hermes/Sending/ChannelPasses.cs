using System;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

namespace Hermes.Sending;

/// <summary>
/// The hosted dispatcher's passes of one channel (<c>01</c> MSG-SEND-007): at most
/// <c>Hermes:Dispatch:MaxPasses</c> at once, a trigger that finds them all running starting the
/// next as soon as one ends; a pass that took its whole limit starts another at the next
/// second, and at once where it ends within the second it claimed in.
/// </summary>
internal sealed class ChannelPasses(Channel channel, DispatchPasses passes, Func<int> maxPasses, Action<Channel, Exception> failed, CancellationToken stopping) : IDisposable
{
    private readonly Lock gate = new();

    private int running;

    private bool pending;

    private TaskCompletionSource? idle;

    private ITimer? nextSecond;

    private DateTimeOffset? scheduled;

    /// <summary>Starts a pass, or marks one to start as soon as a running one ends.</summary>
    internal void Trigger()
    {
        lock (gate)
        {
            if (stopping.IsCancellationRequested)
            {
                return;
            }

            if (running >= maxPasses())
            {
                pending = true;
                return;
            }

            running++;
        }

        _ = Task.Run(RunAsync, CancellationToken.None);
    }

    /// <summary>Completes once no pass of the channel runs.</summary>
    internal Task IdleAsync()
    {
        lock (gate)
        {
            if (running == 0)
            {
                return Task.CompletedTask;
            }

            idle ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return idle.Task;
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            nextSecond?.Dispose();
            nextSecond = null;
        }
    }

    private async Task RunAsync()
    {
        DateTimeOffset? claimedIn = null;
        try
        {
            PassOutcome outcome = await passes.RunAsync(
                channel,
                (took, limit) =>
                {
                    claimedIn = DeliveryClaims.Second(passes.Clock.GetUtcNow());
                    if (limit > 0 && took == limit)
                    {
                        AtNextSecond(claimedIn.Value);
                    }
                },
                stopping).ConfigureAwait(false);
            if (outcome.TookAll && claimedIn == DeliveryClaims.Second(passes.Clock.GetUtcNow()))
            {
                Trigger();
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
        }
        catch (Exception failure) when (failure is not OperationCanceledException || !stopping.IsCancellationRequested)
        {
            failed(channel, failure);
        }
        finally
        {
            bool again;
            TaskCompletionSource? done = null;
            lock (gate)
            {
                running--;
                again = pending && !stopping.IsCancellationRequested;
                pending = false;
                if (running == 0)
                {
                    done = idle;
                    idle = null;
                }
            }

            done?.TrySetResult();
            if (again)
            {
                Trigger();
            }
        }
    }

    private void AtNextSecond(DateTimeOffset second)
    {
        DateTimeOffset next = second.AddSeconds(1);
        lock (gate)
        {
            if (scheduled == next || stopping.IsCancellationRequested)
            {
                return;
            }

            scheduled = next;
            nextSecond?.Dispose();
            TimeSpan wait = next - passes.Clock.GetUtcNow();
            nextSecond = passes.Clock.CreateTimer(_ => Trigger(), null, wait > TimeSpan.Zero ? wait : TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        }
    }
}