using System;

using Hermes.Operations;

namespace Hermes.Sending;

/// <summary>
/// Quiet hours (<c>01</c> MSG-SEND-013): from <c>Hermes:QuietHours:Start</c> to
/// <c>Hermes:QuietHours:End</c> in local wall-clock time, a window that crosses midnight
/// included; a start or end in a daylight-saving gap is the first instant after it, one in an
/// overlap its first occurrence, and a start equal to the end turns them off.
/// </summary>
internal static class QuietHours
{
    /// <summary>The instant the quiet hours that hold at <paramref name="now"/> in <paramref name="zone"/> end, or none where they do not hold.</summary>
    internal static DateTimeOffset? End(QuietHoursOptions options, TimeZoneInfo zone, DateTimeOffset now)
    {
        if (options.Start == options.End)
        {
            return null;
        }

        DateTime today = TimeZoneInfo.ConvertTime(now, zone).DateTime.Date;
        foreach (DateTime day in (ReadOnlySpan<DateTime>)[today.AddDays(-1), today])
        {
            DateTimeOffset start = Instant(day + options.Start.ToTimeSpan(), zone);
            DateTime endDay = options.Start < options.End ? day : day.AddDays(1);
            DateTimeOffset end = Instant(endDay + options.End.ToTimeSpan(), zone);
            if (now >= start && now < end)
            {
                return end;
            }
        }

        return null;
    }

    /// <summary>The instant of the wall-clock time <paramref name="local"/> in <paramref name="zone"/>: in a gap, the first instant after it; in an overlap, its first occurrence.</summary>
    internal static DateTimeOffset Instant(DateTime local, TimeZoneInfo zone)
    {
        DateTime wall = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(wall))
        {
            DateTime after = wall;
            while (zone.IsInvalidTime(after))
            {
                after = after.AddMinutes(1);
            }

            after = after.AddTicks(-(after.Ticks % TimeSpan.TicksPerMinute));
            return new DateTimeOffset(after, zone.GetUtcOffset(after)).ToUniversalTime();
        }

        if (zone.IsAmbiguousTime(wall))
        {
            TimeSpan[] offsets = zone.GetAmbiguousTimeOffsets(wall);
            TimeSpan first = offsets[0] > offsets[1] ? offsets[0] : offsets[1];
            return new DateTimeOffset(wall, first).ToUniversalTime();
        }

        return new DateTimeOffset(wall, zone.GetUtcOffset(wall)).ToUniversalTime();
    }
}