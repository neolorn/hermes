using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

namespace Hermes.Repository.Tests;

/// <summary>
/// What each project may reference (<c>04</c> MSG-LIB-001) and the grants of
/// <c>InternalsVisibleTo</c> (<c>07</c> MSG-ENG-001).
/// </summary>
internal static class ReferenceRules
{
    internal const string AspNetCore = "Microsoft.AspNetCore.App";

    internal const string PublicApiAnalyzers = "Microsoft.CodeAnalysis.PublicApiAnalyzers";

    internal const string MinVer = "MinVer";

    internal const string EntityFrameworkCoreDesign = "Microsoft.EntityFrameworkCore.Design";

    /// <summary>The Microsoft.Extensions abstractions of <c>07</c> MSG-ENG-009.</summary>
    internal static IReadOnlyList<string> HostingAbstractions { get; } =
    [
        "Microsoft.Extensions.DependencyInjection.Abstractions",
        "Microsoft.Extensions.Configuration.Abstractions",
        "Microsoft.Extensions.Configuration.Binder",
        "Microsoft.Extensions.Options",
        "Microsoft.Extensions.Options.ConfigurationExtensions",
        "Microsoft.Extensions.Logging.Abstractions",
        "Microsoft.Extensions.Hosting.Abstractions",
        "Microsoft.Extensions.Http",
    ];

    /// <summary>The test packages of <c>07</c> MSG-ENG-009.</summary>
    internal static IReadOnlyList<string> TestPackages { get; } = ["xunit.v3", "Testcontainers.PostgreSql", "Microsoft.AspNetCore.Mvc.Testing"];

    /// <summary>The library's own projects each package's project may reference.</summary>
    private static readonly FrozenDictionary<string, IReadOnlyList<string>> AllowedProjects = new Dictionary<string, IReadOnlyList<string>>
    {
        [Packages.Core] = [],
        [Packages.Channels] = [Packages.Core],
        [Packages.Engine] = [Packages.Core, Packages.Channels],
        [Packages.JanusChannels] = [Packages.Core, Packages.Channels],
        [Packages.Janus] = [Packages.Core, Packages.Engine, Packages.JanusChannels],
        [Packages.Testing] = [Packages.Core, Packages.Channels, Packages.Engine],
        [Packages.Cli] = [Packages.Engine],
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// The packages each package's project may reference at run time. Janus's main package
    /// joins the two <c>Hermes.Janus</c> rows with its source (plan, phase 6).
    /// </summary>
    private static readonly FrozenDictionary<string, IReadOnlyList<string>> AllowedPackages = new Dictionary<string, IReadOnlyList<string>>
    {
        [Packages.Core] = [],
        [Packages.Channels] = ["MailKit", "Lib.Net.Http.WebPush", .. HostingAbstractions],
        [Packages.Engine] = ["Microsoft.EntityFrameworkCore", "Microsoft.EntityFrameworkCore.Relational", "Npgsql.EntityFrameworkCore.PostgreSQL", "Fluid.Core"],
        [Packages.JanusChannels] = [],
        [Packages.Janus] = [],
        [Packages.Testing] = [],
        [Packages.Cli] = [],
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Answers each reference of a project of the repository at <paramref name="root"/> that MSG-LIB-001 does not allow.</summary>
    internal static IReadOnlyList<string> ReferenceViolations(string root)
    {
        IReadOnlyList<ProjectFile> projects = ProjectFile.ReadAll(root);
        Dictionary<string, ProjectFile> byName = projects.ToDictionary(project => project.Name, StringComparer.Ordinal);
        List<string> violations = [];

        foreach (ProjectFile project in projects)
        {
            bool shipped = project.RelativePath.StartsWith("src/", StringComparison.Ordinal);
            bool test = project.RelativePath.StartsWith("tests/", StringComparison.Ordinal);
            if (!shipped && !test)
            {
                continue;
            }

            IReadOnlyList<string> allowedProjects = shipped ? AllowedProjects.GetValueOrDefault(project.Name, []) : AllowedTestProjects(project.Name);
            IReadOnlyList<string> allowedPackages = shipped ? AllowedPackages.GetValueOrDefault(project.Name, []) : TestPackages;

            foreach (string reference in project.ProjectReferences.Where(reference => !allowedProjects.Contains(reference, StringComparer.Ordinal)))
            {
                violations.Add($"{project.RelativePath} references the project {reference}, which MSG-LIB-001 does not allow it.");
            }

            List<string> buildOnly = [];
            foreach (XElement reference in project.PackageReferences)
            {
                string package = ProjectFile.Include(reference);
                if (IsBuildOnly(project, package))
                {
                    buildOnly.Add(package);
                    if (!string.Equals(reference.Attribute("PrivateAssets")?.Value, "all", StringComparison.Ordinal))
                    {
                        violations.Add($"{project.RelativePath} references {package} without PrivateAssets=\"all\", though it is build-only.");
                    }
                }
                else if (!allowedPackages.Contains(package, StringComparer.Ordinal))
                {
                    violations.Add($"{project.RelativePath} references the package {package}, which MSG-LIB-001 does not allow it.");
                }
            }

            HashSet<string> referenced = [.. project.PackageReferences.Select(ProjectFile.Include)];
            foreach (string package in project.LockedDirectPackages.Where(package => !referenced.Contains(package)))
            {
                violations.Add($"{project.RelativePath} takes the package {package} by a reference outside its project file, which MSG-LIB-001 does not allow it.");
            }

            if (shipped)
            {
                foreach (string required in new[] { PublicApiAnalyzers, MinVer }.Where(required => !buildOnly.Contains(required, StringComparer.Ordinal)))
                {
                    violations.Add($"{project.RelativePath} does not reference {required} as a build-only reference.");
                }
            }

            violations.AddRange(FrameworkViolations(project, byName));
        }

        violations.AddRange(SharedItemViolations(root));
        return violations;
    }

    /// <summary>
    /// Answers each item of <c>Directory.Build.props</c> but the CLSCompliant attribute and the
    /// exact-version target's update, and each item of <c>Directory.Packages.props</c> but a
    /// <c>PackageVersion</c>, since such an item would reach every project unseen.
    /// </summary>
    private static IEnumerable<string> SharedItemViolations(string root)
    {
        string buildProps = Path.Combine(root, "Directory.Build.props");
        if (File.Exists(buildProps))
        {
            foreach (XElement item in XDocument.Load(buildProps).Descendants("ItemGroup").Elements())
            {
                string name = item.Name.LocalName;
                XElement? owner = item.Parent!.Parent;
                bool attribute = owner == item.Document!.Root && string.Equals(name, "AssemblyAttribute", StringComparison.Ordinal)
                    && string.Equals(item.Attribute("Include")?.Value, "System.CLSCompliantAttribute", StringComparison.Ordinal);
                bool exactVersion = owner is { Name.LocalName: "Target" } && string.Equals(owner.Attribute("Name")?.Value, "DependOnTheExactVersionPacked", StringComparison.Ordinal)
                    && string.Equals(name, "_ProjectReferencesWithVersions", StringComparison.Ordinal);
                if (!attribute && !exactVersion)
                {
                    yield return $"Directory.Build.props adds a {name} item {Target(item)}; it adds no item but the CLSCompliant attribute.";
                }
            }
        }

        string packagesProps = Path.Combine(root, "Directory.Packages.props");
        if (File.Exists(packagesProps))
        {
            foreach (XElement item in XDocument.Load(packagesProps).Descendants("ItemGroup").Elements().Where(item => item.Name.LocalName is not "PackageVersion"))
            {
                yield return $"Directory.Packages.props adds a {item.Name.LocalName} item {Target(item)}; it adds no item but PackageVersion.";
            }
        }
    }

    private static string Target(XElement item)
    {
        return item.Attribute("Include")?.Value ?? item.Attribute("Update")?.Value ?? string.Empty;
    }

    /// <summary>
    /// Answers each reference by which <c>Hermes.Channels</c>, directly or through what it
    /// references, takes EF Core, ASP.NET Core or Fluid (MSG-LIB-001 criterion 2).
    /// </summary>
    internal static IReadOnlyList<string> ChannelsViolations(string root)
    {
        string project = Path.Combine(root, "src", Packages.Channels, Packages.Channels + ".csproj");
        if (!File.Exists(project))
        {
            return [$"src/{Packages.Channels} is missing."];
        }

        IReadOnlyList<ProjectFile> projects = ProjectFile.ReadAll(root);
        Dictionary<string, ProjectFile> byName = projects.ToDictionary(file => file.Name, StringComparer.Ordinal);
        List<string> violations = [];

        foreach (ProjectFile reached in Closure(byName[Packages.Channels], byName))
        {
            if (string.Equals(reached.Name, Packages.Engine, StringComparison.Ordinal))
            {
                violations.Add($"{Packages.Channels} reaches the engine, {Packages.Engine}.");
            }

            if (reached.FrameworkReferences.Contains(AspNetCore, StringComparer.Ordinal))
            {
                violations.Add($"{Packages.Channels} reaches ASP.NET Core through {reached.Name}.");
            }
        }

        string lockFile = Path.Combine(root, "src", Packages.Channels, "packages.lock.json");
        if (File.Exists(lockFile))
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(lockFile));
            foreach (JsonProperty framework in document.RootElement.GetProperty("dependencies").EnumerateObject())
            {
                foreach (JsonProperty package in framework.Value.EnumerateObject().Where(package => IsForbiddenToChannels(package.Name)))
                {
                    violations.Add($"{Packages.Channels} takes the package {package.Name}.");
                }
            }
        }

        return violations;
    }

    /// <summary>
    /// Answers each grant of <c>InternalsVisibleTo</c> outside MSG-ENG-001's rule that the built
    /// assembly of a project of the repository at <paramref name="root"/> carries, whatever form
    /// declared it: an item, an attribute item or an attribute in a source file.
    /// </summary>
    internal static IReadOnlyList<string> GrantViolations(string root)
    {
        List<string> violations = [];
        foreach (ProjectFile project in ProjectFile.ReadAll(root))
        {
            string assembly = Path.Combine(project.Folder, "bin", "Release", "net10.0", project.Name + ".dll");
            if (!File.Exists(assembly))
            {
                violations.Add($"{project.RelativePath} has no built assembly at bin/Release/net10.0/, so its grants cannot be read.");
                continue;
            }

            violations.AddRange(GrantViolations(project.RelativePath, project.Name, AssemblySurface.Grants(assembly)));
        }

        return violations;
    }

    /// <summary>Answers each of <paramref name="grantees"/> the project at <paramref name="relativePath"/> may not grant its internals to.</summary>
    internal static IEnumerable<string> GrantViolations(string relativePath, string name, IEnumerable<string> grantees)
    {
        return grantees.Where(grantee => !IsAllowedGrant(relativePath, name, grantee))
            .Select(grantee => $"{relativePath} grants its internals to {grantee}, which MSG-ENG-001 does not allow.");
    }

    private static bool IsAllowedGrant(string relativePath, string name, string grantee)
    {
        if (!relativePath.StartsWith("src/", StringComparison.Ordinal))
        {
            return false;
        }

        if (string.Equals(grantee, Packages.TestProject(name), StringComparison.Ordinal))
        {
            return true;
        }

        string[] allowed = name switch
        {
            Packages.Channels => [Packages.Engine, Packages.JanusChannels, Packages.Testing],
            Packages.Engine => [Packages.Cli, Packages.Testing],
            _ => [],
        };
        return allowed.Contains(grantee, StringComparer.Ordinal);
    }

    private static IReadOnlyList<string> AllowedTestProjects(string testProject)
    {
        // The repository's tests read every shipped project's built assembly (07 MSG-ENG-004).
        if (string.Equals(testProject, Packages.RepositoryTests, StringComparison.Ordinal))
        {
            return Packages.All;
        }

        string source = testProject.EndsWith(".Tests", StringComparison.Ordinal) ? testProject[..^".Tests".Length] : testProject;
        return [source, Packages.Testing];
    }

    private static bool IsBuildOnly(ProjectFile project, string package)
    {
        bool shipped = project.RelativePath.StartsWith("src/", StringComparison.Ordinal);
        return shipped && (string.Equals(package, PublicApiAnalyzers, StringComparison.Ordinal)
            || string.Equals(package, MinVer, StringComparison.Ordinal)
            || (string.Equals(package, EntityFrameworkCoreDesign, StringComparison.Ordinal) && string.Equals(project.Name, Packages.Engine, StringComparison.Ordinal)));
    }

    /// <summary>
    /// A project that references ASP.NET Core's shared framework, or a project that does,
    /// references the framework itself; <c>Hermes.Core</c> and <c>Hermes.Channels</c> never do.
    /// </summary>
    private static IEnumerable<string> FrameworkViolations(ProjectFile project, Dictionary<string, ProjectFile> byName)
    {
        foreach (string framework in project.FrameworkReferences.Where(framework => !string.Equals(framework, AspNetCore, StringComparison.Ordinal)))
        {
            yield return $"{project.RelativePath} references the framework {framework}, which MSG-LIB-001 does not allow it.";
        }

        bool references = project.FrameworkReferences.Contains(AspNetCore, StringComparer.Ordinal);
        if (references && project.Name is Packages.Core or Packages.Channels)
        {
            yield return $"{project.RelativePath} references {AspNetCore}, which it never may.";
        }

        bool needs = Closure(project, byName).Skip(1).Any(reached => reached.FrameworkReferences.Contains(AspNetCore, StringComparer.Ordinal));
        if (needs && !references)
        {
            yield return $"{project.RelativePath} references a project that references {AspNetCore}, without referencing the framework itself.";
        }

        bool allowed = string.Equals(project.Name, Packages.Engine, StringComparison.Ordinal) || needs
            || (project.RelativePath.StartsWith("tests/", StringComparison.Ordinal) && project.PackageReferences.Any(reference => string.Equals(ProjectFile.Include(reference), "Microsoft.AspNetCore.Mvc.Testing", StringComparison.Ordinal)));
        if (references && !allowed)
        {
            yield return $"{project.RelativePath} references {AspNetCore} without needing it.";
        }
    }

    /// <summary>The project and every project it reaches through its references.</summary>
    private static List<ProjectFile> Closure(ProjectFile project, Dictionary<string, ProjectFile> byName)
    {
        List<ProjectFile> reached = [project];
        for (int index = 0; index < reached.Count; index++)
        {
            foreach (string name in reached[index].ProjectReferences)
            {
                if (byName.TryGetValue(name, out ProjectFile? next) && !reached.Contains(next))
                {
                    reached.Add(next);
                }
            }
        }

        return reached;
    }

    private static bool IsForbiddenToChannels(string package)
    {
        return package.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase)
            || package.StartsWith("Npgsql", StringComparison.OrdinalIgnoreCase)
            || package.StartsWith("Fluid", StringComparison.OrdinalIgnoreCase)
            || package.StartsWith("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase);
    }
}