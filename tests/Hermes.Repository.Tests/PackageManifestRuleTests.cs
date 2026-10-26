using System;
using System.Xml.Linq;

using Xunit;

namespace Hermes.Repository.Tests;

[Trait("Category", "unit")]
public sealed class PackageManifestRuleTests
{
    private const string Core = """
        <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
          <metadata>
            <id>Hermes.Core</id>
            <version>1.2.3</version>
            <authors>neolorn</authors>
            <description>The public types and contracts every other package shares</description>
            <repository type="git" url="https://github.com/neolorn/hermes" />
          </metadata>
        </package>
        """;

    [Theory]
    [InlineData("1.2.3")]
    [InlineData("[1.2.2]")]
    [InlineData("[1.2.3, )")]
    public void MSG_LIB_001_AC3_DependencyOnALibraryPackageNotAtTheExactVersionPackedIsFound(string version)
    {
        PackageManifest[] manifests = [Parse(Core), Parse(Channels(version))];

        Assert.Equal(
            [$"Hermes.Channels depends on Hermes.Core at {version}, not the exact version packed, [1.2.3]."],
            PackageRules.DependencyViolations(manifests));
    }

    [Fact]
    public void MSG_LIB_001_AC3_DependencyAtTheExactVersionPackedIsAccepted()
    {
        PackageManifest[] manifests = [Parse(Core), Parse(Channels("[1.2.3]"))];

        Assert.Empty(PackageRules.DependencyViolations(manifests));
    }

    [Fact]
    public void ManifestViolations_PackageAsTheRuleGivesIt_HasNone()
    {
        Assert.Empty(PackageRules.ManifestViolations(Parse(Core)));
    }

    [Theory]
    [InlineData("<tags>messaging</tags>", "Hermes.Core carries the package property tags, which is left unset.")]
    [InlineData("<license type=\"expression\">MIT</license>", "Hermes.Core carries the package property license, which is left unset.")]
    public void ManifestViolations_AnotherPackageProperty_IsFound(string element, string violation)
    {
        PackageManifest manifest = Parse(Core.Replace("</metadata>", element + "</metadata>", StringComparison.Ordinal));

        Assert.Equal([violation], PackageRules.ManifestViolations(manifest));
    }

    [Theory]
    [InlineData("<authors>neolorn</authors>", "<authors>someone</authors>", "Hermes.Core's authors are someone, not neolorn.")]
    [InlineData("every other package shares", "every package shares", "Hermes.Core's description is not its cell of 00 section 2.2.")]
    [InlineData("github.com/neolorn/hermes", "github.com/someone/hermes", "Hermes.Core's repository is https://github.com/someone/hermes, not https://github.com/neolorn/hermes.")]
    public void ManifestViolations_PropertyNotAsTheRuleGivesIt_IsFound(string text, string replacement, string violation)
    {
        PackageManifest manifest = Parse(Core.Replace(text, replacement, StringComparison.Ordinal));

        Assert.Equal([violation], PackageRules.ManifestViolations(manifest));
    }

    private static string Channels(string coreVersion)
    {
        return $"""
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>Hermes.Channels</id>
                <version>1.2.3</version>
                <dependencies>
                  <group targetFramework="net10.0">
                    <dependency id="Hermes.Core" version="{coreVersion}" exclude="Build,Analyzers" />
                    <dependency id="MailKit" version="4.18.1" exclude="Build,Analyzers" />
                  </group>
                </dependencies>
              </metadata>
            </package>
            """;
    }

    private static PackageManifest Parse(string nuspec)
    {
        return PackageManifest.Parse(XDocument.Parse(nuspec));
    }
}