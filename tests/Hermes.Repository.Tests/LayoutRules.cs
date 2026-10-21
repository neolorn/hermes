using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Hermes.Repository.Tests;

/// <summary>
/// The layout of <c>07</c> MSG-ENG-001: one project per package under <c>src/</c>, one test
/// project per source project under <c>tests/</c>, the solution holding exactly those, flat
/// projects but for the named folders, and namespaces that follow the folders.
/// </summary>
internal static partial class LayoutRules
{
    /// <summary>Answers each project of the repository at <paramref name="root"/> that is outside the layout.</summary>
    internal static IReadOnlyList<string> ProjectViolations(string root)
    {
        List<string> violations = [];
        IReadOnlyList<ProjectFile> projects = ProjectFile.ReadAll(root);
        List<string> sources = [];
        List<string> tests = [];

        foreach (ProjectFile project in projects)
        {
            string[] parts = project.RelativePath.Split('/');
            bool placed = parts.Length == 3 && string.Equals(parts[1], project.Name, StringComparison.Ordinal);
            if (placed && string.Equals(parts[0], "src", StringComparison.Ordinal) && Packages.All.Contains(project.Name, StringComparer.Ordinal))
            {
                sources.Add(project.Name);
            }
            else if (placed && string.Equals(parts[0], "tests", StringComparison.Ordinal))
            {
                tests.Add(project.Name);
            }
            else if (!(parts.Length == 3 && string.Equals(parts[0], "samples", StringComparison.Ordinal)))
            {
                violations.Add($"{project.RelativePath} is not a package's project at src/<Package>/ or a test project at tests/<Project>/.");
            }
        }

        foreach (string test in tests)
        {
            bool known = string.Equals(test, Packages.RepositoryTests, StringComparison.Ordinal)
                || sources.Any(source => string.Equals(test, Packages.TestProject(source), StringComparison.Ordinal));
            if (!known)
            {
                violations.Add($"tests/{test} is the test project of no source project.");
            }
        }

        foreach (string source in sources.Where(source => !tests.Contains(Packages.TestProject(source), StringComparer.Ordinal)))
        {
            violations.Add($"src/{source} has no test project tests/{Packages.TestProject(source)}.");
        }

        if (!tests.Contains(Packages.RepositoryTests, StringComparer.Ordinal))
        {
            violations.Add($"tests/{Packages.RepositoryTests} is missing.");
        }

        violations.AddRange(SolutionViolations(root, [.. sources.Select(name => $"src/{name}/{name}.csproj"), .. tests.Select(name => $"tests/{name}/{name}.csproj")]));
        return violations;
    }

    /// <summary>Answers each folder inside a project, and each file's namespace or public type, outside the layout.</summary>
    internal static IReadOnlyList<string> FolderViolations(string root)
    {
        List<string> violations = [];
        foreach (ProjectFile project in ProjectFile.ReadAll(root))
        {
            IReadOnlyList<string> allowed = Packages.Folders.TryGetValue(project.Name, out IReadOnlyList<string>? folders) ? folders : [];
            foreach (string folder in Directory.EnumerateDirectories(project.Folder))
            {
                string name = Path.GetFileName(folder);
                if (name is "bin" or "obj")
                {
                    continue;
                }

                if (!allowed.Contains(name, StringComparer.Ordinal))
                {
                    violations.Add($"{project.RelativePath}: the folder {name} is not one the project may hold.");
                }
                else if (Directory.EnumerateDirectories(folder).Any())
                {
                    violations.Add($"{project.RelativePath}: the folder {name} holds a folder of its own.");
                }
            }

            violations.AddRange(SourceFileViolations(root, project));
        }

        return violations;
    }

    private static IEnumerable<string> SourceFileViolations(string root, ProjectFile project)
    {
        bool shipped = project.RelativePath.StartsWith("src/", StringComparison.Ordinal);
        foreach (string file in ProjectFile.Enumerate(project.Folder, "*.cs"))
        {
            string relative = Path.GetRelativePath(project.Folder, file).Replace('\\', '/');
            string folder = Path.GetDirectoryName(relative)!.Replace('\\', '/');
            string expected = folder.Length == 0 ? project.Name : project.Name + "." + folder.Replace('/', '.');
            string text = File.ReadAllText(file);
            string where = Path.GetRelativePath(root, file).Replace('\\', '/');

            Match declared = NamespacePattern().Match(text);
            if (declared.Success && !string.Equals(declared.Groups["name"].Value, expected, StringComparison.Ordinal))
            {
                yield return $"{where} declares the namespace {declared.Groups["name"].Value}, not {expected}.";
            }

            int publicTypes = PublicTopLevelTypePattern().Count(text);
            if (publicTypes > 1)
            {
                yield return $"{where} declares more than one public top-level type.";
            }

            if (shipped && folder.Length > 0 && publicTypes > 0)
            {
                yield return $"{where} declares a public type outside the project's root folder.";
            }
        }
    }

    private static IEnumerable<string> SolutionViolations(string root, IReadOnlyList<string> expected)
    {
        string solution = Path.Combine(root, "Hermes.sln");
        if (!File.Exists(solution))
        {
            yield return "Hermes.sln is missing.";
            yield break;
        }

        HashSet<string> listed = [.. SolutionProjectPattern().Matches(File.ReadAllText(solution)).Select(match => match.Groups["path"].Value.Replace('\\', '/'))];
        foreach (string project in expected.Where(project => !listed.Contains(project)))
        {
            yield return $"Hermes.sln does not list {project}.";
        }

        foreach (string project in listed.Where(project => !expected.Contains(project, StringComparer.Ordinal)))
        {
            yield return $"Hermes.sln lists {project}, which is not a package's project or its test project.";
        }
    }

    [GeneratedRegex(@"^namespace\s+(?<name>[\w.]+)", RegexOptions.Multiline)]
    private static partial Regex NamespacePattern();

    [GeneratedRegex(@"^public\s+(?:(?:abstract|sealed|static|partial|readonly|ref|unsafe)\s+)*(?:class|record|struct|interface|enum|delegate)\b", RegexOptions.Multiline)]
    private static partial Regex PublicTopLevelTypePattern();

    [GeneratedRegex(@"^Project\(""\{[0-9A-F-]+\}""\)\s*=\s*""[^""]+"",\s*""(?<path>[^""]+\.csproj)""", RegexOptions.Multiline)]
    private static partial Regex SolutionProjectPattern();
}