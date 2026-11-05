using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

namespace Hermes.Sms.Cequens;

/// <summary>
/// The account's access token, the secret <c>smsCequens</c> (<c>02</c> MSG-SMS-007): 1 to
/// 8,192 bytes of printable ASCII with no space, its highest version used.
/// </summary>
internal static class CequensToken
{
    /// <summary>The secret's name.</summary>
    internal const string Name = "smsCequens";

    private const int MaxLength = 8192;

    /// <summary>The highest version of <c>smsCequens</c>, or none where the source answers no version.</summary>
    internal static async Task<SecretVersion?> HighestAsync(ISecretSource secrets, CancellationToken cancellationToken)
    {
        SecretVersions? read = await secrets.ReadAsync(Name, cancellationToken).ConfigureAwait(false);
        return read is { Versions.Count: > 0 } ? read.Versions.MaxBy(version => version.Version) : null;
    }

    /// <summary>Whether <paramref name="value"/> is within the token's form.</summary>
    internal static bool IsInForm(ReadOnlySpan<byte> value)
    {
        return value.Length is >= 1 and <= MaxLength && !value.ContainsAnyExceptInRange((byte)'!', (byte)'~');
    }
}