using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace Hermes.Repository.Tests;

/// <remarks>
/// Each test builds a copy of <c>Hermes.Core</c>'s project and public API files under
/// <c>tmp/</c>, so the copy takes the repository's settings and analysers as the project does.
/// </remarks>
[Trait("Category", "integration")]
public sealed class PublicSurfaceBuildTests
{
    private static readonly IReadOnlyList<string> Copied = ["Hermes.Core.csproj", "PublicAPI.Shipped.txt", "PublicAPI.Unshipped.txt", "packages.lock.json"];

    [Fact]
    public async Task MSG_LIB_002_AC1_AddingAPublicTypeWithoutItsPublicApiFileFailsTheBuild()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        ProbeProject probe = CopyCore(folder);

        ProcessResult unchanged = await probe.BuildAsync(cancellationToken);
        probe.WriteSource("Probe.cs", """
            namespace Hermes.Core;

            /// <summary>A public type no public API file lists.</summary>
            public sealed class Probe
            {
            }
            """);
        ProcessResult added = await probe.BuildAsync(cancellationToken);

        Assert.True(unchanged.ExitCode == 0, unchanged.Output);
        Assert.NotEqual(0, added.ExitCode);
        Assert.Contains("RS0016", added.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MSG_LIB_002_AC1_RemovingAPublicTypeWithoutItsPublicApiFileFailsTheBuild()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        ProbeProject probe = CopyCore(folder);
        probe.WriteSource("PublicAPI.Unshipped.txt", "#nullable enable\nHermes.Core.Removed\n");

        ProcessResult removed = await probe.BuildAsync(cancellationToken);

        Assert.NotEqual(0, removed.ExitCode);
        Assert.Contains("RS0017", removed.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MSG_ENG_004_AC1_PublicMemberWithoutDocumentationFailsTheBuild()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        ProbeProject probe = CopyCore(folder);
        probe.WriteSource("PublicAPI.Unshipped.txt", "#nullable enable\nHermes.Core.Probe\nHermes.Core.Probe.Probe() -> void\n");
        probe.WriteSource("Probe.cs", """
            namespace Hermes.Core;

            public sealed class Probe
            {
            }
            """);

        ProcessResult undocumented = await probe.BuildAsync(cancellationToken);

        Assert.NotEqual(0, undocumented.ExitCode);
        Assert.Contains("error CS1591", undocumented.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("RS0016", undocumented.Output, StringComparison.Ordinal);
    }

    private static ProbeProject CopyCore(ScratchFolder folder)
    {
        ProbeProject probe = new(folder, Packages.Core);
        foreach (string file in Copied)
        {
            probe.WriteSource(file, File.ReadAllText(RepositoryRoot.Combine("src", Packages.Core, file)));
        }

        return probe;
    }
}