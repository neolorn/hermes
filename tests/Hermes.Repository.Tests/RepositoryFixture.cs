using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Hermes.Repository.Tests;

/// <summary>
/// A small repository tree under <c>tmp/</c> that keeps every rule: the core and channel
/// packages with their test projects, the repository's tests, the solution, and copies of the
/// repository's shared settings, tool manifest and scanner configuration. A test breaks one
/// rule in it and checks the rule finds that.
/// </summary>
internal sealed class RepositoryFixture : IDisposable
{
    private static readonly string[] SharedFiles =
        ["Directory.Build.props", "Directory.Packages.props", "global.json", ".config/dotnet-tools.json", "build/gates/gitleaks.toml"];

    private readonly ScratchFolder _folder = new();

    internal RepositoryFixture()
    {
        foreach (string file in SharedFiles)
        {
            Write(file, File.ReadAllText(RepositoryRoot.Combine(file)));
        }

        string[] sources = [Packages.Core, Packages.Channels];
        WriteSource(Packages.Core, []);
        WriteSource(Packages.Channels, [Packages.Core]);
        foreach (string source in sources)
        {
            WriteTest(Packages.TestProject(source), [source]);
        }

        WriteTest(Packages.RepositoryTests, sources);
        WriteSolution([.. sources.Select(name => $"src/{name}/{name}.csproj"), .. sources.Append("Hermes.Repository").Select(name => $"tests/{name}.Tests/{name}.Tests.csproj")]);
    }

    internal string Root => _folder.Path;

    internal string Write(string relativePath, string content)
    {
        return _folder.Write(relativePath, content);
    }

    /// <summary>Writes a project file of the given body, with an empty lockfile beside it.</summary>
    internal void WriteProject(string relativePath, string body)
    {
        Write(relativePath, $"""
            <Project Sdk="Microsoft.NET.Sdk">
            {body}
            </Project>
            """);
        Write(Path.Combine(Path.GetDirectoryName(relativePath)!, "packages.lock.json"), """{ "version": 2, "dependencies": { "net10.0": { } } }""");
    }

    internal void WriteSource(string name, IReadOnlyList<string> projects)
    {
        WriteProject($"src/{name}/{name}.csproj", $"""
              <ItemGroup>
                <PackageReference Include="Microsoft.CodeAnalysis.PublicApiAnalyzers" PrivateAssets="all" />
                <PackageReference Include="MinVer" PrivateAssets="all" />
              </ItemGroup>
            {References(projects)}
            """);
    }

    internal void WriteTest(string name, IReadOnlyList<string> projects)
    {
        WriteProject($"tests/{name}/{name}.csproj", $"""
              <ItemGroup>
                <PackageReference Include="xunit.v3" />
              </ItemGroup>
            {References(projects)}
            """);
    }

    internal void WriteSolution(IReadOnlyList<string> projects)
    {
        IEnumerable<string> lines = projects.Select(project =>
            $"Project(\"{{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}}\") = \"{Path.GetFileNameWithoutExtension(project)}\", \"{project.Replace('/', '\\')}\", \"{{{Guid.CreateVersion7().ToString().ToUpperInvariant()}}}\"\nEndProject");
        Write("Hermes.sln", string.Join('\n', lines) + "\n");
    }

    public void Dispose()
    {
        _folder.Dispose();
    }

    private static string References(IReadOnlyList<string> projects)
    {
        if (projects.Count == 0)
        {
            return string.Empty;
        }

        string items = string.Join('\n', projects.Select(project => $"    <ProjectReference Include=\"..\\..\\src\\{project}\\{project}.csproj\" />"));
        return $"  <ItemGroup>\n{items}\n  </ItemGroup>";
    }
}