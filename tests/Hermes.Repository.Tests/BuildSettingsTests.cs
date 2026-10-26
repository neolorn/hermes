using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace Hermes.Repository.Tests;

[Trait("Category", "integration")]
public sealed class BuildSettingsTests
{
    private const string Clean = """
        namespace Probe;

        /// <summary>A probe of the shared settings.</summary>
        public static class Numbers
        {
            /// <summary>Answers one.</summary>
            /// <returns>One.</returns>
            public static int One()
            {
                return 1;
            }
        }
        """;

    [Fact]
    public async Task MSG_ENG_002_AC2_AWarningFailsTheBuild()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        ProbeProject probe = new(folder, "Probe");
        probe.WriteProject(string.Empty);
        probe.WriteSource("Numbers.cs", Clean);

        ProcessResult clean = await probe.BuildAsync(cancellationToken);
        probe.WriteSource("Numbers.cs", Clean.Replace("return 1;", "int unused;\n        return 1;", StringComparison.Ordinal));
        ProcessResult warned = await probe.BuildAsync(cancellationToken);

        Assert.True(clean.ExitCode == 0, clean.Output);
        Assert.NotEqual(0, warned.ExitCode);
        Assert.Contains("error CS0168", warned.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MSG_ENG_002_AC2_AnMSBuildWarningFailsTheBuild()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        ProbeProject probe = new(folder, "Probe");
        probe.WriteSource("Numbers.cs", Clean);
        probe.WriteProject(string.Empty);

        ProcessResult clean = await probe.BuildAsync(cancellationToken);
        probe.WriteProject("""
              <Target Name="Warn" BeforeTargets="Build">
                <Warning Code="PROBE001" Text="A warning MSBuild raises." />
              </Target>
            """);
        ProcessResult warned = await probe.BuildAsync(cancellationToken);

        Assert.True(clean.ExitCode == 0, clean.Output);
        Assert.NotEqual(0, warned.ExitCode);
        Assert.Contains("error PROBE001: A warning MSBuild raises.", warned.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MSG_ENG_002_AC2_ALockedRestoreThatDiffersFromTheLockfileFails()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        ProbeProject probe = new(folder, "Probe");
        probe.WriteProject("""
              <ItemGroup>
                <PackageReference Include="MinVer" PrivateAssets="all" />
              </ItemGroup>
            """);

        ProcessResult locked = await probe.RestoreAsync(cancellationToken);
        ProcessResult same = await probe.RestoreAsync(cancellationToken, "--locked-mode", "--force");
        probe.WriteProject("""
              <ItemGroup>
                <PackageReference Include="MinVer" PrivateAssets="all" />
                <PackageReference Include="Microsoft.CodeAnalysis.PublicApiAnalyzers" PrivateAssets="all" />
              </ItemGroup>
            """);
        ProcessResult differs = await probe.RestoreAsync(cancellationToken, "--locked-mode", "--force");

        Assert.True(locked.ExitCode == 0, locked.Output);
        Assert.True(File.Exists(Path.Combine(probe.Folder, "packages.lock.json")), "The restore wrote no lockfile.");
        Assert.True(same.ExitCode == 0, same.Output);
        Assert.NotEqual(0, differs.ExitCode);
        Assert.Contains("NU1004", differs.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MSG_ENG_010_AC1_ARestoreResolvingAPackageWithAPublishedAdvisoryFailsTheBuild()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        ProbeProject probe = new(folder, "Probe");

        // Newtonsoft.Json 12.0.3 carries the published advisory GHSA-5crp-9r3c-p9vr (high).
        // The probe sets its own version, since the package is in no table of the repository.
        probe.WriteProject("""
              <PropertyGroup>
                <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="12.0.3" />
              </ItemGroup>
            """);

        ProcessResult restore = await probe.RestoreAsync(cancellationToken);

        Assert.NotEqual(0, restore.ExitCode);
        Assert.Contains("error NU1903", restore.Output, StringComparison.Ordinal);
    }
}