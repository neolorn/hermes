using System.Collections.Generic;
using System.IO;

using Xunit;

namespace Hermes.Repository.Tests;

[Trait("Category", "unit")]
public sealed class ThreatModelTests
{
    private static readonly string ThreatModel = RepositoryRoot.Combine("docs", "spec", "10-threat-model.md");

    [Fact]
    public void MSG_THR_001_AC1_EveryControlTheLibraryCarriesHasATestCarryingItsRequirement()
    {
        IReadOnlyList<string> controls = ThreatModelRules.Controls(ThreatModel);

        Assert.Contains("MSG-SMS-004", controls);
        Assert.Contains("MSG-OPS-008", controls);
        Assert.Empty(ThreatModelRules.Untested(controls, ThreatModelRules.Tested(RepositoryRoot.Combine("tests"))));
    }

    [Fact]
    public void MSG_THR_001_AC2_EachConversationThreatRowsControlHasAPassingTestCarryingEachRequirementItNames()
    {
        IReadOnlyList<string> controls = ThreatModelRules.Controls(ThreatModel, "M-038");

        Assert.Equal(["MSG-API-009", "MSG-API-010", "MSG-CONV-001", "MSG-CONV-002", "MSG-CONV-006", "MSG-CONV-007", "MSG-CONV-008", "MSG-CONV-011", "MSG-OPS-007"], controls);
        Assert.Empty(ThreatModelRules.Untested(controls, ThreatModelRules.Tested(RepositoryRoot.Combine("tests"))));
    }

    [Fact]
    public void MSG_THR_001_AC1_AControlNoTestCarriesIsFoundAndTheDeploymentsAreNot()
    {
        using RepositoryFixture fixture = new();
        string tests = Path.Combine(fixture.Root, "tests", "Hermes.Tests");
        Directory.CreateDirectory(tests);
        File.WriteAllText(Path.Combine(tests, "ReportTests.cs"), "public void MSG_SMS_004_AC1_AReportWithAWrongKeyIsRefused() { }");
        File.WriteAllText(Path.Combine(tests, "Notes.cs"), "// MSG_PUSH_004_AC1 is named here, in no test.");

        IEnumerable<string> untested = ThreatModelRules.Untested(["MSG-OPS-007", "MSG-PUSH-004", "MSG-SMS-004"], ThreatModelRules.Tested(fixture.Root));

        Assert.Equal(["MSG-PUSH-004 names a control of 10 MSG-THR-001, and no test carries it."], untested);
    }
}