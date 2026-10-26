using System;
using System.IO;

using Xunit;

namespace Hermes.Repository.Tests;

[Trait("Category", "unit")]
public sealed class SettingRuleTests
{
    private const string TestProject = "tests/Hermes.Core.Tests/Hermes.Core.Tests.csproj";

    [Fact]
    public void MSG_ENG_002_AC1_ProjectsAndSharedFilesSetOnlyWhatTheRuleAllows()
    {
        Assert.Empty(SettingRules.SettingViolations(RepositoryRoot.Path));
    }

    [Theory]
    [InlineData("<PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>", "sets TargetFramework")]
    [InlineData("<PropertyGroup><Nullable>disable</Nullable></PropertyGroup>", "sets Nullable")]
    [InlineData("<PropertyGroup><TreatWarningsAsErrors>false</TreatWarningsAsErrors></PropertyGroup>", "sets TreatWarningsAsErrors")]
    [InlineData("<PropertyGroup><MSBuildTreatWarningsAsErrors>false</MSBuildTreatWarningsAsErrors></PropertyGroup>", "sets MSBuildTreatWarningsAsErrors")]
    [InlineData("<ItemGroup><PackageReference Include=\"xunit.v3\" VersionOverride=\"3.0.0\" /></ItemGroup>", "sets the version of xunit.v3")]
    public void MSG_ENG_002_AC1_ProjectSettingWhatOnlyTheSharedFilesSetIsFound(string body, string violation)
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject(TestProject, body);

        Assert.Contains(SettingRules.SettingViolations(fixture.Root), found => found.Contains(violation, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("NoWarn")]
    [InlineData("WarningsNotAsErrors")]
    [InlineData("WarningLevel")]
    [InlineData("MSBuildWarningsAsMessages")]
    [InlineData("MSBuildWarningsNotAsErrors")]
    [InlineData("RunAnalyzers")]
    [InlineData("RunAnalyzersDuringBuild")]
    [InlineData("EnableNETAnalyzers")]
    [InlineData("CodeAnalysisRuleSet")]
    [InlineData("NuGetAuditLevel")]
    [InlineData("AnalysisMode")]
    [InlineData("AnalysisLevelDesign")]
    [InlineData("AnalysisModeSecurity")]
    public void MSG_ENG_002_AC1_ProjectPropertyThatTurnsAWarningOffIsFound(string property)
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject(TestProject, $"<PropertyGroup><{property}>x</{property}></PropertyGroup>");

        Assert.Contains(
            $"{TestProject} sets {property}, which turns a warning off or keeps it from failing the build.",
            SettingRules.SettingViolations(fixture.Root));
    }

    [Fact]
    public void MSG_ENG_002_AC1_PropertyThatTurnsAWarningOffInATargetIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject(TestProject, "<Target Name=\"Quiet\" BeforeTargets=\"Build\"><PropertyGroup><NoWarn>CS0168</NoWarn></PropertyGroup></Target>");

        Assert.Contains(
            $"{TestProject} sets NoWarn, which turns a warning off or keeps it from failing the build.",
            SettingRules.SettingViolations(fixture.Root));
    }

    [Theory]
    [InlineData("NuGetAuditSuppress", "https://github.com/advisories/GHSA-5crp-9r3c-p9vr")]
    [InlineData("EditorConfigFiles", "quiet.editorconfig")]
    [InlineData("GlobalAnalyzerConfigFiles", "quiet.globalconfig")]
    public void MSG_ENG_002_AC1_ItemThatTurnsAWarningOffIsFound(string item, string include)
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject(TestProject, $"<ItemGroup><{item} Include=\"{include}\" /></ItemGroup>");

        Assert.Contains(
            $"{TestProject} adds a {item} item, which turns a warning off or keeps it from failing the build.",
            SettingRules.SettingViolations(fixture.Root));
    }

    [Theory]
    [InlineData("<ItemGroup><PackageReference Include=\"xunit.v3\" NoWarn=\"NU1903\" /></ItemGroup>")]
    [InlineData("<ItemGroup><PackageReference Include=\"xunit.v3\"><NoWarn>NU1903</NoWarn></PackageReference></ItemGroup>")]
    public void MSG_ENG_002_AC1_ReferenceThatTurnsAWarningOffIsFound(string body)
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject(TestProject, body);

        Assert.Contains($"{TestProject} sets NoWarn on its PackageReference item xunit.v3, which turns a warning off.", SettingRules.SettingViolations(fixture.Root));
    }

    [Fact]
    public void MSG_ENG_002_AC1_SharedBuildSettingsTurningAWarningOffIsFound()
    {
        using RepositoryFixture fixture = new();
        string props = File.ReadAllText(Path.Combine(fixture.Root, "Directory.Build.props"));
        fixture.Write("Directory.Build.props", props.Replace("</Project>", "  <PropertyGroup><WarningsNotAsErrors>CS0168</WarningsNotAsErrors></PropertyGroup>\n</Project>", StringComparison.Ordinal));

        Assert.Contains(
            "Directory.Build.props sets WarningsNotAsErrors, which turns a warning off or keeps it from failing the build.",
            SettingRules.SettingViolations(fixture.Root));
    }

    [Fact]
    public void MSG_ENG_002_AC1_PackageVersionsSettingAnotherPropertyIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.Write("Directory.Packages.props", """
            <Project>
              <PropertyGroup>
                <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
              </PropertyGroup>
              <ItemGroup>
                <PackageVersion Include="xunit.v3" Version="4.0.2" NoWarn="NU1903" />
              </ItemGroup>
            </Project>
            """);

        string[] violations = [.. SettingRules.SettingViolations(fixture.Root)];

        Assert.Contains("Directory.Packages.props sets CentralPackageTransitivePinningEnabled; it sets no property but ManagePackageVersionsCentrally.", violations);
        Assert.Contains("Directory.Packages.props sets NoWarn on its PackageVersion item xunit.v3, which turns a warning off.", violations);
    }

    [Theory]
    [InlineData("src/Hermes.Core/.editorconfig")]
    [InlineData(".globalconfig")]
    [InlineData("tests/Hermes.Core.Tests/quiet.globalconfig")]
    [InlineData("artifacts/.editorconfig")]
    public void MSG_ENG_002_AC1_AnalyserConfigurationBesideTheRootEditorConfigIsFound(string path)
    {
        using RepositoryFixture fixture = new();
        fixture.Write(path, "[*.cs]\ndotnet_diagnostic.CS0168.severity = none\n");

        Assert.Contains($"{path} configures the analysers; only the root .editorconfig does.", SettingRules.SettingViolations(fixture.Root));
    }

    [Theory]
    [InlineData(".editorconfig")]
    [InlineData("tmp/.editorconfig")]
    [InlineData("src/Hermes.Core/bin/.editorconfig")]
    [InlineData("src/Hermes.Core/obj/quiet.globalconfig")]
    public void MSG_ENG_002_AC1_RootEditorConfigTmpAndBuildOutputAreLeftOut(string path)
    {
        using RepositoryFixture fixture = new();
        fixture.Write(path, "[*.cs]\ndotnet_diagnostic.CS0168.severity = none\n");

        Assert.Empty(SettingRules.SettingViolations(fixture.Root));
    }

    [Fact]
    public void SettingViolations_ProjectWithoutItsLockfile_IsFound()
    {
        using RepositoryFixture fixture = new();
        File.Delete(Path.Combine(fixture.Root, "src", "Hermes.Core", "packages.lock.json"));

        Assert.Contains("src/Hermes.Core/Hermes.Core.csproj has no packages.lock.json.", SettingRules.SettingViolations(fixture.Root));
    }
}