namespace Hermes.Sending;

/// <summary>
/// What protecting a value answers: the value to store, or the key that is unavailable, a
/// version this process does not hold or a content key past its use, for which a library
/// call answers <c>messaging.key.unavailable</c> (<c>06</c> MSG-OPS-008; <c>08</c> section 1).
/// </summary>
/// <param name="Stored">The envelope to store, where the value was protected.</param>
/// <param name="Key">The secret, or the protector's <c>Id</c>, that is unavailable.</param>
/// <param name="Version">The unavailable version, where there is one.</param>
internal readonly record struct ContentProtecting(byte[]? Stored, string? Key, int? Version)
{
    internal static ContentProtecting Unavailable(string key, int? version)
    {
        return new ContentProtecting(null, key, version);
    }

    /// <summary>Prints whether the value was protected and the key, never the value.</summary>
    public override string ToString()
    {
        return Stored is not null ? "Protected" : $"Unavailable ({Key}{(Version is null ? string.Empty : $" version {Version}")})";
    }
}