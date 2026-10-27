using System;

using Hermes.Operations;
using Hermes.Sending;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "unit")]
public sealed class QuietHoursRuleTests
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    [Theory]
    [InlineData("2026-10-27T20:59:59Z", null)]
    [InlineData("2026-10-27T21:00:00Z", "2026-10-28T09:00:00Z")]
    [InlineData("2026-10-28T03:00:00Z", "2026-10-28T09:00:00Z")]
    [InlineData("2026-10-28T09:00:00Z", null)]
    public void End_AWindowThatCrossesMidnight_HoldsFromItsStartToItsEnd(string now, string? end)
    {
        DateTimeOffset? found = QuietHours.End(new QuietHoursOptions(), TimeZoneInfo.Utc, DateTimeOffset.Parse(now, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(end is null ? null : DateTimeOffset.Parse(end, System.Globalization.CultureInfo.InvariantCulture), found);
    }

    [Fact]
    public void End_AWindowWithinOneDay_HoldsOnlyInsideIt()
    {
        QuietHoursOptions options = new() { Start = new TimeOnly(13, 0), End = new TimeOnly(15, 0) };

        Assert.Equal(new DateTimeOffset(2026, 10, 27, 15, 0, 0, TimeSpan.Zero), QuietHours.End(options, TimeZoneInfo.Utc, new DateTimeOffset(2026, 10, 27, 14, 0, 0, TimeSpan.Zero)));
        Assert.Null(QuietHours.End(options, TimeZoneInfo.Utc, new DateTimeOffset(2026, 10, 27, 16, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void End_AStartEqualToTheEnd_TurnsQuietHoursOff()
    {
        QuietHoursOptions options = new() { Start = new TimeOnly(21, 0), End = new TimeOnly(21, 0) };

        Assert.Null(QuietHours.End(options, TimeZoneInfo.Utc, new DateTimeOffset(2026, 10, 27, 23, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void Instant_AWallClockTimeInADaylightSavingGap_IsTheFirstInstantAfterIt()
    {
        DateTimeOffset instant = QuietHours.Instant(new DateTime(2026, 3, 29, 2, 30, 0, DateTimeKind.Unspecified), Berlin);

        Assert.Equal(new DateTimeOffset(2026, 3, 29, 1, 0, 0, TimeSpan.Zero), instant.ToUniversalTime());
    }

    [Fact]
    public void Instant_AWallClockTimeInAnOverlap_IsItsFirstOccurrence()
    {
        DateTimeOffset instant = QuietHours.Instant(new DateTime(2026, 10, 25, 2, 30, 0, DateTimeKind.Unspecified), Berlin);

        Assert.Equal(new DateTimeOffset(2026, 10, 25, 0, 30, 0, TimeSpan.Zero), instant.ToUniversalTime());
    }
}