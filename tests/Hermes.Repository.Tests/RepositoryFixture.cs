using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Hermes.Repository.Tests;

/// <summary>
/// A repository tree under <c>tmp/</c> that keeps every rule: copies of the repository's
/// project files and lockfiles, its solutions, its shared settings, package sources, tool
/// manifest and scanner configuration, and of the test vectors' files that configuration
/// names, without any other source file. A test breaks one rule in it and checks the rule
/// finds that.
/// </summary>
internal sealed class RepositoryFixture : IDisposable
{
    private static readonly IReadOnlyList<string> SharedFiles =
    [
        "Hermes.sln", "Directory.Build.props", "Directory.Packages.props", "global.json", "nuget.config", ".gitignore",
        ".config/dotnet-tools.json", "build/gates/gitleaks.toml",
    ];

    /// <summary>The sample host's solution and package sources, copied with its projects.</summary>
    private static readonly IReadOnlyList<string> SampleFiles = ["samples/Samples.sln", "samples/nuget.config"];

    private readonly ScratchFolder _folder = new();

    internal RepositoryFixture()
    {
        foreach (string file in SharedFiles)
        {
            Write(file, File.ReadAllText(RepositoryRoot.Combine(file)));
        }

        foreach (string file in SampleFiles.Where(file => File.Exists(RepositoryRoot.Combine(file))))
        {
            Write(file, File.ReadAllText(RepositoryRoot.Combine(file)));
        }

        foreach (string file in ScannerConfigurationRules.NamedFiles(RepositoryRoot.Path).Distinct(StringComparer.Ordinal))
        {
            Write(file, File.ReadAllText(RepositoryRoot.Combine(file)));
        }

        foreach (ProjectFile project in ProjectFile.ReadAll(RepositoryRoot.Path))
        {
            Write(project.RelativePath, File.ReadAllText(project.Path));
            string lockFile = Path.Combine(project.Folder, "packages.lock.json");
            if (File.Exists(lockFile))
            {
                Write(Path.Combine(Path.GetDirectoryName(project.RelativePath)!, "packages.lock.json"), File.ReadAllText(lockFile));
            }
        }
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
        Write("Hermes.sln", Solution(projects));
    }

    /// <summary>
    /// Writes a sample host that keeps every rule: <c>samples/Bakery</c> on the packed packages,
    /// its test project on the sample host and the test packages, the sample solution listing
    /// both, and the sample package sources.
    /// </summary>
    internal void WriteSample()
    {
        Write("samples/Bakery/Bakery.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <ItemGroup>
                <PackageReference Include="Hermes" />
                <PackageReference Include="Hermes.Testing" />
              </ItemGroup>
            </Project>
            """);
        Write("samples/Bakery.Tests/Bakery.Tests.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <FrameworkReference Include="Microsoft.AspNetCore.App" />
                <PackageReference Include="xunit.v3" />
                <PackageReference Include="Testcontainers.PostgreSql" />
                <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
                <ProjectReference Include="..\Bakery\Bakery.csproj" />
              </ItemGroup>
            </Project>
            """);
        Write("samples/Samples.sln", Solution(["Bakery/Bakery.csproj", "Bakery.Tests/Bakery.Tests.csproj"]));
        Write("samples/nuget.config", SampleSources);
    }

    /// <summary>A <c>samples/nuget.config</c> that keeps MSG-ENG-001's rule.</summary>
    internal const string SampleSources = """
        <configuration>
          <packageSources>
            <clear />
            <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
            <add key="packed" value="../artifacts/packages" />
          </packageSources>
          <packageSourceMapping>
            <clear />
            <packageSource key="packed">
              <package pattern="Hermes" />
              <package pattern="Hermes.Core" />
              <package pattern="Hermes.Channels" />
              <package pattern="Hermes.Testing" />
              <package pattern="Hermes.Cli" />
            </packageSource>
            <packageSource key="nuget.org">
              <package pattern="*" />
            </packageSource>
          </packageSourceMapping>
        </configuration>
        """;

    private static string Solution(IReadOnlyList<string> projects)
    {
        IEnumerable<string> lines = projects.Select(project =>
            $"Project(\"{{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}}\") = \"{Path.GetFileNameWithoutExtension(project)}\", \"{project.Replace('/', '\\')}\", \"{{{Guid.CreateVersion7().ToString().ToUpperInvariant()}}}\"\nEndProject");
        return string.Join('\n', lines) + "\n";
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