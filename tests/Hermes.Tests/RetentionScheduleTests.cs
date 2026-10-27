using System;
using System.Globalization;

using Hermes.Sending;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "unit")]
public sealed class RetentionScheduleTests
{
    private static readonly TimeZoneInfo NewYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    [Theory]
    [InlineData("2026-10-27T09:00:00Z", "03:00", "2026-10-28T07:00:00Z")]
    [InlineData("2026-10-28T07:00:00Z", "03:00", "2026-10-29T07:00:00Z")]
    [InlineData("2026-03-08T05:00:00Z", "02:30", "2026-03-08T07:00:00Z")]
    [InlineData("2026-11-01T04:00:00Z", "03:00", "2026-11-01T08:00:00Z")]
    public void Next_TheFirstInstantAfterNowAtTheLocalTime_TakesATimeTheZoneSkipsAtTheFirstTimeAfterIt(string now, string at, string next)
    {
        Assert.Equal(Instant(next), RetentionJob.Next(Instant(now), TimeOnly.Parse(at, CultureInfo.InvariantCulture), NewYork));
    }

    [Theory]
    [InlineData("2026-10-28T06:59:59Z", "03:00", "2026-10-27T07:00:00Z")]
    [InlineData("2026-10-28T07:00:00Z", "03:00", "2026-10-28T07:00:00Z")]
    [InlineData("2026-03-08T08:00:00Z", "02:30", "2026-03-08T07:00:00Z")]
    public void Last_TheLastInstantAtOrBeforeNowAtTheLocalTime(string now, string at, string last)
    {
        Assert.Equal(Instant(last), RetentionJob.Last(Instant(now), TimeOnly.Parse(at, CultureInfo.InvariantCulture), NewYork));
    }

    private static DateTimeOffset Instant(string text)
    {
        return DateTimeOffset.Parse(text, CultureInfo.InvariantCulture);
    }
}