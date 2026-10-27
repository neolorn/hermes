using System;
using System.Collections.Generic;

namespace Hermes.Sending;

/// <summary>
/// Judges the SMS balance's readings (<c>02</c> MSG-SMS-006): spend is the fall between two
/// readings, a rise being a top-up and not spend, and the <c>sms-balance</c> alert holds for a
/// drain or a low balance.
/// </summary>
internal static class BalanceJudgement
{
    internal const string Drain = "drain";

    internal const string Low = "low";

    /// <summary>The spend between consecutive readings of <paramref name="readings"/>, in instant order, whose later reading falls after <paramref name="from"/> and at or before <paramref name="to"/>.</summary>
    internal static decimal Spend(IReadOnlyList<(DateTimeOffset At, decimal Units)> readings, DateTimeOffset from, DateTimeOffset to)
    {
        decimal spend = 0;
        for (int index = 1; index < readings.Count; index++)
        {
            (DateTimeOffset at, decimal units) = readings[index];
            if (at > from && at <= to && readings[index - 1].Units > units)
            {
                spend += readings[index - 1].Units - units;
            }
        }

        return spend;
    }

    /// <summary>
    /// Which of the alert's conditions holds at <paramref name="now"/> for the successful
    /// readings <paramref name="readings"/>, in instant order: <see cref="Drain"/>, else
    /// <see cref="Low"/>, else none.
    /// </summary>
    internal static string? Which(IReadOnlyList<(DateTimeOffset At, decimal Units)> readings, DateTimeOffset now, BalanceRules sms)
    {
        if (readings.Count == 0)
        {
            return null;
        }

        TimeSpan window = sms.DrainWindow;
        decimal spend = Spend(readings, now - window, now);
        if (IsDrain(readings, now, spend, sms))
        {
            return Drain;
        }

        decimal balance = readings[^1].Units;
        if (balance <= sms.BalanceFloor)
        {
            return Low;
        }

        // At the last window's rate, the balance reaches the floor after (balance - floor) / spend windows.
        return spend > 0 && (double)(balance - sms.BalanceFloor) / (double)spend * window.TotalSeconds <= sms.FloorWarning.TotalSeconds ? Low : null;
    }

    /// <summary>
    /// Whether the window's spend is at least <c>DrainMinSpend</c> and exceeds <c>DrainFactor</c>
    /// times the mean spend per window over the trailing <c>DrainBaseline</c> before the window,
    /// judged only once the readings span <c>DrainMinHistory</c>.
    /// </summary>
    private static bool IsDrain(IReadOnlyList<(DateTimeOffset At, decimal Units)> readings, DateTimeOffset now, decimal spend, BalanceRules sms)
    {
        DateTimeOffset first = readings[0].At;
        if (readings[^1].At - first < sms.DrainMinHistory || spend < sms.DrainMinSpend)
        {
            return false;
        }

        DateTimeOffset end = now - sms.DrainWindow;
        DateTimeOffset start = now - sms.DrainBaseline > first ? now - sms.DrainBaseline : first;
        if (end <= start)
        {
            return false;
        }

        double windows = (end - start) / sms.DrainWindow;
        double mean = (double)Spend(readings, start, end) / windows;
        return (double)spend > sms.DrainFactor * mean;
    }
}