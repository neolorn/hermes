using System;
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
    internal static IReadOnlyDictionary<string, string> Pinned { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["MailKit"] = "4.18.1",
        ["Fluid.Core"] = "2.40.0",
        ["Lib.Net.Http.WebPush"] = "3.3.1",
    };

    /// <summary>Mocking packages, none of which a project may take, directly or not.</summary>
    internal static IReadOnlyList<string> Mocking { get; } =
        ["Moq", "NSubstitute", "FakeItEasy", "RhinoMocks", "Rhino.Mocks", "JustMock", "Telerik.JustMock", "NMock", "NMock3", "Typemock.Isolator", "Microsoft.QualityTools.Testing.Fakes"];

    /// <summary>The properties <c>Directory.Build.props</c> sets once and no project overrides (MSG-ENG-002).</summary>
    internal static IReadOnlyDictionary<string, string> Settings { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Nullable"] = "enable",
        ["ImplicitUsings"] = "disable",
        ["TreatWarningsAsErrors"] = "true",
        ["AnalysisLevel"] = "latest-all",
        ["EnforceCodeStyleInBuild"] = "true",
        ["Deterministic"] = "true",
        ["GenerateDocumentationFile"] = "true",
        ["EnableConfigurationBindingGenerator"] = "true",
        ["NuGetAudit"] = "true",
        ["NuGetAuditMode"] = "all",
    };

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

    /// <summary>
    /// Answers each setting of the repository at <paramref name="root"/> outside MSG-ENG-002:
    /// a project file that sets a target framework, a listed property or a package version; the
    /// shared settings or the SDK not as given; a project without its lockfile.
    /// </summary>
    internal static IReadOnlyList<string> SettingViolations(string root)
    {
        List<string> violations = [];
        foreach (ProjectFile project in ProjectFile.ReadAll(root))
        {
            foreach (string property in project.Properties.Where(property => property is "TargetFramework" or "TargetFrameworks" || Settings.ContainsKey(property)))
            {
                violations.Add($"{project.RelativePath} sets {property}, which only Directory.Build.props sets.");
            }

            foreach (XElement reference in project.PackageReferences.Where(reference => reference.Attribute("Version") is not null || reference.Attribute("VersionOverride") is not null || reference.Element("Version") is not null))
            {
                violations.Add($"{project.RelativePath} sets the version of {ProjectFile.Include(reference)}, which only Directory.Packages.props sets.");
            }

            bool samples = project.RelativePath.StartsWith("samples/", StringComparison.Ordinal);
            if (!samples && !File.Exists(Path.Combine(project.Folder, "packages.lock.json")))
            {
                violations.Add($"{project.RelativePath} has no packages.lock.json.");
            }
        }

        violations.AddRange(SharedSettingViolations(root));
        return violations;
    }

    private static IEnumerable<string> SharedSettingViolations(string root)
    {
        string props = Path.Combine(root, "Directory.Build.props");
        if (!File.Exists(props))
        {
            yield return "Directory.Build.props is missing.";
            yield break;
        }

        XDocument document = XDocument.Load(props);
        Dictionary<string, string> set = document.Root!.Elements("PropertyGroup").Elements()
            .GroupBy(element => element.Name.LocalName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.Ordinal);
        foreach ((string property, string value) in Settings.Append(new KeyValuePair<string, string>("TargetFramework", "net10.0")))
        {
            if (!set.TryGetValue(property, out string? actual) || !string.Equals(actual, value, StringComparison.Ordinal))
            {
                yield return $"Directory.Build.props does not set {property} to {value}.";
            }
        }

        foreach (string other in ProjectFile.Enumerate(root, "Directory.*.props").Concat(ProjectFile.Enumerate(root, "Directory.*.targets")))
        {
            string relative = Path.GetRelativePath(root, other).Replace('\\', '/');
            if (relative is not "Directory.Build.props" and not "Directory.Packages.props")
            {
                yield return $"{relative} sets what only the root's Directory.Build.props and Directory.Packages.props set.";
            }
        }

        string global = Path.Combine(root, "global.json");
        if (!File.Exists(global))
        {
            yield return "global.json is missing.";
            yield break;
        }

        using JsonDocument sdk = JsonDocument.Parse(File.ReadAllText(global));
        JsonElement settings = sdk.RootElement.GetProperty("sdk");
        if (!settings.TryGetProperty("version", out JsonElement version) || !(version.GetString() ?? string.Empty).StartsWith("10.0.", StringComparison.Ordinal))
        {
            yield return "global.json does not name a .NET 10 SDK.";
        }

        if (!settings.TryGetProperty("rollForward", out JsonElement rollForward) || !string.Equals(rollForward.GetString(), "latestPatch", StringComparison.Ordinal))
        {
            yield return "global.json does not set rollForward to latestPatch.";
        }
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