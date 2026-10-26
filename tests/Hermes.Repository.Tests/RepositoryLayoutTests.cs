using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Xunit;

namespace Hermes.Repository.Tests;

[Trait("Category", "unit")]
public sealed class RepositoryLayoutTests
{
    private static readonly IReadOnlyList<string> RootFiles =
        ["Hermes.sln", "Directory.Build.props", "Directory.Packages.props", "global.json", ".editorconfig", ".gitignore", "nuget.config", "CHANGELOG.md", ".config/dotnet-tools.json"];

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
        File.Delete(Path.Combine(fixture.Root, "tests", "Hermes.Testing.Tests", "Hermes.Testing.Tests.csproj"));

        Assert.Contains($"src/{Packages.Testing} has no test project tests/{Packages.Testing}.Tests.", LayoutRules.ProjectViolations(fixture.Root));
    }

    [Theory]
    [InlineData(Packages.Engine)]
    [InlineData(Packages.Testing)]
    [InlineData(Packages.Janus)]
    public void MSG_ENG_001_AC1_PackageWithoutItsProjectIsFound(string package)
    {
        using RepositoryFixture fixture = new();
        File.Delete(Path.Combine(fixture.Root, "src", package, package + ".csproj"));

        Assert.Contains($"src/{package} is missing, though {package} is a package of 00 section 2.2.", LayoutRules.ProjectViolations(fixture.Root));
    }

    [Fact]
    public void MSG_ENG_001_AC1_CommandLinePackageIsNotYetRequired()
    {
        using RepositoryFixture fixture = new();

        Assert.DoesNotContain(LayoutRules.ProjectViolations(fixture.Root), violation => violation.Contains(Packages.Cli, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Other.sln")]
    [InlineData("Hermes.slnx")]
    [InlineData("tests/Tests.sln")]
    [InlineData("samples/Other.sln")]
    public void MSG_ENG_001_AC1_SolutionOtherThanTheTwoIsFound(string path)
    {
        using RepositoryFixture fixture = new();
        fixture.Write(path, string.Empty);

        Assert.Contains($"{path} is a solution other than Hermes.sln and samples/Samples.sln.", LayoutRules.ProjectViolations(fixture.Root));
    }

    [Fact]
    public void MSG_ENG_001_AC1_SampleSolutionIsAccepted()
    {
        using RepositoryFixture fixture = new();
        fixture.Write("samples/Samples.sln", string.Empty);

        Assert.Empty(LayoutRules.ProjectViolations(fixture.Root));
    }

    [Theory]
    [InlineData("public sealed class Message;\n")]
    [InlineData("// namespace Hermes.Core;\n\npublic sealed class Message;\n")]
    [InlineData("[assembly: System.CLSCompliant(false)]\n")]
    public void MSG_ENG_001_AC1_FileWithoutANamespaceIsFound(string source)
    {
        using RepositoryFixture fixture = new();
        fixture.Write("src/Hermes.Core/Message.cs", source);

        Assert.Contains("src/Hermes.Core/Message.cs declares no namespace.", LayoutRules.FolderViolations(fixture.Root));
    }

    [Theory]
    [InlineData("namespace Hermes.Core\n{\n    public sealed class One;\n\n    public sealed class Two;\n}\n")]
    [InlineData("namespace Hermes.Core\n{\n    /// <summary>One.</summary>\n    public sealed class One\n    {\n    }\n\n    public enum Two\n    {\n        First,\n    }\n}\n")]
    public void MSG_ENG_001_AC1_TwoPublicTypesInABlockScopedNamespaceAreFound(string source)
    {
        using RepositoryFixture fixture = new();
        fixture.Write("src/Hermes.Core/Pair.cs", source);

        Assert.Contains("src/Hermes.Core/Pair.cs declares more than one public top-level type.", LayoutRules.FolderViolations(fixture.Root));
    }

    [Theory]
    [InlineData("namespace Hermes.Core;\n\npublic abstract record Attempt\n{\n    public sealed record Accepted : Attempt;\n\n    public sealed record Gone : Attempt;\n}\n")]
    [InlineData("namespace Hermes.Core\n{\n    public abstract record Attempt\n    {\n        public sealed record Accepted : Attempt;\n    }\n}\n")]
    [InlineData("namespace Hermes.Core;\n\n// public sealed class Commented;\n\npublic sealed class One\n{\n    private const string Text = \"public sealed class Quoted\";\n}\n")]
    public void MSG_ENG_001_AC1_TypeWithItsNestedCasesIsOneTopLevelType(string source)
    {
        using RepositoryFixture fixture = new();
        fixture.Write("src/Hermes.Core/Attempt.cs", source);

        Assert.Empty(LayoutRules.FolderViolations(fixture.Root));
    }

    [Fact]
    public void MSG_ENG_001_AC1_PackageSourcesFollowTheRule()
    {
        Assert.Empty(LayoutRules.PackageSourceViolations(RepositoryRoot.Path));
    }

    [Theory]
    [InlineData("<packageSources><add key=\"nuget.org\" value=\"https://api.nuget.org/v3/index.json\" /></packageSources>", "nuget.config does not clear the package sources first.")]
    [InlineData("<packageSources><clear /><add key=\"nuget.org\" value=\"https://api.nuget.org/v3/index.json\" /><add key=\"local\" value=\"artifacts/packages\" /></packageSources>", "does not add nuget.org, https://api.nuget.org/v3/index.json, as its one source.")]
    [InlineData("<packageSources><clear /><add key=\"nuget.org\" value=\"https://example.invalid/v3/index.json\" /></packageSources>", "does not add nuget.org, https://api.nuget.org/v3/index.json, as its one source.")]
    public void MSG_ENG_001_AC1_PackageSourcesOutsideTheRuleAreFound(string sources, string expected)
    {
        using RepositoryFixture fixture = new();
        fixture.Write("nuget.config", $"""
            <configuration>
              {sources}
              <packageSourceMapping><packageSource key="nuget.org"><package pattern="*" /></packageSource></packageSourceMapping>
            </configuration>
            """);

        Assert.Contains(LayoutRules.PackageSourceViolations(fixture.Root), violation => violation.EndsWith(expected, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData("<packageSourceMapping><packageSource key=\"nuget.org\"><package pattern=\"Microsoft.*\" /></packageSource></packageSourceMapping>")]
    [InlineData("<packageSourceMapping><packageSource key=\"nuget.org\"><package pattern=\"*\" /></packageSource><packageSource key=\"other\"><package pattern=\"Hermes.*\" /></packageSource></packageSourceMapping>")]
    public void MSG_ENG_001_AC1_PackageSourceMappingOutsideTheRuleIsFound(string mapping)
    {
        using RepositoryFixture fixture = new();
        fixture.Write("nuget.config", $"""
            <configuration>
              <packageSources><clear /><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources>
              {mapping}
            </configuration>
            """);

        Assert.Contains("nuget.config does not map every package identifier, \"*\", to nuget.org alone.", LayoutRules.PackageSourceViolations(fixture.Root));
    }

    [Fact]
    public void MSG_ENG_001_AC1_IgnoreFileListsThePackedPackages()
    {
        Assert.Empty(LayoutRules.IgnoreViolations(RepositoryRoot.Path));
    }

    [Fact]
    public void MSG_ENG_001_AC1_IgnoreFileWithoutThePackedPackagesIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.Write(".gitignore", "bin/\nobj/\n# /artifacts/packages/\n");

        Assert.Contains(".gitignore does not list artifacts/packages/.", LayoutRules.IgnoreViolations(fixture.Root));
    }

    [Fact]
    public void MSG_ENG_001_AC1_RootOfTheListedFilesIsAccepted()
    {
        Assert.Empty(LayoutRules.RootFileViolations([.. RootFiles, "RELEASE-NOTES.md", "src/Hermes.Core/Hermes.Core.csproj", "docs/spec/00-overview.md", ".github/workflows/gates.yml"]));
    }

    [Theory]
    [InlineData("README.md", "README.md sits at the repository's root, which holds only the files MSG-ENG-001 lists.")]
    [InlineData("tools/run.ps1", "tools/ is a folder at the repository's root that MSG-ENG-001 does not list.")]
    [InlineData(".config/other.json", ".config/other.json sits in .config/, which holds only the tool manifest.")]
    public void MSG_ENG_001_AC1_RootFileOutsideTheListIsFound(string path, string expected)
    {
        Assert.Contains(expected, LayoutRules.RootFileViolations([.. RootFiles, path]));
    }

    [Theory]
    [InlineData("CHANGELOG.md")]
    [InlineData("nuget.config")]
    [InlineData(".config/dotnet-tools.json")]
    public void MSG_ENG_001_AC1_ListedRootFileMissingIsFound(string missing)
    {
        Assert.Contains($"{missing} is missing from the repository's root.", LayoutRules.RootFileViolations([.. RootFiles.Where(file => !string.Equals(file, missing, StringComparison.Ordinal))]));
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
    public void RepositoryFixture_AsMade_KeepsEveryRule()
    {
        using RepositoryFixture fixture = new();

        Assert.Empty(LayoutRules.ProjectViolations(fixture.Root));
        Assert.Empty(LayoutRules.FolderViolations(fixture.Root));
        Assert.Empty(SettingRules.SettingViolations(fixture.Root));
        Assert.Empty(PackageRules.AllowListViolations(fixture.Root));
        Assert.Empty(PackageRules.MockingViolations(fixture.Root));
        Assert.Empty(ReferenceRules.ReferenceViolations(fixture.Root));
        Assert.Empty(ReferenceRules.ChannelsViolations(fixture.Root));
        Assert.Empty(ScannerConfigurationRules.Violations(fixture.Root));
    }
}