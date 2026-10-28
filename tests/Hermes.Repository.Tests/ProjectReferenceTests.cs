using System;
using System.IO;

using Xunit;

namespace Hermes.Repository.Tests;

[Trait("Category", "unit")]
public sealed class ProjectReferenceTests
{
    [Fact]
    public void MSG_LIB_001_AC1_EachProjectReferencesOnlyWhatItsRowAllows()
    {
        Assert.Empty(ReferenceRules.ReferenceViolations(RepositoryRoot.Path));
    }

    [Fact]
    public void MSG_LIB_001_AC1_ProjectReferenceOutsideTheTableIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteSource(Packages.Core, [Packages.Channels]);

        Assert.Contains(
            "src/Hermes.Core/Hermes.Core.csproj references the project Hermes.Channels, which MSG-LIB-001 does not allow it.",
            ReferenceRules.ReferenceViolations(fixture.Root));
    }

    [Fact]
    public void MSG_LIB_001_AC1_PackageReferenceOutsideTheTableIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject("src/Hermes.Channels/Hermes.Channels.csproj", """
              <ItemGroup>
                <PackageReference Include="Microsoft.CodeAnalysis.PublicApiAnalyzers" PrivateAssets="all" />
                <PackageReference Include="MinVer" PrivateAssets="all" />
                <PackageReference Include="Fluid.Core" />
              </ItemGroup>
            """);

        Assert.Contains(
            "src/Hermes.Channels/Hermes.Channels.csproj references the package Fluid.Core, which MSG-LIB-001 does not allow it.",
            ReferenceRules.ReferenceViolations(fixture.Root));
    }

    [Fact]
    public void MSG_LIB_001_AC1_ShippedProjectWithoutItsBuildOnlyReferencesIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject("src/Hermes.Core/Hermes.Core.csproj", """
              <ItemGroup>
                <PackageReference Include="MinVer" />
              </ItemGroup>
            """);

        string[] violations = [.. ReferenceRules.ReferenceViolations(fixture.Root)];

        Assert.Contains("src/Hermes.Core/Hermes.Core.csproj references MinVer without PrivateAssets=\"all\", though it is build-only.", violations);
        Assert.Contains("src/Hermes.Core/Hermes.Core.csproj does not reference Microsoft.CodeAnalysis.PublicApiAnalyzers as a build-only reference.", violations);
    }

    [Fact]
    public void MSG_LIB_001_AC1_CoreReferencingAspNetCoreIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject("src/Hermes.Core/Hermes.Core.csproj", """
              <ItemGroup>
                <PackageReference Include="Microsoft.CodeAnalysis.PublicApiAnalyzers" PrivateAssets="all" />
                <PackageReference Include="MinVer" PrivateAssets="all" />
                <FrameworkReference Include="Microsoft.AspNetCore.App" />
              </ItemGroup>
            """);

        Assert.Contains("src/Hermes.Core/Hermes.Core.csproj references Microsoft.AspNetCore.App, which it never may.", ReferenceRules.ReferenceViolations(fixture.Root));
    }

    [Fact]
    public void MSG_LIB_001_AC1_FrameworkOtherThanAspNetCoreIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject("src/Hermes.Channels/Hermes.Channels.csproj", """
              <ItemGroup>
                <PackageReference Include="Microsoft.CodeAnalysis.PublicApiAnalyzers" PrivateAssets="all" />
                <PackageReference Include="MinVer" PrivateAssets="all" />
                <FrameworkReference Include="Microsoft.WindowsDesktop.App" />
                <ProjectReference Include="..\..\src\Hermes.Core\Hermes.Core.csproj" />
              </ItemGroup>
            """);

        Assert.Contains(
            "src/Hermes.Channels/Hermes.Channels.csproj references the framework Microsoft.WindowsDesktop.App, which MSG-LIB-001 does not allow it.",
            ReferenceRules.ReferenceViolations(fixture.Root));
    }

    [Fact]
    public void MSG_LIB_001_AC1_ProjectReachingAspNetCoreWithoutTheFrameworkIsFound()
    {
        using RepositoryFixture fixture = new();
        WriteEngine(fixture);
        fixture.WriteTest("Hermes.Tests", [Packages.Engine]);

        Assert.Contains(
            "tests/Hermes.Tests/Hermes.Tests.csproj references a project that references Microsoft.AspNetCore.App, without referencing the framework itself.",
            ReferenceRules.ReferenceViolations(fixture.Root));
    }

    [Fact]
    public void MSG_LIB_001_AC1_AspNetCoreWithoutNeedIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject("tests/Hermes.Core.Tests/Hermes.Core.Tests.csproj", """
              <ItemGroup>
                <PackageReference Include="xunit.v3" />
                <FrameworkReference Include="Microsoft.AspNetCore.App" />
                <ProjectReference Include="..\..\src\Hermes.Core\Hermes.Core.csproj" />
              </ItemGroup>
            """);

        Assert.Contains(
            "tests/Hermes.Core.Tests/Hermes.Core.Tests.csproj references Microsoft.AspNetCore.App without needing it.",
            ReferenceRules.ReferenceViolations(fixture.Root));
    }

    [Fact]
    public void MSG_LIB_001_AC1_PackageReferenceInTheSharedBuildSettingsIsFound()
    {
        using RepositoryFixture fixture = new();
        string props = File.ReadAllText(Path.Combine(fixture.Root, "Directory.Build.props"));
        fixture.Write("Directory.Build.props", props.Replace("</Project>", "  <ItemGroup><PackageReference Include=\"Fluid.Core\" /></ItemGroup>\n</Project>", StringComparison.Ordinal));
        fixture.Write("src/Hermes.Core/packages.lock.json", """
            {
              "version": 2,
              "dependencies": {
                "net10.0": {
                  "Fluid.Core": { "type": "Direct", "requested": "[2.40.0, )", "resolved": "2.40.0" },
                  "Microsoft.CodeAnalysis.PublicApiAnalyzers": { "type": "Direct", "requested": "[5.6.0, )", "resolved": "5.6.0" },
                  "MinVer": { "type": "Direct", "requested": "[8.0.0, )", "resolved": "8.0.0" }
                }
              }
            }
            """);

        string[] violations = [.. ReferenceRules.ReferenceViolations(fixture.Root)];

        Assert.Contains("Directory.Build.props adds a PackageReference item Fluid.Core; it adds no item but the CLSCompliant attribute.", violations);
        Assert.Contains("src/Hermes.Core/Hermes.Core.csproj takes the package Fluid.Core by a reference outside its project file, which MSG-LIB-001 does not allow it.", violations);
        Assert.DoesNotContain(violations, violation => violation.Contains("MinVer", StringComparison.Ordinal));
    }

    [Fact]
    public void MSG_LIB_001_AC1_GlobalPackageReferenceIsFound()
    {
        using RepositoryFixture fixture = new();
        string props = File.ReadAllText(Path.Combine(fixture.Root, "Directory.Packages.props"));
        fixture.Write("Directory.Packages.props", props.Replace("</Project>", "  <ItemGroup><GlobalPackageReference Include=\"Fluid.Core\" Version=\"2.40.0\" /></ItemGroup>\n</Project>", StringComparison.Ordinal));

        Assert.Contains(
            "Directory.Packages.props adds a GlobalPackageReference item Fluid.Core; it adds no item but PackageVersion.",
            ReferenceRules.ReferenceViolations(fixture.Root));
    }

    [Fact]
    public void MSG_LIB_001_AC1_SampleHostAndItsTestsWithinTheirRowAreAccepted()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteSample();

        Assert.DoesNotContain(ReferenceRules.ReferenceViolations(fixture.Root), violation => violation.StartsWith("samples/", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("<ProjectReference Include=\"..\\..\\src\\Hermes\\Hermes.csproj\" />", "references the project Hermes")]
    [InlineData("<PackageReference Include=\"Fluid.Core\" />", "references the package Fluid.Core")]
    [InlineData("<PackageReference Include=\"xunit.v3\" />", "references the package xunit.v3")]
    public void MSG_LIB_001_AC1_SampleHostReferenceOutsideItsRowIsFound(string reference, string found)
    {
        using RepositoryFixture fixture = new();
        fixture.WriteSample();
        fixture.Write("samples/Bakery/Bakery.csproj", $"""
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <ItemGroup>
                <PackageReference Include="Hermes" />
                {reference}
              </ItemGroup>
            </Project>
            """);

        Assert.Contains($"samples/Bakery/Bakery.csproj {found}, which MSG-LIB-001 does not allow it.", ReferenceRules.ReferenceViolations(fixture.Root));
    }

    [Fact]
    public void MSG_LIB_001_AC1_SampleTestsReferencingALibraryProjectIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteSample();
        fixture.Write("samples/Bakery.Tests/Bakery.Tests.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="xunit.v3" />
                <ProjectReference Include="..\Bakery\Bakery.csproj" />
                <ProjectReference Include="..\..\src\Hermes.Testing\Hermes.Testing.csproj" />
              </ItemGroup>
            </Project>
            """);

        Assert.Contains(
            "samples/Bakery.Tests/Bakery.Tests.csproj references the project Hermes.Testing, which MSG-LIB-001 does not allow it.",
            ReferenceRules.ReferenceViolations(fixture.Root));
    }

    [Fact]
    public void MSG_LIB_001_AC2_ChannelsReachingTheEngineThroughAProjectIsFound()
    {
        using RepositoryFixture fixture = new();
        WriteEngine(fixture);
        fixture.WriteSource(Packages.Channels, [Packages.Core, Packages.Engine]);

        Assert.Contains("Hermes.Channels reaches the engine, Hermes.", ReferenceRules.ChannelsViolations(fixture.Root));
    }

    [Fact]
    public void MSG_LIB_001_AC2_ChannelsReachingAspNetCoreThroughAProjectIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject("src/Hermes.Core/Hermes.Core.csproj", """
              <ItemGroup>
                <PackageReference Include="Microsoft.CodeAnalysis.PublicApiAnalyzers" PrivateAssets="all" />
                <PackageReference Include="MinVer" PrivateAssets="all" />
                <FrameworkReference Include="Microsoft.AspNetCore.App" />
              </ItemGroup>
            """);

        Assert.Contains("Hermes.Channels reaches ASP.NET Core through Hermes.Core.", ReferenceRules.ChannelsViolations(fixture.Root));
    }

    [Fact]
    public void MSG_LIB_001_AC2_ChannelsReferencesNeitherEntityFrameworkCoreNorAspNetCoreNorFluid()
    {
        Assert.Empty(ReferenceRules.ChannelsViolations(RepositoryRoot.Path));
    }

    [Fact]
    public void MSG_LIB_001_AC2_ChannelsTakingFluidThroughAnotherPackageIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.Write("src/Hermes.Channels/packages.lock.json", """
            { "version": 2, "dependencies": { "net10.0": { "Fluid.Core": { "type": "Transitive", "resolved": "2.40.0" } } } }
            """);

        Assert.Contains("Hermes.Channels takes the package Fluid.Core.", ReferenceRules.ChannelsViolations(fixture.Root));
    }

    /// <summary>Writes the engine's project, which references ASP.NET Core's shared framework.</summary>
    private static void WriteEngine(RepositoryFixture fixture)
    {
        fixture.WriteProject("src/Hermes/Hermes.csproj", """
              <ItemGroup>
                <PackageReference Include="Microsoft.CodeAnalysis.PublicApiAnalyzers" PrivateAssets="all" />
                <PackageReference Include="MinVer" PrivateAssets="all" />
                <FrameworkReference Include="Microsoft.AspNetCore.App" />
                <ProjectReference Include="..\..\src\Hermes.Core\Hermes.Core.csproj" />
              </ItemGroup>
            """);
    }

    [Theory]
    [InlineData(Packages.Core, Packages.Engine)]
    [InlineData(Packages.Core, Packages.Testing)]
    [InlineData(Packages.Channels, "Hermes.Extra")]
    [InlineData(Packages.Channels, Packages.Cli)]
    public void MSG_ENG_001_AC1_GrantOutsideTheRuleIsFound(string grantor, string grantee)
    {
        string[] violations = [.. ReferenceRules.GrantViolations($"src/{grantor}/{grantor}.csproj", grantor, [grantee])];

        Assert.Equal([$"src/{grantor}/{grantor}.csproj grants its internals to {grantee}, which MSG-ENG-001 does not allow."], violations);
    }

    [Fact]
    public void MSG_ENG_001_AC1_GrantOfATestProjectIsFound()
    {
        string[] violations = [.. ReferenceRules.GrantViolations("tests/Hermes.Core.Tests/Hermes.Core.Tests.csproj", "Hermes.Core.Tests", [Packages.Core])];

        Assert.Equal(["tests/Hermes.Core.Tests/Hermes.Core.Tests.csproj grants its internals to Hermes.Core, which MSG-ENG-001 does not allow."], violations);
    }

    [Theory]
    [InlineData(Packages.Core, "Hermes.Core.Tests")]
    [InlineData(Packages.Channels, Packages.Engine)]
    [InlineData(Packages.Channels, Packages.Testing)]
    public void MSG_ENG_001_AC1_GrantWithinTheRuleIsAccepted(string grantor, string grantee)
    {
        Assert.Empty(ReferenceRules.GrantViolations($"src/{grantor}/{grantor}.csproj", grantor, [grantee]));
    }
}