using System;
using System.IO;
using System.Linq;

using Hermes.Core;

using Xunit;

namespace Hermes.Tests;

/// <summary>The set the release gate compares, read from the build (<c>04</c> MSG-LIB-006).</summary>
[Trait("Category", "contract")]
public sealed class ReleaseSetTests
{
    /// <summary>The folder the release gate's script names: it holds the baseline, and the test writes the current set and what the change needs there.</summary>
    internal const string GateFolder = "HERMES_RELEASE_GATE";

    /// <summary>The interfaces of the provider contracts' package the library implements itself.</summary>
    private static readonly Type[] LibraryImplemented = [typeof(IMessages), typeof(IPreferences), typeof(ISuppressions), typeof(ISubjects)];

    [Fact]
    public void MSG_LIB_006_AC1_TheCurrentSetHoldsEverySectionTheGateCompares()
    {
        ReleaseSet current = ReleaseSet.Current(ReleaseSet.Root());

        Assert.Contains("PUT /notifications/preferences/{category}/{channel}", current[ReleaseSet.Routes]);
        Assert.Contains("POST /notifications/push/subscriptions 204", current[ReleaseSet.Statuses]);
        Assert.Contains("SubscriptionBody.endpoint: string", current[ReleaseSet.Bodies]);
        Assert.Contains("messaging.send.nodestination", current[ReleaseSet.Codes]);
        Assert.Contains("Hermes:Dispatch:Interval", current[ReleaseSet.Keys]);
        Assert.Contains("status sent", current[ReleaseSet.Spelled]);
        Assert.Contains("hermes.deliveries Counter {delivery}", current[ReleaseSet.Metrics]);
        Assert.Contains(current[ReleaseSet.Abstract], member => member.StartsWith("Hermes.Core.IEmailProvider.OpenAsync(", StringComparison.Ordinal));
        Assert.Contains(current[ReleaseSet.Surface], line => line.StartsWith("Hermes.Core: ", StringComparison.Ordinal));
    }

    [Fact]
    public void MSG_LIB_006_AC1_EveryPublicInterfaceOfTheContractsIsOneAHostImplementsOrOneTheLibraryDoes()
    {
        Type[] interfaces = [.. typeof(Channel).Assembly.GetExportedTypes().Where(type => type.IsInterface)];

        Assert.Empty(interfaces.Except(ReleaseSet.HostImplemented).Except(LibraryImplemented));
        Assert.Empty(ReleaseSet.HostImplemented.Intersect(LibraryImplemented));
    }

    /// <summary>
    /// Run by the release gate's script with <c>HERMES_RELEASE_GATE</c> naming a folder holding
    /// <c>baseline.json</c> and <c>shipped/</c>: writes <c>current.json</c> and <c>needs.json</c>
    /// there; without it, compares the current set with an empty baseline.
    /// </summary>
    [Fact]
    public void ReleaseGate_WritesTheCurrentSetAndWhatTheChangeFromTheBaselineNeeds()
    {
        string? folder = Environment.GetEnvironmentVariable(GateFolder);
        ReleaseSet current = ReleaseSet.Current(ReleaseSet.Root());
        ReleaseSet baseline = folder is null
            ? ReleaseSet.Read("{}")
            : ReleaseSet.Read(File.ReadAllText(Path.Combine(folder, "baseline.json"))).WithSurface(ReleaseSet.SurfaceOf(Path.Combine(folder, "shipped")));

        ReleaseNeeds needs = ReleaseComparison.Compare(baseline, current);

        Assert.Contains(needs.Level, new[] { ReleaseNeeds.Major, ReleaseNeeds.Minor, ReleaseNeeds.Patch });
        if (folder is not null)
        {
            File.WriteAllText(Path.Combine(folder, "current.json"), current.ToJson());
            File.WriteAllText(Path.Combine(folder, "needs.json"), needs.ToJson());
        }
    }
}