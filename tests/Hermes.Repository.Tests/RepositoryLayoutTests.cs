using System;
using System.IO;

using Xunit;

namespace Hermes.Repository.Tests;

[Trait("Category", "unit")]
public sealed class RepositoryLayoutTests
{
    [Fact]
    public void MSG_ENG_001_AC1_ProjectsFollowTheLayout()
    {
        Assert.Empty(LayoutRules.ProjectViolations(RepositoryRoot.Path));
    }

    [Fact]
    public void MSG_ENG_001_AC1_ProjectFoldersFollowTheLayout()
    {
        Assert.Empty(LayoutRules.FolderViolations(RepositoryRoot.Path));
    }

    [Fact]
    public void MSG_ENG_001_AC1_ProjectOutsideTheLayoutIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject("src/Hermes.Extras/Hermes.Extras.csproj", string.Empty);

        Assert.Contains(LayoutRules.ProjectViolations(fixture.Root), violation => violation.StartsWith("src/Hermes.Extras/Hermes.Extras.csproj is not", StringComparison.Ordinal));
    }

    [Fact]
    public void MSG_ENG_001_AC1_SourceProjectWithoutItsTestProjectIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteSource(Packages.Testing, [Packages.Core]);

        Assert.Contains($"src/{Packages.Testing} has no test project tests/{Packages.Testing}.Tests.", LayoutRules.ProjectViolations(fixture.Root));
    }

    [Fact]
    public void MSG_ENG_001_AC1_ProjectTheSolutionDoesNotListIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteSolution(["src/Hermes.Core/Hermes.Core.csproj"]);

        Assert.Contains("Hermes.sln does not list src/Hermes.Channels/Hermes.Channels.csproj.", LayoutRules.ProjectViolations(fixture.Root));
    }

    [Fact]
    public void MSG_ENG_001_AC1_FolderOutsideTheLayoutIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.Write("src/Hermes.Core/Sending/Send.cs", "namespace Hermes.Core.Sending;\n");

        Assert.Contains("src/Hermes.Core/Hermes.Core.csproj: the folder Sending is not one the project may hold.", LayoutRules.FolderViolations(fixture.Root));
    }

    [Fact]
    public void MSG_ENG_001_AC1_NamespaceThatDoesNotMatchItsFolderIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.Write("src/Hermes.Channels/Email/Message.cs", "namespace Hermes.Channels;\n");

        Assert.Contains("src/Hermes.Channels/Email/Message.cs declares the namespace Hermes.Channels, not Hermes.Channels.Email.", LayoutRules.FolderViolations(fixture.Root));
    }

    [Fact]
    public void MSG_ENG_001_AC1_PublicTypeOutsideTheProjectsRootIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.Write("src/Hermes.Channels/Email/Message.cs", "namespace Hermes.Channels.Email;\n\npublic sealed class Message;\n");

        Assert.Contains("src/Hermes.Channels/Email/Message.cs declares a public type outside the project's root folder.", LayoutRules.FolderViolations(fixture.Root));
    }

    [Fact]
    public void MSG_ENG_001_AC1_TwoPublicTypesInOneFileAreFound()
    {
        using RepositoryFixture fixture = new();
        fixture.Write("src/Hermes.Core/Pair.cs", "namespace Hermes.Core;\n\npublic sealed class One;\n\npublic sealed class Two;\n");

        Assert.Contains("src/Hermes.Core/Pair.cs declares more than one public top-level type.", LayoutRules.FolderViolations(fixture.Root));
    }

    [Fact]
    public void MSG_ENG_002_AC1_NoProjectSetsATargetFrameworkAListedPropertyOrAPackageVersion()
    {
        Assert.Empty(PackageRules.SettingViolations(RepositoryRoot.Path));
    }

    [Theory]
    [InlineData("<PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>", "sets TargetFramework")]
    [InlineData("<PropertyGroup><Nullable>disable</Nullable></PropertyGroup>", "sets Nullable")]
    [InlineData("<PropertyGroup><TreatWarningsAsErrors>false</TreatWarningsAsErrors></PropertyGroup>", "sets TreatWarningsAsErrors")]
    [InlineData("<ItemGroup><PackageReference Include=\"xunit.v3\" VersionOverride=\"3.0.0\" /></ItemGroup>", "sets the version of xunit.v3")]
    public void MSG_ENG_002_AC1_ProjectSettingWhatOnlyTheSharedFilesSetIsFound(string body, string violation)
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject("tests/Hermes.Core.Tests/Hermes.Core.Tests.csproj", body);

        Assert.Contains(PackageRules.SettingViolations(fixture.Root), found => found.Contains(violation, StringComparison.Ordinal));
    }

    [Fact]
    public void MSG_ENG_002_AC1_ProjectWithoutItsLockfileIsFound()
    {
        using RepositoryFixture fixture = new();
        File.Delete(Path.Combine(fixture.Root, "src", "Hermes.Core", "packages.lock.json"));

        Assert.Contains("src/Hermes.Core/Hermes.Core.csproj has no packages.lock.json.", PackageRules.SettingViolations(fixture.Root));
    }

    [Fact]
    public void RepositoryFixture_AsMade_KeepsEveryRule()
    {
        using RepositoryFixture fixture = new();

        Assert.Empty(LayoutRules.ProjectViolations(fixture.Root));
        Assert.Empty(LayoutRules.FolderViolations(fixture.Root));
        Assert.Empty(PackageRules.SettingViolations(fixture.Root));
        Assert.Empty(PackageRules.AllowListViolations(fixture.Root));
        Assert.Empty(PackageRules.MockingViolations(fixture.Root));
        Assert.Empty(ReferenceRules.ReferenceViolations(fixture.Root));
        Assert.Empty(ReferenceRules.ChannelsViolations(fixture.Root));
        Assert.Empty(ReferenceRules.GrantViolations(fixture.Root));
        Assert.Empty(ScannerConfigurationRules.Violations(fixture.Root));
    }
}