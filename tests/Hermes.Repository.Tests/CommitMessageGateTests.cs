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

    /// <summary>A made-up project's folder, so no test names a project of the repository.</summary>
    private const string ProjectFolder = "src/Hermes.Extra";

    /// <summary>The release commit's date, whose day in UTC is the day before its day where it was made.</summary>
    private const string ReleaseDate = "2026-10-31T01:30:00+03:00";

    private const string Entry = "\n- The send operation records a send.\n";

    /// <summary>The final release the release commit makes where a test names no other version.</summary>
    private const string FinalRelease = "1.0.0";

    private const string Shipped = "#nullable enable\nHermes.Extra.Old\nHermes.Extra.Gone\n";

    private const string Unshipped = "#nullable enable\nabstract Hermes.Extra.Send.Name.get -> string!\nHermes.Extra.Send\nHermes.Extra.Added\n*REMOVED*Hermes.Extra.Gone\n";

    private const string ReleasedShipped = "#nullable enable\nHermes.Extra.Added\nHermes.Extra.Old\nHermes.Extra.Send\nabstract Hermes.Extra.Send.Name.get -> string!\n";

    private const string ShippedProblem = ProjectFolder + "/PublicAPI.Shipped.txt is not its parent's entries and those of the PublicAPI.Unshipped.txt beside it, less each one marked *REMOVED*, under #nullable enable in ordinal order";

    private const string ChangelogProblem = "CHANGELOG.md does not move the Unreleased entries under ## [1.0.0] - 2026-10-30 below an empty ## [Unreleased]";

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
    [InlineData("build(extra): add the shared build settings", Header)]
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

    [Theory]
    [InlineData("v1.0.0")]
    [InlineData("v1.0.0-rc.1")]
    [InlineData("v1.0.0-rc.10")]
    public async Task MSG_ENG_011_AC1_ChangelogLineUnderChangedPassesOnceAReleaseIsTagged(string tag)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateAsync(folder, cancellationToken);
        await repository.GitAsync(cancellationToken, "tag", tag, start);
        repository.Write("CHANGELOG.md", Changelog.Replace("### Added", "### Changed", StringComparison.Ordinal) + "- A send records its time zone.\n");
        await repository.CommitAsync("feat(engine): record the send's time zone", cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.True(gate.ExitCode == 0, gate.Output);
    }

    [Theory]
    [InlineData("v1.0.0-rc.0")]
    [InlineData("v1.0.0-rc.01")]
    [InlineData("v1.0.0-beta.1")]
    [InlineData("v1.0.0-RC.1")]
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

    [Theory]
    [InlineData("#nullable enable\n")]
    [InlineData("#nullable enable\n\n")]
    [InlineData("\n#nullable enable\n\n\n")]
    [InlineData("")]
    public async Task MSG_ENG_011_AC2_ShippedFileHoldingNoEntryDeletedWithTheProjectFileBesideItPasses(string shipped)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateWithProjectAsync(folder, shipped, cancellationToken);
        await repository.GitAsync(cancellationToken, "rm", "--quiet", "-r", ProjectFolder);
        await repository.CommitAsync("build: remove the extra project", cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.True(gate.ExitCode == 0, gate.Output);
    }

    [Fact]
    public async Task MSG_ENG_011_AC2_ShippedFileHoldingNoEntryDeletedWhileTheProjectFileStaysFails()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateWithProjectAsync(folder, "#nullable enable\n", cancellationToken);
        await repository.GitAsync(cancellationToken, "rm", "--quiet", ProjectFolder + "/PublicAPI.Shipped.txt", ProjectFolder + "/PublicAPI.Unshipped.txt");
        await repository.CommitAsync("build: remove the extra project's lists", cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.NotEqual(0, gate.ExitCode);
        Assert.Contains($"it deletes {ProjectFolder}/PublicAPI.Shipped.txt, which changes only in a release commit", gate.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("#nullable enable\nHermes.Extra.Send\n")]
    [InlineData("Hermes.Extra.Send\n")]
    [InlineData("#nullable enable\n// Hermes.Extra.Send\n")]
    public async Task MSG_ENG_011_AC2_ShippedFileHoldingAnEntryDeletedWithTheProjectFileBesideItFails(string shipped)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateWithProjectAsync(folder, shipped, cancellationToken);
        await repository.GitAsync(cancellationToken, "rm", "--quiet", "-r", ProjectFolder);
        await repository.CommitAsync("build: remove the extra project", cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.NotEqual(0, gate.ExitCode);
        Assert.Contains($"it deletes {ProjectFolder}/PublicAPI.Shipped.txt, which changes only in a release commit", gate.Output, StringComparison.Ordinal);
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

    [Theory]
    [InlineData("1.0.0")]
    [InlineData("1.0.0-rc.1")]
    [InlineData("1.0.0-rc.10")]
    [InlineData("2.3.4-rc.2")]
    public async Task MSG_ENG_011_AC2_TheReleaseCommitChangingItsFilesAsTheValueGivesPasses(string version)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateForReleaseAsync(folder, cancellationToken);
        WriteRelease(repository, version);
        await CommitReleaseAsync(repository, version, cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.True(gate.ExitCode == 0, gate.Output);
        Assert.Contains("1 commit(s)", gate.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("1.0.0-rc.0")]
    [InlineData("1.0.0-rc.01")]
    [InlineData("1.0.0-rc")]
    [InlineData("1.0.0-rc.1.2")]
    [InlineData("1.0.0-beta.1")]
    [InlineData("1.0.0-RC.1")]
    [InlineData("1.0.0+build.1")]
    [InlineData("1.0")]
    [InlineData("v1.0.0")]
    [InlineData("the next version")]
    public async Task MSG_ENG_011_AC3_AReleaseCommitNamingAVersionOutsideBothFormsFails(string version)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateForReleaseAsync(folder, cancellationToken);
        WriteRelease(repository, version);
        await CommitReleaseAsync(repository, version, cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.NotEqual(0, gate.ExitCode);
        Assert.Contains($"it names the release {version}, which is neither X.Y.Z nor X.Y.Z-rc.N with N from 1", gate.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("src/Hermes.Extra/Send.cs")]
    [InlineData("README.md")]
    [InlineData("docs/reports/phase-08.md")]
    public async Task MSG_ENG_011_AC3_AReleaseCommitChangingAFileOutsideItsValueFails(string path)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateForReleaseAsync(folder, cancellationToken);
        WriteRelease(repository);
        repository.Write(path, "namespace Hermes.Extra;\n");
        await CommitReleaseAsync(repository, cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.NotEqual(0, gate.ExitCode);
        Assert.Contains($"it changes {path}, which a release commit does not change", gate.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ProjectFolder + "/PublicAPI.Shipped.txt", "#nullable enable\nabstract Hermes.Extra.Send.Name.get -> string!\nHermes.Extra.Added\nHermes.Extra.Old\nHermes.Extra.Send\n", ShippedProblem)]
    [InlineData(ProjectFolder + "/PublicAPI.Shipped.txt", "#nullable enable\nHermes.Extra.Added\nHermes.Extra.Gone\nHermes.Extra.Old\nHermes.Extra.Send\nabstract Hermes.Extra.Send.Name.get -> string!\n", ShippedProblem)]
    [InlineData(ProjectFolder + "/PublicAPI.Shipped.txt", "#nullable enable\nHermes.Extra.Old\nHermes.Extra.Send\nabstract Hermes.Extra.Send.Name.get -> string!\n", ShippedProblem)]
    [InlineData(ProjectFolder + "/PublicAPI.Shipped.txt", "Hermes.Extra.Added\nHermes.Extra.Old\nHermes.Extra.Send\nabstract Hermes.Extra.Send.Name.get -> string!\n", ShippedProblem)]
    [InlineData(ProjectFolder + "/PublicAPI.Unshipped.txt", "#nullable enable\nHermes.Extra.Send\n", ProjectFolder + "/PublicAPI.Unshipped.txt holds more than #nullable enable")]
    [InlineData("CHANGELOG.md", "# Changelog\n\n## [Unreleased]\n\n## [1.0.0] - 2026-10-31\n\n### Added\n" + Entry, ChangelogProblem)]
    [InlineData("CHANGELOG.md", Changelog + Entry + "\n## [1.0.0] - 2026-10-30\n", ChangelogProblem)]
    public async Task MSG_ENG_011_AC3_AReleaseCommitChangingOneOfItsFilesOtherwiseThanTheValueGivesFails(string path, string content, string problem)
    {
        ArgumentNullException.ThrowIfNull(content);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateForReleaseAsync(folder, cancellationToken);
        WriteRelease(repository);
        repository.Write(path, content);
        await CommitReleaseAsync(repository, cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.NotEqual(0, gate.ExitCode);
        Assert.Contains(problem, gate.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("1.0.0")]
    [InlineData("1.0.0-rc.2")]
    [InlineData("1.0.0-rc")]
    public async Task MSG_ENG_011_AC3_AReleaseCandidatesReleaseCommitWhoseChangelogHeadingNamesAnotherVersionFails(string heading)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateForReleaseAsync(folder, cancellationToken);
        WriteRelease(repository, heading);
        await CommitReleaseAsync(repository, "1.0.0-rc.1", cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.NotEqual(0, gate.ExitCode);
        Assert.Contains("CHANGELOG.md does not move the Unreleased entries under ## [1.0.0-rc.1] - 2026-10-30 below an empty ## [Unreleased]", gate.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MSG_ENG_011_AC3_AReleaseCommitDeletingTheReleaseNotesFails()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateForReleaseAsync(folder, cancellationToken);
        WriteRelease(repository);
        await repository.GitAsync(cancellationToken, "rm", "--quiet", "RELEASE-NOTES.md");
        await CommitReleaseAsync(repository, cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.NotEqual(0, gate.ExitCode);
        Assert.Contains("it deletes RELEASE-NOTES.md, which a release commit does not delete", gate.Output, StringComparison.Ordinal);
        Assert.Contains("it holds no RELEASE-NOTES.md", gate.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("1.0.0", "docs: correct the report", "docs/reports/phase-08.md", true)]
    [InlineData("1.0.0", "docs: correct the readme", "README.md", false)]
    [InlineData("1.0.0", "test: correct the report", "docs/reports/phase-08.md", false)]
    [InlineData("1.0.0-rc.1", "docs: correct the report", "docs/reports/phase-08.md", true)]
    [InlineData("1.0.0-rc.1", "docs: correct the readme", "README.md", false)]
    [InlineData("1.0.0-rc.1", "test: correct the report", "docs/reports/phase-08.md", false)]
    public async Task MSG_ENG_011_AC3_OnlyADocsCommitChangingOnlyDocsFollowsTheReleaseCommitInItsPullRequest(string version, string message, string path, bool passes)
    {
        ArgumentNullException.ThrowIfNull(message);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateForReleaseAsync(folder, cancellationToken);
        WriteRelease(repository, version);
        string release = await CommitReleaseAsync(repository, version, cancellationToken);
        repository.Write(path, "# Phase 8\n");
        await repository.CommitAsync(message, cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.True((gate.ExitCode == 0) == passes, gate.Output);
        if (!passes)
        {
            Assert.Contains($"it follows the release commit {release[..12]} in its pull request and is not a docs: commit changing only docs/", gate.Output, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task MSG_ENG_011_AC3_ACommitAfterTheMergeOfTheReleaseCommitsPullRequestIsNotItsFollower()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateForReleaseAsync(folder, cancellationToken);
        await repository.GitAsync(cancellationToken, "switch", "--quiet", "--create", "phase-08");
        WriteRelease(repository);
        await CommitReleaseAsync(repository, cancellationToken);
        await repository.GitAsync(cancellationToken, "switch", "--quiet", "main");
        await repository.GitAsync(cancellationToken, "-c", "user.name=GitHub", "-c", "user.email=noreply@github.com", "merge", "--quiet", "--no-ff", "--no-edit", "-m", "Merge pull request #9 from owner/phase-08", "phase-08");
        await repository.CommitAsync("build: add the shared build settings", cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.True(gate.ExitCode == 0, gate.Output);
        Assert.Contains("2 commit(s)", gate.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreviousReleaseTag_TagsOfOtherShapes_AreLeftOut()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateAsync(folder, cancellationToken);
        await repository.GitAsync(cancellationToken, "tag", "v1.0.0", start);
        string second = await repository.CommitAsync("build: add the shared build settings", cancellationToken);
        foreach (string tag in new[] { "v9", "v2.0", "v2.0.0-rc.0", "v2.0.0-rc.01", "v2.0.0-beta.1", "v2.0.0-RC.1", "v2.0.0-rc.1.1", "version-2.0.0" })
        {
            await repository.GitAsync(cancellationToken, "tag", tag, second);
        }

        string head = await repository.CommitAsync("docs: add the release notes", cancellationToken);

        ProcessResult previous = await PreviousReleaseTagAsync(repository, head, cancellationToken);

        Assert.True(previous.ExitCode == 0, previous.Output);
        Assert.Equal("v1.0.0", previous.Output.Trim());
    }

    [Theory]
    [InlineData("v1.0.0-rc.1", "v1.0.0-rc.2", "v1.0.0-rc.2")]
    [InlineData("v1.0.0-rc.10", "v1.0.0-rc.9", "v1.0.0-rc.10")]
    [InlineData("v1.0.0-rc.10", "v1.0.0", "v1.0.0")]
    [InlineData("v1.0.0", "v1.1.0-rc.1", "v1.1.0-rc.1")]
    [InlineData("v1.10.0-rc.1", "v1.9.0", "v1.10.0-rc.1")]
    public async Task PreviousReleaseTag_AReleaseCandidate_IsAReleaseTakenInSemanticVersioningsPrecedence(string first, string second, string expected)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateAsync(folder, cancellationToken);
        await repository.GitAsync(cancellationToken, "tag", first, start);
        string next = await repository.CommitAsync("build: add the shared build settings", cancellationToken);
        await repository.GitAsync(cancellationToken, "tag", second, next);
        string head = await repository.CommitAsync("docs: add the release notes", cancellationToken);

        ProcessResult previous = await PreviousReleaseTagAsync(repository, head, cancellationToken);

        Assert.True(previous.ExitCode == 0, previous.Output);
        Assert.Equal(expected, previous.Output.Trim());
    }

    [Fact]
    public async Task PreviousReleaseTag_FirstRelease_IsNone()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, _) = await CreateAsync(folder, cancellationToken);
        string head = await repository.CommitAsync("docs: add the release notes", cancellationToken);

        ProcessResult previous = await PreviousReleaseTagAsync(repository, head, cancellationToken);

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

    /// <summary>A repository holding a project at <see cref="ProjectFolder"/>, its shipped list holding <paramref name="shipped"/>.</summary>
    private static async Task<(GitRepository Repository, string Start)> CreateWithProjectAsync(ScratchFolder folder, string shipped, CancellationToken cancellationToken)
    {
        GitRepository repository = await GitRepository.CreateAsync(folder, cancellationToken);
        repository.Write("CHANGELOG.md", Changelog);
        repository.Write(ProjectFolder + "/Hermes.Extra.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
        repository.Write(ProjectFolder + "/PublicAPI.Shipped.txt", shipped);
        repository.Write(ProjectFolder + "/PublicAPI.Unshipped.txt", "#nullable enable\n");
        string start = await repository.CommitAsync("docs: add the changelog and the extra project", cancellationToken);
        return (repository, start);
    }

    /// <summary>A repository holding a project at <see cref="ProjectFolder"/> with entries to release, the baseline and the release notes.</summary>
    private static async Task<(GitRepository Repository, string Start)> CreateForReleaseAsync(ScratchFolder folder, CancellationToken cancellationToken)
    {
        GitRepository repository = await GitRepository.CreateAsync(folder, cancellationToken);
        repository.Write("CHANGELOG.md", Changelog + Entry);
        repository.Write("RELEASE-NOTES.md", "# Hermes 1.0.0\n");
        repository.Write(ProjectFolder + "/Hermes.Extra.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
        repository.Write(ProjectFolder + "/PublicAPI.Shipped.txt", Shipped);
        repository.Write(ProjectFolder + "/PublicAPI.Unshipped.txt", Unshipped);
        repository.Write("build/gates/release-baseline.json", "{}\n");
        string start = await repository.CommitAsync("docs: add the changelog, the release notes and the extra project", cancellationToken);
        return (repository, start);
    }

    /// <summary>Writes the release commit's files as the release commit's value gives them for release 1.0.0.</summary>
    private static void WriteRelease(GitRepository repository)
    {
        WriteRelease(repository, FinalRelease);
    }

    /// <summary>Writes the release commit's files as the release commit's value gives them for release <paramref name="version"/>.</summary>
    private static void WriteRelease(GitRepository repository, string version)
    {
        repository.Write("CHANGELOG.md", "# Changelog\n\n## [Unreleased]\n\n## [" + version + "] - 2026-10-30\n\n### Added\n" + Entry);
        repository.Write(ProjectFolder + "/PublicAPI.Shipped.txt", ReleasedShipped);
        repository.Write(ProjectFolder + "/PublicAPI.Unshipped.txt", "#nullable enable\n");
        repository.Write("build/gates/release-baseline.json", "{\n  \"codes\": [\n    \"messaging.send.unknown\"\n  ]\n}\n");
    }

    private static Task<string> CommitReleaseAsync(GitRepository repository, CancellationToken cancellationToken)
    {
        return CommitReleaseAsync(repository, FinalRelease, cancellationToken);
    }

    private static Task<string> CommitReleaseAsync(GitRepository repository, string version, CancellationToken cancellationToken)
    {
        return repository.CommitAsync("chore: release " + version, cancellationToken, "--date", ReleaseDate);
    }

    private static Task<ProcessResult> PreviousReleaseTagAsync(GitRepository repository, string head, CancellationToken cancellationToken)
    {
        return Processes.GateAsync("previous-release-tag.ps1", repository.Root, cancellationToken, "-Repository", repository.Root, "-Head", head);
    }

    private static Task<ProcessResult> RunAsync(GitRepository repository, string start, CancellationToken cancellationToken)
    {
        return Processes.GateAsync("gate-commit-messages.ps1", repository.Root, cancellationToken, "-Repository", repository.Root, "-Base", start, "-Head", "HEAD");
    }
}