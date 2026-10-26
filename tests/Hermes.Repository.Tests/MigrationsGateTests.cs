using System;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace Hermes.Repository.Tests;

/// <remarks>The gate starts a PostgreSQL container of its own, so these tests need Docker, as the integration tests do.</remarks>
[Trait("Category", "integration")]
public sealed class MigrationsGateTests
{
    [Fact]
    public async Task GateMigrations_ThisRepository_AppliesTheMigrationsThenNothing()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        ProcessResult run = await Processes.GateAsync("gate-migrations.ps1", RepositoryRoot.Path, cancellationToken, "-NoBuild");

        Assert.True(run.ExitCode == 0, run.Output);
        Assert.Contains("The first run applied 1 migration(s): 20261026082731_Foundations.", run.Output, StringComparison.Ordinal);
        Assert.Contains("The second run applied nothing.", run.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GateMigrations_RepositoryWithoutTheTool_Fails()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();

        ProcessResult run = await Processes.GateAsync("gate-migrations.ps1", folder.Path, cancellationToken, "-Repository", folder.Path, "-NoBuild");

        Assert.NotEqual(0, run.ExitCode);
        Assert.Contains("src/Hermes.Cli/Hermes.Cli.csproj is missing.", run.Output, StringComparison.Ordinal);
    }
}