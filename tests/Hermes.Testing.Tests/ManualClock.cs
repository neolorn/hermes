using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Hermes.Testing.Tests;

/// <summary>A clock a test sets and moves; its timers fire only when it moves past their due instant.</summary>
internal sealed class ManualClock(DateTimeOffset start) : TimeProvider
{
    private readonly Lock gate = new();

    private readonly List<ManualTimer> timers = [];

    private DateTimeOffset now = start;

    public override DateTimeOffset GetUtcNow()
    {
        lock (gate)
        {
            return now;
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ManualTimer timer = new(this, callback, state);
        lock (gate)
        {
            timers.Add(timer);
        }

        timer.Change(dueTime, period);
        return timer;
    }

    /// <summary>Moves the clock on by <paramref name="by"/>, firing each timer as its due instants pass.</summary>
    internal void Advance(TimeSpan by)
    {
        DateTimeOffset until;
        lock (gate)
        {
            until = now + by;
        }

        while (true)
        {
            ManualTimer? next;
            lock (gate)
            {
                next = timers.Where(timer => timer.Due is { } due && due <= until).MinBy(timer => timer.Due);
                if (next is null)
                {
                    now = until;
                    return;
                }

                now = next.Due!.Value;
                next.Advance();
            }

            next.Fire();
        }
    }

    private sealed class ManualTimer(ManualClock clock, TimerCallback callback, object? state) : ITimer
    {
        private TimeSpan period = Timeout.InfiniteTimeSpan;

        internal DateTimeOffset? Due { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (clock.gate)
            {
                this.period = period;
                Due = dueTime == Timeout.InfiniteTimeSpan ? null : clock.now + dueTime;
            }

            return true;
        }

        public void Dispose()
        {
            lock (clock.gate)
            {
                Due = null;
                clock.timers.Remove(this);
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        internal void Advance()
        {
            Due = period == Timeout.InfiniteTimeSpan || period == TimeSpan.Zero ? null : Due + period;
        }

        internal void Fire()
        {
            callback(state);
        }
    }
}