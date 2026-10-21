using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Hermes.Repository.Tests;

/// <summary>
/// One project file of a repository, read as written.
/// </summary>
internal sealed class ProjectFile
{
    private static readonly string[] BuildOutputFolders = ["bin", "obj"];

    private static readonly string[] RootOnlyFolders = [".git", "tmp", "artifacts"];

    private ProjectFile(string root, string path)
    {
        Path = path;
        RelativePath = System.IO.Path.GetRelativePath(root, path).Replace('\\', '/');
        Name = System.IO.Path.GetFileNameWithoutExtension(path);
        Document = XDocument.Load(path);
    }

    /// <summary>The file's full path.</summary>
    internal string Path { get; }

    /// <summary>The file's path from the repository's root, with forward slashes.</summary>
    internal string RelativePath { get; }

    /// <summary>The project's name: its file name without the extension.</summary>
    internal string Name { get; }

    /// <summary>The folder holding the project file.</summary>
    internal string Folder => System.IO.Path.GetDirectoryName(Path)!;

    internal XDocument Document { get; }

    /// <summary>The names of the projects this one references.</summary>
    internal IReadOnlyList<string> ProjectReferences =>
        [.. Items("ProjectReference").Select(item => System.IO.Path.GetFileNameWithoutExtension(Include(item).Replace('\\', '/')))];

    internal IReadOnlyList<XElement> PackageReferences => [.. Items("PackageReference")];

    internal IReadOnlyList<string> FrameworkReferences => [.. Items("FrameworkReference").Select(Include)];

    /// <summary>The assemblies this project's <c>InternalsVisibleTo</c> items name.</summary>
    internal IReadOnlyList<string> Grants => [.. Items("InternalsVisibleTo").Select(Include)];

    /// <summary>The names of the properties the project file sets.</summary>
    internal IReadOnlyList<string> Properties =>
        [.. Document.Root!.Elements("PropertyGroup").Elements().Select(element => element.Name.LocalName)];

    internal string? Property(string name)
    {
        return Document.Root!.Elements("PropertyGroup").Elements(name).LastOrDefault()?.Value;
    }

    /// <summary>Reads every project file of the repository at <paramref name="root"/>.</summary>
    internal static IReadOnlyList<ProjectFile> ReadAll(string root)
    {
        return [.. Enumerate(root, "*.csproj").Order(StringComparer.Ordinal).Select(path => new ProjectFile(root, path))];
    }

    /// <summary>
    /// Lists the files of the repository at <paramref name="root"/> that match
    /// <paramref name="pattern"/>, leaving out build output, <c>tmp/</c> and Git's own folder.
    /// </summary>
    internal static IEnumerable<string> Enumerate(string root, string pattern)
    {
        return Walk(root, root, pattern);
    }

    private static IEnumerable<string> Walk(string root, string folder, string pattern)
    {
        foreach (string file in Directory.EnumerateFiles(folder, pattern))
        {
            yield return file;
        }

        foreach (string child in Directory.EnumerateDirectories(folder))
        {
            string name = System.IO.Path.GetFileName(child);
            bool atRoot = string.Equals(folder, root, StringComparison.Ordinal);
            if (BuildOutputFolders.Contains(name, StringComparer.Ordinal) || (atRoot && RootOnlyFolders.Contains(name, StringComparer.Ordinal)))
            {
                continue;
            }

            foreach (string file in Walk(root, child, pattern))
            {
                yield return file;
            }
        }
    }

    internal static string Include(XElement item)
    {
        return item.Attribute("Include")?.Value ?? throw new InvalidOperationException($"An item {item.Name.LocalName} has no Include.");
    }

    private IEnumerable<XElement> Items(string kind)
    {
        return Document.Root!.Elements("ItemGroup").Elements(kind);
    }
}