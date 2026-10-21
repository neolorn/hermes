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

    [Fact]
    public void MSG_ENG_001_AC1_GrantsFollowTheRule()
    {
        Assert.Empty(ReferenceRules.GrantViolations(RepositoryRoot.Path));
    }

    [Theory]
    [InlineData(Packages.Core, Packages.Engine)]
    [InlineData(Packages.Core, Packages.Testing)]
    [InlineData(Packages.Channels, Packages.Janus)]
    [InlineData(Packages.Channels, Packages.Cli)]
    public void MSG_ENG_001_AC1_GrantOutsideTheRuleIsFound(string grantor, string grantee)
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject($"src/{grantor}/{grantor}.csproj", $"""
              <ItemGroup>
                <PackageReference Include="Microsoft.CodeAnalysis.PublicApiAnalyzers" PrivateAssets="all" />
                <PackageReference Include="MinVer" PrivateAssets="all" />
                <InternalsVisibleTo Include="{grantee}" />
              </ItemGroup>
            """);

        Assert.Contains(
            $"src/{grantor}/{grantor}.csproj grants its internals to {grantee}, which MSG-ENG-001 does not allow.",
            ReferenceRules.GrantViolations(fixture.Root));
    }

    [Theory]
    [InlineData(Packages.Core, "Hermes.Core.Tests")]
    [InlineData(Packages.Channels, Packages.Engine)]
    [InlineData(Packages.Channels, Packages.JanusChannels)]
    [InlineData(Packages.Channels, Packages.Testing)]
    public void MSG_ENG_001_AC1_GrantWithinTheRuleIsAccepted(string grantor, string grantee)
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject($"src/{grantor}/{grantor}.csproj", $"""
              <ItemGroup>
                <PackageReference Include="Microsoft.CodeAnalysis.PublicApiAnalyzers" PrivateAssets="all" />
                <PackageReference Include="MinVer" PrivateAssets="all" />
                <InternalsVisibleTo Include="{grantee}" />
              </ItemGroup>
            """);

        Assert.Empty(ReferenceRules.GrantViolations(fixture.Root));
    }
}