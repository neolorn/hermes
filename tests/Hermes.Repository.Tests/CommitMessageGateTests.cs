using System;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace Hermes.Repository.Tests;

[Trait("Category", "integration")]
public sealed class CommitMessageGateTests
{
    private const string Changelog = """
        # Changelog

        ## [Unreleased]

        ### Added

        """;

    private const string Header = "the first line is not \"type(scope): description\" of an allowed type and scope";

    private const string Fragment = "is not a dash fragment, and the footer holds only Implements: and BREAKING CHANGE: lines";

    [Fact]
    public async Task MSG_ENG_011_AC1_CommitsWithinTheRulePass()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateAsync(folder, cancellationToken);
        await repository.CommitAsync("build: add the shared build settings", cancellationToken);
        await repository.CommitAsync("""
            test(core): add the layout tests

            - the layout is read from the project files, not the solution
            - a fixture breaks one rule at a time

            Implements: MSG-ENG-001, MSG-LIB-001
            """, cancellationToken);
        repository.Write("CHANGELOG.md", Changelog + "- The send operation records a send.\n");
        await repository.CommitAsync("feat(engine): record a send\n\nImplements: MSG-SEND-003\n", cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.True(gate.ExitCode == 0, gate.Output);
        Assert.Contains("3 commit(s)", gate.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("build: add the IMessages interface")]
    [InlineData("feat(channels): add the SMTP provider")]
    public async Task MSG_ENG_011_AC1_NameInItsOwnSpellingPasses(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateAsync(folder, cancellationToken);
        repository.Write("CHANGELOG.md", Changelog + "- The SMTP provider sends email.\n");
        await repository.CommitAsync(message, cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.True(gate.ExitCode == 0, gate.Output);
    }

    [Theory]
    [InlineData("add the shared build settings", Header)]
    [InlineData("feature: add the shared build settings", Header)]
    [InlineData("build(api): add the shared build settings", Header)]
    [InlineData("build:add the shared build settings", Header)]
    [InlineData("build: Add the shared build settings", "the description does not begin with a lower-case letter")]
    [InlineData("build: SMTP settings", "the description does not begin with a lower-case letter")]
    [InlineData("build: add the shared build settings.", "the description ends with a full stop")]
    [InlineData("build: add the shared build settings that every project of the solution takes", "the first line is longer than 72 characters")]
    [InlineData("build: add the shared build settings\nsecond line", "the first line is not followed by a blank line")]
    [InlineData("build: add the shared build settings\n\nThe settings are shared.", "\"The settings are shared.\" " + Fragment)]
    [InlineData("build: add the shared build settings\n\n- a fragment that runs on for longer than the seventy-two characters allowed", "is longer than 72 characters")]
    [InlineData("build: add the shared build settings\n\nCo-Authored-By: Someone <someone@example.invalid>", Fragment)]
    [InlineData("build: add the shared build settings\n\nSigned-off-by: Someone <someone@example.invalid>", Fragment)]
    [InlineData("build: add the shared build settings\n\nImplements: MSG-SEND-6", "\"Implements: MSG-SEND-6\" " + Fragment)]
    [InlineData("build: add the shared build settings\n\nImplements: MSG-SEND-006\nReviewed-by: Someone", "\"Reviewed-by: Someone\" " + Fragment)]
    public async Task MSG_ENG_011_AC1_CommitMessageOutsideTheRuleFails(string message, string reason)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(reason);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateAsync(folder, cancellationToken);
        await repository.CommitAsync(message, cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.NotEqual(0, gate.ExitCode);
        Assert.Contains(reason, gate.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("feat(engine): record a send")]
    [InlineData("fix(channels): class a refused recipient as permanent")]
    [InlineData("perf(engine): claim deliveries in one statement")]
    [InlineData("refactor(core)!: rename the send's values")]
    [InlineData("refactor(core): rename the send's values\n\nBREAKING CHANGE: the values are renamed")]
    public async Task MSG_ENG_011_AC1_BehaviourChangingCommitWithoutAChangelogLineFails(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateAsync(folder, cancellationToken);
        await repository.CommitAsync(message, cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.NotEqual(0, gate.ExitCode);
        Assert.Contains("adds no line under the changelog's Unreleased section", gate.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MSG_ENG_011_AC1_ChangelogLineOutsideUnreleasedFails()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateAsync(folder, cancellationToken);
        repository.Write("CHANGELOG.md", Changelog + "## [0.1.0] - 2026-10-22\n\n### Added\n\n- The send operation records a send.\n");
        await repository.CommitAsync("feat(engine): record a send", cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.NotEqual(0, gate.ExitCode);
        Assert.Contains("adds no line under the changelog's Unreleased section", gate.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MSG_ENG_011_AC1_ChangelogLineUnderChangedPassesOnceAReleaseIsTagged()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateAsync(folder, cancellationToken);
        await repository.GitAsync(cancellationToken, "tag", "v1.0.0", start);
        repository.Write("CHANGELOG.md", Changelog.Replace("### Added", "### Changed", StringComparison.Ordinal) + "- A send records its time zone.\n");
        await repository.CommitAsync("feat(engine): record the send's time zone", cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.True(gate.ExitCode == 0, gate.Output);
    }

    [Theory]
    [InlineData("v1.0.0-rc.1")]
    [InlineData("v1.0")]
    [InlineData("version-1.0.0")]
    public async Task MSG_ENG_011_AC1_ChangelogLineUnderChangedFailsAfterATagThatIsNoRelease(string tag)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateAsync(folder, cancellationToken);
        await repository.GitAsync(cancellationToken, "tag", tag, start);
        repository.Write("CHANGELOG.md", Changelog.Replace("### Added", "### Changed", StringComparison.Ordinal) + "- A send records its time zone.\n");
        await repository.CommitAsync("feat(engine): record the send's time zone", cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.NotEqual(0, gate.ExitCode);
        Assert.Contains("adds no line under the changelog's Unreleased section", gate.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MSG_ENG_011_AC1_MergeCommitsThePlatformWritesAreNotChecked()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateAsync(folder, cancellationToken);
        await repository.GitAsync(cancellationToken, "switch", "--quiet", "--create", "phase-01");
        await repository.CommitAsync("build: add the shared build settings", cancellationToken);
        await repository.GitAsync(cancellationToken, "switch", "--quiet", "main");
        await repository.GitAsync(cancellationToken, "-c", "user.name=GitHub", "-c", "user.email=noreply@github.com", "merge", "--quiet", "--no-ff", "--no-edit", "-m", "Merge pull request #1 from owner/phase-01", "phase-01");

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.True(gate.ExitCode == 0, gate.Output);
        Assert.Contains("1 commit(s)", gate.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MSG_ENG_011_AC1_LocalMergeCommitOutsideTheRuleFails()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateAsync(folder, cancellationToken);
        await repository.GitAsync(cancellationToken, "switch", "--quiet", "--create", "phase-01");
        await repository.CommitAsync("build: add the shared build settings", cancellationToken);
        await repository.GitAsync(cancellationToken, "switch", "--quiet", "main");
        await repository.GitAsync(cancellationToken, "merge", "--quiet", "--no-ff", "--no-edit", "-m", "Merge branch 'phase-01'", "phase-01");

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.NotEqual(0, gate.ExitCode);
        Assert.Contains("\"Merge branch 'phase-01'\": " + Header, gate.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("src/Hermes.Core/PublicAPI.Shipped.txt")]
    [InlineData("build/gates/release-baseline.json")]
    public async Task MSG_ENG_011_AC2_CommitChangingAReleasedFileFails(string path)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateWithReleasedFilesAsync(folder, cancellationToken);
        repository.Write(path, "#nullable enable\nHermes.Core.Send\n");
        await repository.CommitAsync("build: list the send", cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.NotEqual(0, gate.ExitCode);
        Assert.Contains($"it changes {path}, which changes only in a release commit", gate.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("src/Hermes.Core/PublicAPI.Shipped.txt")]
    [InlineData("build/gates/release-baseline.json")]
    public async Task MSG_ENG_011_AC2_CommitDeletingAReleasedFileFails(string path)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateWithReleasedFilesAsync(folder, cancellationToken);
        await repository.GitAsync(cancellationToken, "rm", "--quiet", path);
        await repository.CommitAsync("build: remove the list", cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.NotEqual(0, gate.ExitCode);
        Assert.Contains($"it deletes {path}, which changes only in a release commit", gate.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MSG_ENG_011_AC2_NewProjectsPublicApiFileAndAFileMovedUnchangedPass()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateWithReleasedFilesAsync(folder, cancellationToken);
        repository.Write("src/Hermes.Cli/PublicAPI.Shipped.txt", "#nullable enable\n");
        repository.Write("src/Hermes.Core/PublicAPI.Unshipped.txt", "#nullable enable\nHermes.Core.Send\n");
        await repository.CommitAsync("build: add the command line project", cancellationToken);
        await repository.GitAsync(cancellationToken, "mv", "src/Hermes.Core", "src/Renamed.Core");
        await repository.CommitAsync("refactor: rename the core project", cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.True(gate.ExitCode == 0, gate.Output);
    }

    [Fact]
    public async Task PreviousReleaseTag_TagsOfOtherShapes_AreLeftOut()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateAsync(folder, cancellationToken);
        await repository.GitAsync(cancellationToken, "tag", "v1.0.0", start);
        string second = await repository.CommitAsync("build: add the shared build settings", cancellationToken);
        await repository.GitAsync(cancellationToken, "tag", "v1.1.0-rc.1", second);
        await repository.GitAsync(cancellationToken, "tag", "v9", second);
        string head = await repository.CommitAsync("docs: add the release notes", cancellationToken);

        ProcessResult previous = await Processes.GateAsync("previous-release-tag.ps1", repository.Root, cancellationToken, "-Repository", repository.Root, "-Head", head);

        Assert.True(previous.ExitCode == 0, previous.Output);
        Assert.Equal("v1.0.0", previous.Output.Trim());
    }

    [Fact]
    public async Task PreviousReleaseTag_FirstRelease_IsNone()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, _) = await CreateAsync(folder, cancellationToken);
        string head = await repository.CommitAsync("docs: add the release notes", cancellationToken);

        ProcessResult previous = await Processes.GateAsync("previous-release-tag.ps1", repository.Root, cancellationToken, "-Repository", repository.Root, "-Head", head);

        Assert.True(previous.ExitCode == 0, previous.Output);
        Assert.Equal(string.Empty, previous.Output.Trim());
    }

    private static async Task<(GitRepository Repository, string Start)> CreateAsync(ScratchFolder folder, CancellationToken cancellationToken)
    {
        GitRepository repository = await GitRepository.CreateAsync(folder, cancellationToken);
        repository.Write("CHANGELOG.md", Changelog);
        string start = await repository.CommitAsync("docs: add the changelog", cancellationToken);
        return (repository, start);
    }

    private static async Task<(GitRepository Repository, string Start)> CreateWithReleasedFilesAsync(ScratchFolder folder, CancellationToken cancellationToken)
    {
        GitRepository repository = await GitRepository.CreateAsync(folder, cancellationToken);
        repository.Write("CHANGELOG.md", Changelog);
        repository.Write("src/Hermes.Core/PublicAPI.Shipped.txt", "#nullable enable\n");
        repository.Write("src/Hermes.Core/PublicAPI.Unshipped.txt", "#nullable enable\n");
        repository.Write("build/gates/release-baseline.json", "{}\n");
        string start = await repository.CommitAsync("docs: add the changelog and the released files", cancellationToken);
        return (repository, start);
    }

    private static Task<ProcessResult> RunAsync(GitRepository repository, string start, CancellationToken cancellationToken)
    {
        return Processes.GateAsync("gate-commit-messages.ps1", repository.Root, cancellationToken, "-Repository", repository.Root, "-Base", start, "-Head", "HEAD");
    }
}