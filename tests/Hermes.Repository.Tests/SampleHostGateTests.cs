using System;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace Hermes.Repository.Tests;

/// <summary>The sample host's gate: the host built from the packages packed from this commit, and its tests run (<c>07</c> MSG-ENG-001, MSG-ENG-012).</summary>
/// <remarks>The gate packs and builds under <c>tmp/sample-host/</c> and <c>artifacts/packages/</c>, and the sample host's tests start PostgreSQL containers, so these tests need Docker, as the integration tests do.</remarks>
[Trait("Category", "integration")]
public sealed class SampleHostGateTests
{
    [Fact]
    public async Task MSG_ENG_001_AC2_TheSampleHostBuildsFromThePackagesPackedFromThisCommitAndItsTestsPass()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        ProcessResult run = await Processes.GateAsync("gate-sample-host.ps1", RepositoryRoot.Path, cancellationToken);

        Assert.True(run.ExitCode == 0, run.Output);
        Assert.Contains("The sample host built from the packages packed from this commit, and its tests passed.", run.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GateSampleHost_RepositoryWithoutTheSampleSolution_Fails()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();

        ProcessResult run = await Processes.GateAsync("gate-sample-host.ps1", folder.Path, cancellationToken, "-Repository", folder.Path);

        Assert.NotEqual(0, run.ExitCode);
        Assert.Contains("samples/Samples.sln is missing.", run.Output, StringComparison.Ordinal);
    }
}