using System;
using System.IO;

using Xunit;

namespace Hermes.Repository.Tests;

[Trait("Category", "unit")]
public sealed class PackageAllowListTests
{
    [Fact]
    public void MSG_ENG_009_AC1_PackagesAndToolsAreWithinTheTable()
    {
        Assert.Empty(PackageRules.AllowListViolations(RepositoryRoot.Path));
    }

    [Fact]
    public void MSG_ENG_009_AC1_PackageOutsideTheTableIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject("tests/Hermes.Core.Tests/Hermes.Core.Tests.csproj", """
              <ItemGroup>
                <PackageReference Include="xunit.v3" />
                <PackageReference Include="Shouldly" />
              </ItemGroup>
            """);

        Assert.Contains(
            "tests/Hermes.Core.Tests/Hermes.Core.Tests.csproj references Shouldly, which is not in MSG-ENG-009's table.",
            PackageRules.AllowListViolations(fixture.Root));
    }

    [Fact]
    public void MSG_ENG_009_AC1_PackageVersionOutsideTheTableIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.Write("Directory.Packages.props", """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Polly" Version="8.0.0" />
              </ItemGroup>
            </Project>
            """);

        Assert.Contains("Directory.Packages.props names Polly, which is not in MSG-ENG-009's table.", PackageRules.AllowListViolations(fixture.Root));
    }

    [Fact]
    public void MSG_ENG_009_AC1_PinnedPackageAtAnotherVersionIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.Write("Directory.Packages.props", """
            <Project>
              <ItemGroup>
                <PackageVersion Include="MailKit" Version="4.0.0" />
              </ItemGroup>
            </Project>
            """);

        Assert.Contains("Directory.Packages.props takes MailKit 4.0.0, not the pinned 4.18.2.", PackageRules.AllowListViolations(fixture.Root));
    }

    [Fact]
    public void MSG_ENG_009_AC1_ToolOtherThanDotnetEfIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.Write(".config/dotnet-tools.json", """
            {
              "version": 1,
              "isRoot": true,
              "tools": {
                "dotnet-ef": { "version": "10.0.13", "commands": [ "dotnet-ef" ] },
                "dotnet-reportgenerator-globaltool": { "version": "5.0.0", "commands": [ "reportgenerator" ] }
              }
            }
            """);

        Assert.Contains(
            "The tool manifest names dotnet-reportgenerator-globaltool; it names dotnet-ef alone.",
            PackageRules.AllowListViolations(fixture.Root));
    }

    [Fact]
    public void MSG_ENG_009_AC1_PackageTakenByAReferenceOutsideTheProjectFileIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.Write("src/Hermes.Core/packages.lock.json", """
            { "version": 2, "dependencies": { "net10.0": { "Newtonsoft.Json": { "type": "Direct", "requested": "[13.0.3, )", "resolved": "13.0.3" } } } }
            """);

        Assert.Contains(
            "src/Hermes.Core/Hermes.Core.csproj references Newtonsoft.Json, which is not in MSG-ENG-009's table.",
            PackageRules.AllowListViolations(fixture.Root));
    }

    [Fact]
    public void MSG_ENG_009_AC1_DotnetEfAtAVersionOtherThanEntityFrameworkCoresIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.Write("Directory.Packages.props", """
            <Project>
              <PropertyGroup>
                <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
              </PropertyGroup>
              <ItemGroup>
                <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.0.14" />
              </ItemGroup>
            </Project>
            """);
        fixture.Write(".config/dotnet-tools.json", """
            {
              "version": 1,
              "isRoot": true,
              "tools": {
                "dotnet-ef": { "version": "10.0.13", "commands": [ "dotnet-ef" ] }
              }
            }
            """);

        Assert.Contains(
            "The tool manifest takes dotnet-ef 10.0.13, not the EF Core version 10.0.14.",
            PackageRules.AllowListViolations(fixture.Root));
    }

    [Fact]
    public void MSG_ENG_008_AC1_NoMockingPackageIsReferenced()
    {
        Assert.Empty(PackageRules.MockingViolations(RepositoryRoot.Path));
    }

    [Fact]
    public void MSG_ENG_008_AC1_MockingPackageTakenThroughAnotherIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.Write("tests/Hermes.Core.Tests/packages.lock.json", """
            { "version": 2, "dependencies": { "net10.0": { "NSubstitute": { "type": "Transitive", "resolved": "5.0.0" } } } }
            """);

        Assert.Contains(
            "tests/Hermes.Core.Tests/Hermes.Core.Tests.csproj takes the mocking package NSubstitute.",
            PackageRules.MockingViolations(fixture.Root));
    }
}