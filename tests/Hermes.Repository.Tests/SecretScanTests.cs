using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace Hermes.Repository.Tests;

/// <remarks>
/// The credential-shaped string is drawn at run time and written only in a repository the
/// test makes under <c>tmp/</c>, so none is ever written in this repository's history.
/// </remarks>
[Trait("Category", "integration")]
public sealed class SecretScanTests
{
    private const string Alphanumeric = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    [Fact]
    public async Task MSG_THR_004_AC1_ACommitCarryingACredentialShapedStringFailsTheScan()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await GitRepository.CreateAsync(folder, cancellationToken);
        repository.Write("README.md", "A repository with no secret.\n");
        await repository.CommitAsync("docs: add the readme", cancellationToken);

        // The shape of a GitHub personal access token, which gitleaks' default finds.
        string token = "ghp" + "_" + RandomNumberGenerator.GetString(Alphanumeric, 36);
        repository.Write("settings.txt", $"token = {token}\n");
        await repository.CommitAsync("chore: add the settings", cancellationToken);

        ProcessResult scan = await Processes.GateAsync("gate-secret-scan.ps1", repository.Root, cancellationToken, "-Repository", repository.Root);

        Assert.NotEqual(0, scan.ExitCode);
        Assert.Contains("leaks found", scan.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(token, scan.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MSG_THR_004_AC1_ACredentialShapedStringRemovedLaterStillFailsTheScan()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await GitRepository.CreateAsync(folder, cancellationToken);
        string token = "ghp" + "_" + RandomNumberGenerator.GetString(Alphanumeric, 36);
        repository.Write("settings.txt", $"token = {token}\n");
        await repository.CommitAsync("chore: add the settings", cancellationToken);
        repository.Write("settings.txt", "token = (read from the secret source)\n");
        await repository.CommitAsync("fix: read the token from the secret source", cancellationToken);

        ProcessResult scan = await Processes.GateAsync("gate-secret-scan.ps1", repository.Root, cancellationToken, "-Repository", repository.Root);

        Assert.NotEqual(0, scan.ExitCode);
    }

    [Fact]
    public async Task SecretScan_HistoryWithoutSecrets_Passes()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        GitRepository repository = await GitRepository.CreateAsync(folder, cancellationToken);
        repository.Write("README.md", "A repository with no secret.\n");
        await repository.CommitAsync("docs: add the readme", cancellationToken);

        ProcessResult scan = await Processes.GateAsync("gate-secret-scan.ps1", repository.Root, cancellationToken, "-Repository", repository.Root);

        Assert.True(scan.ExitCode == 0, scan.Output);
    }
}