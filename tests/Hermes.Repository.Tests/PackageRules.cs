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

    /// <summary>Answers each package or tool of the repository at <paramref name="root"/> outside MSG-ENG-009.</summary>
    internal static IReadOnlyList<string> AllowListViolations(string root)
    {
        List<string> violations = [];
        foreach (ProjectFile project in ProjectFile.ReadAll(root))
        {
            foreach (string package in project.PackageReferences.Select(ProjectFile.Include).Where(package => !Allowed.Contains(package, StringComparer.Ordinal)))
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