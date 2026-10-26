using System;
using System.Linq;

using Xunit;

namespace Hermes.Repository.Tests;

/// <remarks>
/// The packages are packed once for the class, from the working tree, into a folder under
/// <c>tmp/</c>.
/// </remarks>
[Trait("Category", "integration")]
public sealed class PackTests(PackedPackages packed) : IClassFixture<PackedPackages>
{
    [Fact]
    public void MSG_LIB_001_AC3_EachPackageDependsOnTheLibrarysOtherPackagesAtTheExactVersionPacked()
    {
        Assert.Equal(ProjectFile.SourceProjects(RepositoryRoot.Path).Count, packed.Manifests.Count);
        Assert.Contains(packed.Manifests, manifest => manifest.Dependencies.Any(dependency => Packages.All.Contains(dependency.Id, StringComparer.Ordinal)));
        Assert.Empty(PackageRules.DependencyViolations(packed.Manifests));
    }

    [Fact]
    public void Pack_EachPackage_CarriesItsDescriptionAuthorsAndAddressAlone()
    {
        Assert.NotEmpty(packed.Manifests);
        Assert.Empty(packed.Manifests.SelectMany(PackageRules.ManifestViolations));
    }
}