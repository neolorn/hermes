using System;

using Hermes.Operations;

namespace Hermes.Sending;

/// <summary>When a delivery answered transient is next due (<c>01</c> MSG-SEND-016).</summary>
internal static class RetrySchedule
{
    /// <summary>
    /// The instant the attempt after the <paramref name="attempt"/>th (from 1) is due:
    /// <c>Initial</c> times <c>Factor</c> to the power <c>attempt - 1</c>, capped at <c>Max</c>,
    /// increased by <paramref name="random"/> (from 0 to 1) of <c>Jitter</c> of it; or, where the
    /// provider asked for a later one, after <paramref name="retryAfter"/>, capped at <c>Max</c>.
    /// </summary>
    internal static DateTimeOffset Next(RetryOptions retry, int attempt, TimeSpan? retryAfter, DateTimeOffset now, double random)
    {
        ArgumentNullException.ThrowIfNull(retry);
        double grown = retry.Initial.Ticks * Math.Pow(retry.Factor, attempt - 1);
        TimeSpan delay = TimeSpan.FromTicks((long)Math.Min(grown, retry.Max.Ticks));
        delay += TimeSpan.FromTicks((long)(delay.Ticks * retry.Jitter * random));
        if (retryAfter is { } asked && asked > delay)
        {
            delay = asked < retry.Max ? asked : retry.Max;
        }

        return now + delay;
    }
}