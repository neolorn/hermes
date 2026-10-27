using System;

using Hermes.Operations;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "unit")]
public sealed class JobStallTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Fact]
    public void Stalled_AJobNoProcessHasStarted_IsNeverStalled()
    {
        Assert.False(JobStall.Stalled(Row(first: null), Interval, 3, Timeout, Now));
    }

    [Fact]
    public void Stalled_PastTheFactorOfItsIntervalsPlusTheTimeoutSinceItsLastCompletionOrRenewal_IsStalledAndAtItIsNot()
    {
        DateTimeOffset edge = Now - TimeSpan.FromSeconds(210);

        Assert.False(JobStall.Stalled(Row(first: edge.AddDays(-1), completed: edge), Interval, 3, Timeout, Now));
        Assert.True(JobStall.Stalled(Row(first: edge.AddDays(-1), completed: edge.AddTicks(-1)), Interval, 3, Timeout, Now));
        Assert.False(JobStall.Stalled(Row(first: edge.AddDays(-1), completed: edge.AddDays(-1), renewed: edge), Interval, 3, Timeout, Now));
    }

    [Fact]
    public void Stalled_AJobStartedThatHasNeitherCompletedNorRenewed_IsJudgedFromItsFirstStart()
    {
        Assert.False(JobStall.Stalled(Row(first: Now.AddMinutes(-3)), Interval, 3, Timeout, Now));
        Assert.True(JobStall.Stalled(Row(first: Now.AddMinutes(-4)), Interval, 3, Timeout, Now));
    }

    private static JobRow Row(DateTimeOffset? first, DateTimeOffset? completed = null, DateTimeOffset? renewed = null)
    {
        return new JobRow { Name = "retention", FirstStartedAt = first, LastStartedAt = first, LastCompletedAt = completed, LastRenewedAt = renewed };
    }
}