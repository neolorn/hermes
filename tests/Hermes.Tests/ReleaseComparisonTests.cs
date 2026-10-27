using System.Linq;

using Xunit;

namespace Hermes.Tests;

/// <summary>The release a change needs (<c>04</c> MSG-LIB-006).</summary>
[Trait("Category", "unit")]
public sealed class ReleaseComparisonTests
{
    private static readonly ReleaseSet Baseline = ReleaseSet.Of(
        (ReleaseSet.Routes, ["GET /notifications/preferences"]),
        (ReleaseSet.Statuses, ["GET /notifications/preferences 200"]),
        (ReleaseSet.Bodies, ["PreferenceChange.on: boolean?"]),
        (ReleaseSet.Codes, ["messaging.send.unknown"]),
        (ReleaseSet.Abstract, ["Hermes.Core.IAlertSink.RaiseAsync(String, String, IReadOnlyDictionary`2, CancellationToken)"]),
        (ReleaseSet.Surface, ["Hermes.Core: Hermes.Core.Channel.Email = 0 -> Hermes.Core.Channel"]));

    private interface IProbe
    {
        void Must();

        void May()
        {
        }
    }

    [Fact]
    public void MSG_LIB_006_AC1_AnAdditionNeedsAMinorReleaseAndTheSameSetAPatch()
    {
        ReleaseSet added = Baseline.WithSurface(["Hermes.Core: Hermes.Core.Channel.Email = 0 -> Hermes.Core.Channel", "Hermes.Core: Hermes.Core.Channel.Sms = 1 -> Hermes.Core.Channel"]);
        ReleaseSet status = ReleaseSet.Of(
            (ReleaseSet.Routes, ["GET /notifications/preferences"]),
            (ReleaseSet.Statuses, ["GET /notifications/preferences 200", "GET /notifications/preferences 401"]),
            (ReleaseSet.Bodies, ["PreferenceChange.on: boolean?"]),
            (ReleaseSet.Codes, ["messaging.send.unknown", "messaging.send.other"]),
            (ReleaseSet.Abstract, ["Hermes.Core.IAlertSink.RaiseAsync(String, String, IReadOnlyDictionary`2, CancellationToken)"]),
            (ReleaseSet.Surface, ["Hermes.Core: Hermes.Core.Channel.Email = 0 -> Hermes.Core.Channel"]));

        Assert.Equal(ReleaseNeeds.Minor, ReleaseComparison.Compare(Baseline, added).Level);
        Assert.Equal(ReleaseNeeds.Minor, ReleaseComparison.Compare(Baseline, status).Level);
        Assert.Equal(ReleaseNeeds.Patch, ReleaseComparison.Compare(Baseline, Baseline).Level);
    }

    [Theory]
    [InlineData(ReleaseSet.Routes, "GET /notifications/preferences")]
    [InlineData(ReleaseSet.Statuses, "GET /notifications/preferences 200")]
    [InlineData(ReleaseSet.Bodies, "PreferenceChange.on: boolean?")]
    [InlineData(ReleaseSet.Codes, "messaging.send.unknown")]
    [InlineData(ReleaseSet.Surface, "Hermes.Core: Hermes.Core.Channel.Email = 0 -> Hermes.Core.Channel")]
    public void MSG_LIB_006_AC1_ARemovalOrAChangeNeedsAMajorRelease(string section, string line)
    {
        ReleaseSet current = Without(section, line);

        ReleaseNeeds needs = ReleaseComparison.Compare(Baseline, current);

        Assert.Equal(ReleaseNeeds.Major, needs.Level);
        Assert.Equal([$"{section}: removed or changed: {line}"], needs.Lines);
    }

    [Fact]
    public void MSG_LIB_006_AC1_AMemberAddedToAHostImplementedInterfaceWithoutADefaultAndARequestMemberACallerMustSendAreBreaks()
    {
        string[] surface = [.. Baseline[ReleaseSet.Surface], "Hermes.Tests: Hermes.Tests.ReleaseComparisonTests+IProbe"];
        ReleaseSet before = Baseline.WithSurface(surface);
        ReleaseSet member = ReleaseSet.Of(
            (ReleaseSet.Routes, [.. Baseline[ReleaseSet.Routes]]),
            (ReleaseSet.Statuses, [.. Baseline[ReleaseSet.Statuses]]),
            (ReleaseSet.Bodies, [.. Baseline[ReleaseSet.Bodies]]),
            (ReleaseSet.Codes, [.. Baseline[ReleaseSet.Codes]]),
            (ReleaseSet.Abstract, [.. Baseline[ReleaseSet.Abstract], .. ReleaseSet.AbstractMembers(typeof(IProbe))]),
            (ReleaseSet.Surface, surface));
        ReleaseSet required = ReleaseSet.Of(
            (ReleaseSet.Routes, [.. Baseline[ReleaseSet.Routes]]),
            (ReleaseSet.Statuses, [.. Baseline[ReleaseSet.Statuses]]),
            (ReleaseSet.Bodies, [.. Baseline[ReleaseSet.Bodies]]),
            (ReleaseSet.Required, ["PreferenceChange.on"]),
            (ReleaseSet.Codes, [.. Baseline[ReleaseSet.Codes]]),
            (ReleaseSet.Abstract, [.. Baseline[ReleaseSet.Abstract]]),
            (ReleaseSet.Surface, [.. Baseline[ReleaseSet.Surface]]));

        ReleaseNeeds added = ReleaseComparison.Compare(before, member);

        Assert.Equal(["Hermes.Tests.ReleaseComparisonTests+IProbe.Must()"], ReleaseSet.AbstractMembers(typeof(IProbe)));
        Assert.Equal((ReleaseNeeds.Major, "abstract: added: Hermes.Tests.ReleaseComparisonTests+IProbe.Must()"), (added.Level, Assert.Single(added.Lines)));
        Assert.Equal(ReleaseNeeds.Minor, ReleaseComparison.Compare(Baseline, member).Level);
        Assert.Equal(ReleaseNeeds.Major, ReleaseComparison.Compare(Baseline, required).Level);
    }

    [Fact]
    public void MSG_LIB_006_AC1_BeforeTheFirstReleaseTheBaselineIsEmptySoNothingIsABreak()
    {
        ReleaseNeeds needs = ReleaseComparison.Compare(ReleaseSet.Read("{}"), Baseline);

        Assert.Equal(ReleaseNeeds.Minor, needs.Level);
    }

    /// <summary>The baseline without <paramref name="line"/> in <paramref name="section"/>.</summary>
    private static ReleaseSet Without(string section, string line)
    {
        string[] sections = [ReleaseSet.Routes, ReleaseSet.Statuses, ReleaseSet.Bodies, ReleaseSet.Codes, ReleaseSet.Abstract, ReleaseSet.Surface];
        return ReleaseSet.Of([.. sections.Select(name => (name, Baseline[name].Where(held => name != section || held != line).ToArray()))]);
    }
}