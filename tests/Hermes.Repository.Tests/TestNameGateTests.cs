using System;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace Hermes.Repository.Tests;

/// <remarks>
/// The test names these tests write are split in two literals, so the gate, which reads this
/// file too, never takes them for tests of this repository.
/// </remarks>
[Trait("Category", "integration")]
public sealed class TestNameGateTests
{
    private const string Test =
        "namespace Hermes.Tests;\n\npublic sealed class PreferenceTests\n{\n    [Fact]\n    public void MSG_SEND_006_AC2"
        + "_PreferenceTurnedOffStopsDelivery()\n    {\n    }\n}\n";

    [Fact]
    public async Task MSG_ENG_008_AC2_EveryListedCriterionWithItsTestPasses()
    {
        using ScratchFolder folder = new();
        folder.Write("build/gates/built-items.txt", "MSG-SEND-006 AC2\n");
        folder.Write("tests/Hermes.Tests/PreferenceTests.cs", Test);

        ProcessResult gate = await RunAsync(folder, TestContext.Current.CancellationToken);

        Assert.True(gate.ExitCode == 0, gate.Output);
    }

    [Theory]
    [InlineData("MSG-SEND-006 AC1\n")]
    [InlineData("MSG-SEND-006 AC2\nMSG-SEND-007 AC2\n")]
    [InlineData("MSG-SEND-006 AC2\nMSG-SEND-006\n")]
    [InlineData("MSG-SEND-006 AC2\n\n")]
    public async Task MSG_ENG_008_AC2_ListedCriterionWithoutItsTestFails(string criteria)
    {
        using ScratchFolder folder = new();
        folder.Write("build/gates/built-items.txt", criteria);
        folder.Write("tests/Hermes.Tests/PreferenceTests.cs", Test);

        ProcessResult gate = await RunAsync(folder, TestContext.Current.CancellationToken);

        Assert.NotEqual(0, gate.ExitCode);
    }

    [Fact]
    public async Task MSG_ENG_008_AC2_TestOfACriterionWithAHigherNumberDoesNotCount()
    {
        using ScratchFolder folder = new();
        folder.Write("build/gates/built-items.txt", "MSG-SEND-006 AC1\n");
        folder.Write("tests/Hermes.Tests/PreferenceTests.cs", Test.Replace("AC2_", "AC12_", StringComparison.Ordinal));

        ProcessResult gate = await RunAsync(folder, TestContext.Current.CancellationToken);

        Assert.NotEqual(0, gate.ExitCode);
    }

    private static Task<ProcessResult> RunAsync(ScratchFolder folder, CancellationToken cancellationToken)
    {
        return Processes.GateAsync("gate-test-names.ps1", folder.Path, cancellationToken, "-Repository", folder.Path);
    }
}