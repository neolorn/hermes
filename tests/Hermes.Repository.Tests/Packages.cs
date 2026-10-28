using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace Hermes.Repository.Tests;

/// <summary>
/// The packages of the first release (<c>00</c> section 2.2, <c>04</c> MSG-LIB-001), each a
/// project under <c>src/</c>.
/// </summary>
internal static class Packages
{
    internal const string Core = "Hermes.Core";

    internal const string Channels = "Hermes.Channels";

    internal const string Engine = "Hermes";

    internal const string Testing = "Hermes.Testing";

    internal const string Cli = "Hermes.Cli";

    /// <summary>The packages' authors, the name the owner's first message of phase 0 gives (<c>07</c> MSG-ENG-011).</summary>
    internal const string Authors = "neolorn";

    /// <summary>The repository's address, which every package carries (<c>07</c> MSG-ENG-011).</summary>
    internal const string RepositoryUrl = "https://github.com/neolorn/hermes";

    /// <summary>The test project over the whole repository, the one with no source project.</summary>
    internal const string RepositoryTests = "Hermes.Repository.Tests";

    internal static IReadOnlyList<string> All { get; } = [Core, Channels, Engine, Testing, Cli];

    /// <summary>The folders a project may hold, by project (<c>07</c> MSG-ENG-001); every other project is flat.</summary>
    internal static FrozenDictionary<string, IReadOnlyList<string>> Folders { get; } = new Dictionary<string, IReadOnlyList<string>>
    {
        [Engine] = ["Sending", "Content", "Endpoints", "Operations", "Migrations"],
        [Channels] = ["Email", "Sms", "Push"],
    }.ToFrozenDictionary(StringComparer.Ordinal);

    internal static string TestProject(string package)
    {
        return package + ".Tests";
    }
}