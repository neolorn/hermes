using System.Collections.Generic;

namespace Hermes.Operations;

/// <summary>One of the five keys as this process holds it: its versions and the one new values are made under.</summary>
/// <param name="Name">The key's name, of <c>08</c> section 7.</param>
/// <param name="Versions">The versions held, by number.</param>
/// <param name="WriteVersion">The version new values are made under.</param>
internal sealed record HeldKey(string Name, IReadOnlyDictionary<int, byte[]> Versions, int WriteVersion)
{
    /// <summary>The write version's bytes.</summary>
    internal byte[] Write => Versions[WriteVersion];

    /// <summary>Prints the key's name and version numbers, never its bytes.</summary>
    public override string ToString()
    {
        return $"{Name} (versions {string.Join(", ", Versions.Keys)}; write version {WriteVersion})";
    }
}