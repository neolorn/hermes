using System;
using System.Collections.Generic;
using System.Linq;

using Hermes.Sending;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "unit")]
public sealed class BalanceJudgementTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 20, 0, 0, 0, TimeSpan.Zero);

    /// <summary>The rules of the defaults of <c>08</c> section 4, with no floor.</summary>
    private static readonly BalanceRules Defaults = new(TimeSpan.FromHours(1), 3, TimeSpan.FromDays(7), TimeSpan.FromDays(1), 10, 0, TimeSpan.FromDays(1));

    [Fact]
    public void Spend_IsTheFallBetweenReadingsAndARiseIsATopUpNotSpend()
    {
        IReadOnlyList<(DateTimeOffset, decimal)> readings = Hourly(100, 90, 95, 80);

        Assert.Equal(25m, BalanceJudgement.Spend(readings, Start, Start.AddHours(3)));
        Assert.Equal(15m, BalanceJudgement.Spend(readings, Start.AddHours(2), Start.AddHours(3)));
    }

    [Fact]
    public void Which_AWindowsSpendOverTheFactorTimesTheBaselineAndTheMinimumIsADrain()
    {
        IReadOnlyList<(DateTimeOffset At, decimal Units)> readings = Steady(hours: 48, spendPerHour: 1, lastHour: 20);

        Assert.Equal(BalanceJudgement.Drain, BalanceJudgement.Which(readings, readings[^1].At, Defaults));
    }

    [Fact]
    public void Which_BeforeTheReadingsSpanTheMinimumHistory_JudgesNoDrain()
    {
        IReadOnlyList<(DateTimeOffset At, decimal Units)> readings = Steady(hours: 12, spendPerHour: 1, lastHour: 20);

        Assert.Null(BalanceJudgement.Which(readings, readings[^1].At, Defaults));
    }

    [Fact]
    public void Which_AWindowsSpendBelowTheMinimum_IsNoDrainHoweverTheBaselineCompares()
    {
        IReadOnlyList<(DateTimeOffset At, decimal Units)> readings = Steady(hours: 48, spendPerHour: 0, lastHour: 9);

        Assert.Null(BalanceJudgement.Which(readings, readings[^1].At, Defaults));
    }

    [Fact]
    public void Which_ASpendWithinTheFactorOfTheBaseline_IsNoDrain()
    {
        IReadOnlyList<(DateTimeOffset At, decimal Units)> readings = Steady(hours: 48, spendPerHour: 10, lastHour: 25);

        Assert.Null(BalanceJudgement.Which(readings, readings[^1].At, Defaults));
    }

    [Theory]
    [InlineData(100, 10, 0, true)]
    [InlineData(1000, 10, 0, false)]
    [InlineData(1000, 10, 800, true)]
    [InlineData(50, 0, 50, true)]
    [InlineData(51, 0, 50, false)]
    public void Which_ABalanceThatWouldReachTheFloorWithinTheWarningAtTheLastWindowsRate_IsLow(int balance, int spend, int floor, bool low)
    {
        IReadOnlyList<(DateTimeOffset At, decimal Units)> readings = Hourly(balance + spend, balance);
        BalanceRules sms = Defaults with { BalanceFloor = floor, DrainMinSpend = 1_000 };

        Assert.Equal(low ? BalanceJudgement.Low : null, BalanceJudgement.Which(readings, readings[^1].At, sms));
    }

    [Fact]
    public void Which_NoReadings_HoldsNeither()
    {
        Assert.Null(BalanceJudgement.Which([], Start, Defaults));
    }

    private static List<(DateTimeOffset At, decimal Units)> Hourly(params decimal[] balances)
    {
        return [.. balances.Select((units, hour) => (Start.AddHours(hour), units))];
    }

    /// <summary>Hourly readings over <paramref name="hours"/> spending <paramref name="spendPerHour"/> each hour, then one more hour spending <paramref name="lastHour"/>.</summary>
    private static List<(DateTimeOffset At, decimal Units)> Steady(int hours, decimal spendPerHour, decimal lastHour)
    {
        decimal units = 10_000;
        List<(DateTimeOffset At, decimal Units)> readings = [];
        for (int hour = 0; hour <= hours; hour++)
        {
            readings.Add((Start.AddHours(hour), units));
            units -= spendPerHour;
        }

        readings.Add((Start.AddHours(hours + 1), readings[^1].Units - lastHour));
        return readings;
    }
}