using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

namespace Hermes.Repository.Tests;

/// <summary>
/// The third-party packages of <c>07</c> MSG-ENG-009, the local tools, and the packages no
/// test may use (MSG-ENG-008).
/// </summary>
internal static class PackageRules
{
    /// <summary>
    /// The packages of MSG-ENG-009's table. Janus's main package joins it with its source
    /// (plan, phase 6).
    /// </summary>
    internal static IReadOnlyList<string> Allowed { get; } =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.EntityFrameworkCore.Relational",
        "Npgsql.EntityFrameworkCore.PostgreSQL",
        ReferenceRules.EntityFrameworkCoreDesign,
        .. ReferenceRules.HostingAbstractions,
        "MailKit",
        "Lib.Net.Http.WebPush",
        "Fluid.Core",
        ReferenceRules.PublicApiAnalyzers,
        ReferenceRules.MinVer,
        .. ReferenceRules.TestPackages,
    ];

    /// <summary>The packages pinned at the versions whose sources the chapters were checked against.</summary>
    internal static FrozenDictionary<string, string> Pinned { get; } = new Dictionary<string, string>
    {
        ["MailKit"] = "4.18.1",
        ["Fluid.Core"] = "2.40.0",
        ["Lib.Net.Http.WebPush"] = "3.3.1",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Mocking packages, none of which a project may take, directly or not.</summary>
    internal static IReadOnlyList<string> Mocking { get; } =
        ["Moq", "NSubstitute", "FakeItEasy", "RhinoMocks", "Rhino.Mocks", "JustMock", "Telerik.JustMock", "NMock", "NMock3", "Typemock.Isolator", "Microsoft.QualityTools.Testing.Fakes"];

    /// <summary>
    /// The elements of a packed package's metadata: its identifier, version, authors, description,
    /// repository and dependencies, and the framework references the SDK writes where
    /// <c>04</c> MSG-LIB-001 has a project reference the shared framework.
    /// </summary>
    private static readonly FrozenSet<string> ManifestElements =
        FrozenSet.Create(StringComparer.Ordinal, "id", "version", "authors", "description", "repository", "dependencies", "frameworkReferences");

    /// <summary>The element <c>Hermes.Cli</c> alone carries, its package type as a .NET tool.</summary>
    private const string ToolPackageType = "packageTypes";

    /// <summary>
    /// Answers each dependency of a packed package on another of the library's packages that is
    /// not the exact version packed, <c>[v]</c> (<c>04</c> MSG-LIB-001 criterion 3).
    /// </summary>
    internal static IEnumerable<string> DependencyViolations(IReadOnlyList<PackageManifest> manifests)
    {
        Dictionary<string, string> packed = manifests.ToDictionary(manifest => manifest.Id, manifest => manifest.Version, StringComparer.Ordinal);
        foreach (PackageManifest manifest in manifests)
        {
            foreach (PackageDependency dependency in manifest.Dependencies.Where(dependency => packed.ContainsKey(dependency.Id) || Packages.All.Contains(dependency.Id, StringComparer.Ordinal)))
            {
                if (!packed.TryGetValue(dependency.Id, out string? version))
                {
                    yield return $"{manifest.Id} depends on {dependency.Id}, which was not packed with it.";
                }
                else if (!string.Equals(dependency.Version, $"[{version}]", StringComparison.Ordinal))
                {
                    yield return $"{manifest.Id} depends on {dependency.Id} at {dependency.Version}, not the exact version packed, [{version}].";
                }
            }
        }
    }

    /// <summary>
    /// Answers each property of a packed package outside <c>07</c> MSG-ENG-011: its description
    /// is its cell of <c>00</c> section 2.2, its authors and repository are the owner's, and it
    /// carries no other package property.
    /// </summary>
    internal static IEnumerable<string> ManifestViolations(PackageManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        foreach (string element in manifest.Elements.Where(element => !ManifestElements.Contains(element) && !(string.Equals(element, ToolPackageType, StringComparison.Ordinal) && string.Equals(manifest.Id, Packages.Cli, StringComparison.Ordinal))))
        {
            yield return $"{manifest.Id} carries the package property {element}, which is left unset.";
        }

        if (!string.Equals(manifest.Authors, Packages.Authors, StringComparison.Ordinal))
        {
            yield return $"{manifest.Id}'s authors are {manifest.Authors}, not {Packages.Authors}.";
        }

        if (!PackageTable.Descriptions.TryGetValue(manifest.Id, out string? description) || !string.Equals(manifest.Description, description, StringComparison.Ordinal))
        {
            yield return $"{manifest.Id}'s description is not its cell of 00 section 2.2.";
        }

        if (!string.Equals(manifest.RepositoryUrl, Packages.RepositoryUrl, StringComparison.Ordinal))
        {
            yield return $"{manifest.Id}'s repository is {manifest.RepositoryUrl}, not {Packages.RepositoryUrl}.";
        }
    }

    /// <summary>Answers each package or tool of the repository at <paramref name="root"/> outside MSG-ENG-009.</summary>
    internal static IReadOnlyList<string> AllowListViolations(string root)
    {
        List<string> violations = [];
        foreach (ProjectFile project in ProjectFile.ReadAll(root))
        {
            // A reference made outside the project file reaches its lockfile's direct entries.
            IEnumerable<string> direct = project.PackageReferences.Select(ProjectFile.Include).Union(project.LockedDirectPackages, StringComparer.Ordinal);
            foreach (string package in direct.Where(package => !Allowed.Contains(package, StringComparer.Ordinal)))
            {
                violations.Add($"{project.RelativePath} references {package}, which is not in MSG-ENG-009's table.");
            }
        }

        Dictionary<string, string> versions = PackageVersions(root);
        foreach (string package in versions.Keys.Where(package => !Allowed.Contains(package, StringComparer.Ordinal)))
        {
            violations.Add($"Directory.Packages.props names {package}, which is not in MSG-ENG-009's table.");
        }

        foreach ((string package, string version) in Pinned)
        {
            if (versions.TryGetValue(package, out string? actual) && !string.Equals(actual, version, StringComparison.Ordinal))
            {
                violations.Add($"Directory.Packages.props takes {package} {actual}, not the pinned {version}.");
            }
        }

        violations.AddRange(ToolViolations(root, versions));
        return violations;
    }

    /// <summary>Answers each mocking package a project of the repository at <paramref name="root"/> takes, directly or through another.</summary>
    internal static IReadOnlyList<string> MockingViolations(string root)
    {
        List<string> violations = [];
        foreach (ProjectFile project in ProjectFile.ReadAll(root))
        {
            IEnumerable<string> taken = project.PackageReferences.Select(ProjectFile.Include);
            string lockFile = Path.Combine(project.Folder, "packages.lock.json");
            if (File.Exists(lockFile))
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(lockFile));
                taken = taken.Concat([.. document.RootElement.GetProperty("dependencies").EnumerateObject().SelectMany(framework => framework.Value.EnumerateObject().Select(package => package.Name))]);
            }

            foreach (string package in taken.Distinct(StringComparer.OrdinalIgnoreCase).Where(package => Mocking.Contains(package, StringComparer.OrdinalIgnoreCase)))
            {
                violations.Add($"{project.RelativePath} takes the mocking package {package}.");
            }
        }

        return violations;
    }

    private static IEnumerable<string> ToolViolations(string root, Dictionary<string, string> versions)
    {
        string manifest = Path.Combine(root, ".config", "dotnet-tools.json");
        if (!File.Exists(manifest))
        {
            yield return ".config/dotnet-tools.json is missing.";
            yield break;
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifest));
        List<JsonProperty> tools = [.. document.RootElement.GetProperty("tools").EnumerateObject()];
        foreach (JsonProperty tool in tools.Where(tool => !string.Equals(tool.Name, "dotnet-ef", StringComparison.Ordinal)))
        {
            yield return $"The tool manifest names {tool.Name}; it names dotnet-ef alone.";
        }

        JsonProperty? entityFramework = tools.Cast<JsonProperty?>().FirstOrDefault(tool => string.Equals(tool!.Value.Name, "dotnet-ef", StringComparison.Ordinal));
        if (entityFramework is null)
        {
            yield return "The tool manifest does not name dotnet-ef.";
            yield break;
        }

        string? toolVersion = entityFramework.Value.Value.GetProperty("version").GetString();
        if (versions.TryGetValue("Microsoft.EntityFrameworkCore", out string? core) && !string.Equals(core, toolVersion, StringComparison.Ordinal))
        {
            yield return $"The tool manifest takes dotnet-ef {toolVersion}, not the EF Core version {core}.";
        }
    }

    private static Dictionary<string, string> PackageVersions(string root)
    {
        string props = Path.Combine(root, "Directory.Packages.props");
        if (!File.Exists(props))
        {
            return [];
        }

        return XDocument.Load(props).Root!.Elements("ItemGroup").Elements("PackageVersion")
            .ToDictionary(ProjectFile.Include, version => version.Attribute("Version")?.Value ?? string.Empty, StringComparer.Ordinal);
    }
}