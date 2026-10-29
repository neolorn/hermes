using System;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace Hermes.Repository.Tests;

/// <summary>The release compatibility gate's decision, given what the comparison found (<c>04</c> MSG-LIB-006).</summary>
[Trait("Category", "integration")]
public sealed class ReleaseGateTests
{
    private const string Gate = "gate-release-compatibility.ps1";

    private const string Baseline = "build/gates/release-baseline.json";

    /// <summary>A set the release gate reads, as the baseline file holds it.</summary>
    private const string Read = "{\n  \"codes\": [\n    \"messaging.send.unknown\"\n  ]\n}\n";

    [Fact]
    public async Task MSG_LIB_006_AC1_BeforeTheFirstReleaseTheBaselineIsEmptySoTheGatePassesWithoutComparing()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await GitRepository.CreateAsync(folder, cancellationToken);
        string head = await repository.CommitAsync("feat(engine)!: remove a code", cancellationToken);

        ProcessResult gate = await Processes.GateAsync(Gate, repository.Root, cancellationToken, "-Repository", repository.Root, "-Head", head);

        Assert.True(gate.ExitCode == 0, gate.Output);
        Assert.Contains("No release precedes Head, so the baseline is empty and nothing is a break.", gate.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("feat(engine): remove a code", false)]
    [InlineData("feat(engine)!: remove a code", true)]
    [InlineData("feat(engine): remove a code\n\n- The code is no longer answered.\n\nBREAKING CHANGE: the code is gone", true)]
    public async Task MSG_LIB_006_AC1_OnAPullRequestAChangeThatNeedsAMajorReleasePassesOnlyWhereACommitSinceTheReleaseMarksTheBreak(string message, bool passes)
    {
        ArgumentNullException.ThrowIfNull(message);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await GitRepository.CreateAsync(folder, cancellationToken);
        string released = await repository.CommitAsync("docs: add the changelog", cancellationToken);
        await repository.GitAsync(cancellationToken, "tag", "v1.0.0", released);
        await repository.CommitAsync(message, cancellationToken);
        string head = await repository.CommitAsync("test(engine): test the change", cancellationToken);
        string needs = folder.Write("needs.json", """{"needs":"major","lines":["codes: removed or changed: messaging.send.unknown"]}""");

        ProcessResult gate = await Processes.GateAsync(Gate, repository.Root, cancellationToken, "-Repository", repository.Root, "-Base", released, "-Head", head, "-Needs", needs);

        Assert.True((gate.ExitCode == 0) == passes, gate.Output);
        Assert.Contains("codes: removed or changed: messaging.send.unknown", gate.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Read, true)]
    [InlineData("""{"codes":["messaging.send.unknown"],"keys":[]}""", true)]
    [InlineData("""{"codes":["messaging.send.unknown","messaging.send.retired"]}""", false)]
    [InlineData("""{"codes":[]}""", false)]
    public async Task MSG_ENG_011_AC3_APullRequestChangingTheBaselinePassesOnlyWhereTheBaselineAtItsHeadIsTheSetReadThere(string current, bool passes)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await GitRepository.CreateAsync(folder, cancellationToken);
        repository.Write(Baseline, "{}\n");
        string start = await repository.CommitAsync("docs: add the baseline", cancellationToken);
        repository.Write(Baseline, Read);
        string head = await repository.CommitAsync("chore: release 1.0.0", cancellationToken);
        string read = folder.Write("current.json", current);

        ProcessResult gate = await Processes.GateAsync(Gate, repository.Root, cancellationToken, "-Repository", repository.Root, "-Base", start, "-Head", head, "-Current", read);

        Assert.True((gate.ExitCode == 0) == passes, gate.Output);
        Assert.Contains(passes ? "The baseline at Head is the set the release gate reads there." : "The baseline at Head is not the set the release gate reads there", gate.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MSG_ENG_011_AC3_APullRequestThatLeavesTheBaselineDoesNotCompareItWithTheSetReadThere()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await GitRepository.CreateAsync(folder, cancellationToken);
        repository.Write(Baseline, "{}\n");
        string start = await repository.CommitAsync("docs: add the baseline", cancellationToken);
        string head = await repository.CommitAsync("feat(engine): add a code", cancellationToken);
        string read = folder.Write("current.json", Read);

        ProcessResult gate = await Processes.GateAsync(Gate, repository.Root, cancellationToken, "-Repository", repository.Root, "-Base", start, "-Head", head, "-Current", read);

        Assert.True(gate.ExitCode == 0, gate.Output);
        Assert.DoesNotContain("The baseline at Head", gate.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("major", "v1.1.0", false)]
    [InlineData("major", "v2.0.0", true)]
    [InlineData("minor", "v1.0.1", false)]
    [InlineData("minor", "v1.1.0", true)]
    [InlineData("patch", "v1.0.1", true)]
    public async Task MSG_LIB_006_AC1_AReleaseTagBelowWhatTheChangeNeedsFails(string level, string tag, bool passes)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await GitRepository.CreateAsync(folder, cancellationToken);
        string released = await repository.CommitAsync("docs: add the changelog", cancellationToken);
        await repository.GitAsync(cancellationToken, "tag", "v1.0.0", released);
        string head = await repository.CommitAsync("feat(engine)!: change the release", cancellationToken);
        await repository.GitAsync(cancellationToken, "tag", tag, head);
        string needs = folder.Write("needs.json", $$"""{"needs":"{{level}}","lines":[]}""");

        ProcessResult gate = await Processes.GateAsync(Gate, repository.Root, cancellationToken, "-Repository", repository.Root, "-Base", "v1.0.0", "-Head", head, "-Needs", needs);

        Assert.True((gate.ExitCode == 0) == passes, gate.Output);
    }
}