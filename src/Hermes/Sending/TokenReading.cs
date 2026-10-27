namespace Hermes.Sending;

/// <summary>What an unsubscribe token reads as (<c>05</c> MSG-API-005).</summary>
/// <param name="Status">Whether it verifies, and if not, why.</param>
/// <param name="Key">For <see cref="TokenStatus.NotHeld"/>, the key whose version is not held.</param>
/// <param name="TokenVersion">The <c>tokenKeys</c> version, or for <see cref="TokenStatus.NotHeld"/> the version not held.</param>
/// <param name="HashVersion">The <c>hashKeys</c> version its hash is made under.</param>
/// <param name="Category">The category.</param>
/// <param name="Language">The language of its pages.</param>
/// <param name="Hash">The destination's keyed hash.</param>
internal sealed record TokenReading(TokenStatus Status, string? Key, int TokenVersion, int HashVersion, string? Category, string? Language, byte[]? Hash)
{
    internal static TokenReading Invalid { get; } = new(TokenStatus.Invalid, null, 0, 0, null, null, null);

    internal static TokenReading Later { get; } = new(TokenStatus.Later, null, 0, 0, null, null, null);

    internal static TokenReading NotHeld(string key, int version)
    {
        return new TokenReading(TokenStatus.NotHeld, key, version, 0, null, null, null);
    }

    internal static TokenReading Valid(int tokenVersion, int hashVersion, string category, string language, byte[] hash)
    {
        return new TokenReading(TokenStatus.Valid, null, tokenVersion, hashVersion, category, language, hash);
    }
}