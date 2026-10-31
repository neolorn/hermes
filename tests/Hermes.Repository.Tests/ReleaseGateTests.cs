using System;
using System.IO;
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

    private const string NothingBinds = "No final release precedes Head, so the baseline is empty and nothing is a break.";

    [Fact]
    public async Task MSG_LIB_006_AC1_BeforeTheFirstReleaseTheBaselineIsEmptySoTheGatePassesWithoutComparing()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await GitRepository.CreateAsync(folder, cancellationToken);
        string head = await repository.CommitAsync("feat(engine)!: remove a code", cancellationToken);

        ProcessResult gate = await Processes.GateAsync(Gate, repository.Root, cancellationToken, "-Repository", repository.Root, "-Head", head);

        Assert.True(gate.ExitCode == 0, gate.Output);
        Assert.Contains(NothingBinds, gate.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("v1.0.0-rc.2")]
    [InlineData("v1.0.0")]
    public async Task MSG_LIB_006_AC2_WithNoFinalReleaseTaggedAPullRequestAndAReleaseTagThatRemoveAPublicMemberPass(string? tag)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await GitRepository.CreateAsync(folder, cancellationToken);
        string candidate = await repository.CommitAsync("chore: release 1.0.0-rc.1", cancellationToken);
        await repository.GitAsync(cancellationToken, "tag", "v1.0.0-rc.1", candidate);
        string head = await repository.CommitAsync("feat(core): remove a public member", cancellationToken);
        if (tag is not null)
        {
            await repository.GitAsync(cancellationToken, "tag", tag, head);
        }

        string needs = folder.Write("needs.json", """{"needs":"major","lines":["public surface: removed or changed: Hermes.Core.Send"]}""");

        ProcessResult gate = await Processes.GateAsync(Gate, repository.Root, cancellationToken, "-Repository", repository.Root, "-Base", candidate, "-Head", head, "-Needs", needs);

        Assert.True(gate.ExitCode == 0, gate.Output);
        Assert.Contains(NothingBinds, gate.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, "A commit since v1.0.0 marks the break.")]
    [InlineData("v1.1.0-rc.1", "v1.1.0-rc.1 is the major release the change needs, or above it.")]
    public async Task MSG_LIB_006_AC2_WithAFinalReleaseTaggedTheSameRemovalFailsInAPullRequestAndInAReleaseCandidatesTag(string? tag, string passing)
    {
        ArgumentNullException.ThrowIfNull(passing);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await GitRepository.CreateAsync(folder, cancellationToken);
        string released = await repository.CommitAsync("chore: release 1.0.0", cancellationToken);
        await repository.GitAsync(cancellationToken, "tag", "v1.0.0", released);
        string head = await repository.CommitAsync("feat(core): remove a public member", cancellationToken);
        if (tag is not null)
        {
            await repository.GitAsync(cancellationToken, "tag", tag, head);
        }

        string needs = folder.Write("needs.json", """{"needs":"major","lines":["public surface: removed or changed: Hermes.Core.Send"]}""");

        ProcessResult gate = await Processes.GateAsync(Gate, repository.Root, cancellationToken, "-Repository", repository.Root, "-Base", released, "-Head", head, "-Needs", needs);

        // The gate's refusal is thrown, and PowerShell wraps a thrown message at the console's
        // width, so the test reads the lines the gate writes rather than the refusal's own text.
        Assert.NotEqual(0, gate.ExitCode);
        Assert.Contains("Since v1.0.0 the change needs a major release.", gate.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(passing, gate.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("feat(core)!: remove a public member", true)]
    [InlineData("feat(core): remove a public member", false)]
    public async Task MSG_LIB_006_AC1_APullRequestIsComparedWithTheNewestFinalReleaseAndNeverWithANewerReleaseCandidate(string marked, bool passes)
    {
        ArgumentNullException.ThrowIfNull(marked);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await GitRepository.CreateAsync(folder, cancellationToken);
        string released = await repository.CommitAsync("chore: release 1.0.0", cancellationToken);
        await repository.GitAsync(cancellationToken, "tag", "v1.0.0", released);
        await repository.CommitAsync(marked, cancellationToken);
        string candidate = await repository.CommitAsync("chore: release 2.0.0-rc.1", cancellationToken);
        await repository.GitAsync(cancellationToken, "tag", "v2.0.0-rc.1", candidate);
        string head = await repository.CommitAsync("feat(core): remove another public member", cancellationToken);
        string needs = folder.Write("needs.json", """{"needs":"major","lines":[]}""");

        ProcessResult gate = await Processes.GateAsync(Gate, repository.Root, cancellationToken, "-Repository", repository.Root, "-Base", candidate, "-Head", head, "-Needs", needs);

        Assert.True((gate.ExitCode == 0) == passes, gate.Output);
        Assert.Contains("Since v1.0.0 the change needs a major release.", gate.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MSG_LIB_006_AC1_TheBaselineComparedWithIsWhatTheNewestFinalReleasesTagHoldsNotTheBaseOrAReleaseCandidate()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await GitRepository.CreateAsync(folder, cancellationToken);
        repository.Write(Baseline, "{\"codes\":[\"messaging.final.kept\"]}\n");
        repository.Write("src/Hermes.Extra/PublicAPI.Shipped.txt", "#nullable enable\nHermes.Extra.Final\n");
        string released = await repository.CommitAsync("chore: release 1.0.0", cancellationToken);
        await repository.GitAsync(cancellationToken, "tag", "v1.0.0", released);
        repository.Write(Baseline, "{\"codes\":[\"messaging.candidate.kept\"]}\n");
        repository.Write("src/Hermes.Extra/PublicAPI.Shipped.txt", "#nullable enable\nHermes.Extra.Candidate\n");
        string candidate = await repository.CommitAsync("chore: release 1.1.0-rc.1", cancellationToken);
        await repository.GitAsync(cancellationToken, "tag", "v1.1.0-rc.1", candidate);
        string head = await repository.CommitAsync("feat(core): add a public member", cancellationToken);

        // The made-up repository holds no test project to read the current set with, so the read
        // fails after the gate has laid out, in its folder, the baseline it would compare with.
        ProcessResult gate = await Processes.GateAsync(Gate, repository.Root, cancellationToken, "-Repository", repository.Root, "-Base", candidate, "-Head", head);

        Assert.NotEqual(0, gate.ExitCode);
        Assert.Contains("The current set could not be read and compared", gate.Output, StringComparison.Ordinal);
        string laidOut = Path.Combine(repository.Root, "tmp", "release-gate");
        Assert.Contains("messaging.final.kept", await File.ReadAllTextAsync(Path.Combine(laidOut, "baseline.json"), cancellationToken), StringComparison.Ordinal);
        Assert.Equal("#nullable enable\nHermes.Extra.Final", (await File.ReadAllTextAsync(Path.Combine(laidOut, "shipped", "Hermes.Extra.txt"), cancellationToken)).ReplaceLineEndings("\n").Trim());
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
    [InlineData("major", "v1.1.0-rc.1", false)]
    [InlineData("major", "v2.0.0-rc.1", true)]
    [InlineData("minor", "v1.0.1-rc.2", false)]
    [InlineData("minor", "v1.1.0-rc.1", true)]
    [InlineData("patch", "v1.0.0-rc.2", false)]
    [InlineData("patch", "v1.0.1-rc.1", true)]
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

    [Theory]
    [InlineData(new[] { "v1.0.0-rc.1", "v1.0.0-rc.2" }, "")]
    [InlineData(new[] { "v1.0.0-rc.3", "v1.0.0", "v1.1.0-rc.1" }, "v1.0.0")]
    [InlineData(new[] { "v1.0.0", "v1.1.0", "v2.0.0-rc.4" }, "v1.1.0")]
    public async Task PreviousReleaseTag_Final_LeavesOutEveryReleaseCandidate(string[] tags, string expected)
    {
        ArgumentNullException.ThrowIfNull(tags);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await GitRepository.CreateAsync(folder, cancellationToken);
        foreach (string tag in tags)
        {
            string commit = await repository.CommitAsync("chore: release " + tag[1..], cancellationToken);
            await repository.GitAsync(cancellationToken, "tag", tag, commit);
        }

        string head = await repository.CommitAsync("docs: add the release notes", cancellationToken);

        ProcessResult previous = await Processes.GateAsync("previous-release-tag.ps1", repository.Root, cancellationToken, "-Repository", repository.Root, "-Head", head, "-Final");

        Assert.True(previous.ExitCode == 0, previous.Output);
        Assert.Equal(expected, previous.Output.Trim());
    }
}