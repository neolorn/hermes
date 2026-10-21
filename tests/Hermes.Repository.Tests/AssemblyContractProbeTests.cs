using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace Hermes.Repository.Tests;

/// <remarks>
/// The rules of the contract tests, run over an assembly built to break them, so a public
/// type that breaks one in a package is found.
/// </remarks>
[Trait("Category", "integration")]
public sealed class AssemblyContractProbeTests
{
    [Fact]
    public async Task MSG_ENG_004_AC1_PublicClassNeitherSealedNorAbstractIsFound()
    {
        IReadOnlyList<PublicType> types = await BuildProbeAsync(TestContext.Current.CancellationToken);

        string[] violations = [.. SurfaceRules.SealingViolations(types)];

        Assert.Equal(["Probe.Open is a public class neither sealed nor abstract."], violations);
    }

    [Fact]
    public async Task MSG_ENG_004_AC1_PublicAsynchronousMethodWithoutATokenIsFound()
    {
        IReadOnlyList<PublicType> types = await BuildProbeAsync(TestContext.Current.CancellationToken);

        string[] violations = [.. SurfaceRules.TokenViolations(types)];

        Assert.Equal(["Probe.Runner.RunAsync is a public asynchronous method without a CancellationToken."], violations);
    }

    [Fact]
    public async Task MSG_ENG_007_AC1_PublicTypeNamedAfterTheLibraryIsFound()
    {
        IReadOnlyList<PublicType> types = await BuildProbeAsync(TestContext.Current.CancellationToken);

        string[] violations = [.. SurfaceRules.NameViolations(types)];

        Assert.Equal(["Probe.HermesClock is a public type whose name holds the library's name."], violations);
    }

    [Fact]
    public async Task MSG_ENG_004_AC2_LookupOfMembersByNameIsFound()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        ProbeProject probe = await WriteAndBuildAsync(folder, cancellationToken);

        string[] violations = [.. ReflectionRules.Violations("Probe.dll", AssemblySurface.MemberReferences(probe.Assembly), AssemblySurface.AssemblyReferences(probe.Assembly))];

        Assert.Equal(
            [
                "Probe.dll looks members up by name: System.Type.GetMethod.",
                "Probe.dll looks members up by name: System.Activator.CreateInstance.",
                "Probe.dll looks members up by name: System.Type.GetType.",
            ],
            violations);
    }

    [Fact]
    public async Task MSG_LIB_002_AC2_PublicTypeTheFilesDoNotListIsFound()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        ProbeProject probe = await WriteAndBuildAsync(folder, cancellationToken);
        probe.WriteSource("PublicAPI.Shipped.txt", "#nullable enable\nProbe.Open\nProbe.Open.Open() -> void\nProbe.Listed\n");
        probe.WriteSource("PublicAPI.Unshipped.txt", "#nullable enable\nProbe.Runner\n*REMOVED*Probe.Listed\nProbe.HermesClock\nProbe.Disposer\nProbe.Lookups\n");

        string[] violations = [.. SurfaceRules.ListingViolations(probe.Folder, AssemblySurface.PublicTypes(probe.Assembly))];

        Assert.Equal(["Probe declares the public type Probe.Result`1, which its public API files do not list."], violations);
    }

    private static async Task<IReadOnlyList<PublicType>> BuildProbeAsync(CancellationToken cancellationToken)
    {
        using ScratchFolder folder = new();
        ProbeProject probe = await WriteAndBuildAsync(folder, cancellationToken);
        return AssemblySurface.PublicTypes(probe.Assembly);
    }

    private static async Task<ProbeProject> WriteAndBuildAsync(ScratchFolder folder, CancellationToken cancellationToken)
    {
        ProbeProject probe = new(folder, "Probe");
        probe.WriteProject(string.Empty);
        probe.WriteSource("Probe.cs", """
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            namespace Probe;

            /// <summary>A class neither sealed nor abstract.</summary>
            public class Open
            {
            }

            /// <summary>Holds asynchronous methods.</summary>
            public static class Runner
            {
                /// <summary>Takes no token.</summary>
                /// <returns>A completed task.</returns>
                public static Task RunAsync()
                {
                    return Task.CompletedTask;
                }

                /// <summary>Takes a token.</summary>
                /// <param name="cancellationToken">Cancels it.</param>
                /// <returns>A completed task.</returns>
                public static ValueTask StopAsync(CancellationToken cancellationToken)
                {
                    return ValueTask.CompletedTask;
                }
            }

            /// <summary>A type named after the library.</summary>
            public sealed class HermesClock
            {
            }

            /// <summary>Disposes without a token, as IAsyncDisposable has it.</summary>
            public sealed class Disposer : IAsyncDisposable
            {
                /// <inheritdoc/>
                public ValueTask DisposeAsync()
                {
                    return ValueTask.CompletedTask;
                }
            }

            /// <summary>A generic type.</summary>
            /// <typeparam name="T">Anything.</typeparam>
            public sealed class Result<T>
            {
            }

            /// <summary>Looks members up by name, and reads an embedded resource, which is allowed.</summary>
            public static class Lookups
            {
                /// <summary>Finds a method and a type by name and makes an instance.</summary>
                /// <param name="type">The type to look into.</param>
                /// <returns>What it found.</returns>
                public static object?[] Find(Type type)
                {
                    return [type.GetMethod("Trim"), Activator.CreateInstance(type), Type.GetType("System.String"), typeof(Lookups).Assembly.GetManifestResourceStream("Probe.txt")];
                }
            }
            """);

        // A probe built to break rules is built with its warnings left as warnings.
        ProcessResult build = await probe.BuildAsync(cancellationToken, "-p:TreatWarningsAsErrors=false");
        Assert.True(build.ExitCode == 0, build.Output);
        return probe;
    }
}