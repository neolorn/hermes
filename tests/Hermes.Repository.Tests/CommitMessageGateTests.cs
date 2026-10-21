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
    }

    [Theory]
    [InlineData("add the shared build settings")]
    [InlineData("feature: add the shared build settings")]
    [InlineData("build(api): add the shared build settings")]
    [InlineData("build: Add the shared build settings")]
    [InlineData("build: add the IMessages interface")]
    [InlineData("build: add the shared build settings.")]
    [InlineData("build:add the shared build settings")]
    [InlineData("build: add the shared build settings that every project of the solution takes")]
    [InlineData("build: add the shared build settings\nsecond line")]
    [InlineData("build: add the shared build settings\n\nThe settings are shared.")]
    [InlineData("build: add the shared build settings\n\n- a fragment that runs on for longer than the seventy-two characters allowed")]
    [InlineData("build: add the shared build settings\n\nCo-Authored-By: Someone <someone@example.invalid>")]
    [InlineData("build: add the shared build settings\n\nSigned-off-by: Someone <someone@example.invalid>")]
    [InlineData("build: add the shared build settings\n\nImplements: MSG-SEND-6")]
    [InlineData("build: add the shared build settings\n\nImplements: MSG-SEND-006\nReviewed-by: Someone")]
    public async Task MSG_ENG_011_AC1_CommitMessageOutsideTheRuleFails(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateAsync(folder, cancellationToken);
        await repository.CommitAsync(message, cancellationToken);

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.NotEqual(0, gate.ExitCode);
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
    }

    [Fact]
    public async Task MSG_ENG_011_AC1_MergeCommitsAreNotChecked()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        (GitRepository repository, string start) = await CreateAsync(folder, cancellationToken);
        await repository.GitAsync(cancellationToken, "switch", "--quiet", "--create", "phase-01");
        await repository.CommitAsync("build: add the shared build settings", cancellationToken);
        await repository.GitAsync(cancellationToken, "switch", "--quiet", "main");
        await repository.GitAsync(cancellationToken, "merge", "--quiet", "--no-ff", "--no-edit", "-m", "Merge pull request #1 from owner/phase-01", "phase-01");

        ProcessResult gate = await RunAsync(repository, start, cancellationToken);

        Assert.True(gate.ExitCode == 0, gate.Output);
    }

    private static async Task<(GitRepository Repository, string Start)> CreateAsync(ScratchFolder folder, CancellationToken cancellationToken)
    {
        GitRepository repository = await GitRepository.CreateAsync(folder, cancellationToken);
        repository.Write("CHANGELOG.md", Changelog);
        string start = await repository.CommitAsync("docs: add the changelog", cancellationToken);
        return (repository, start);
    }

    private static Task<ProcessResult> RunAsync(GitRepository repository, string start, CancellationToken cancellationToken)
    {
        return Processes.GateAsync("gate-commit-messages.ps1", repository.Root, cancellationToken, "-Repository", repository.Root, "-Base", start, "-Head", "HEAD");
    }
}