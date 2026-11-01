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

        string[] migrations =
        [
            "20261026082731_Foundations",
            "20261026233241_SendAndDispatch",
            "20261027093618_Suppressions",
            "20261027094149_Preferences",
            "20261027100509_LimitPlaces",
            "20261027103924_SmsReports",
            "20261027110515_BalanceReadings",
            "20261027111631_PushSubscriptions",
            "20261031014230_InAppList",
            "20261031160450_SendIdempotencyKey",
            "20261101011311_Conversations",
            "20261101022730_LiveConnection",
            "20261101030513_SupportSide",
            "20261101040645_DeskTools",
            "20261101051157_SupportEnd",
        ];

        Assert.True(run.ExitCode == 0, run.Output);
        Assert.Contains($"The first run applied {migrations.Length} migration(s): {string.Join(", ", migrations)}.", run.Output, StringComparison.Ordinal);
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