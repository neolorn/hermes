using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

namespace Hermes.Channels;

/// <summary>
/// Reads <c>pushVapidKeys</c> where the providers are registered alone and the shipped push
/// provider is used: such an application has no key table, so it checks the key's form only
/// (<c>06</c> MSG-OPS-008).
/// </summary>
internal static class PushKeyCheck
{
    internal const string PushVapidKeys = "pushVapidKeys";

    /// <summary>
    /// The versions of <c>pushVapidKeys</c>: a key not answered stops the start as
    /// unavailable; a version numbered below 1 or twice, or not a P-256 scalar, as invalid.
    /// </summary>
    internal static async Task<SortedDictionary<int, byte[]>> ReadAsync(ISecretSource secrets, CancellationToken cancellationToken)
    {
        SecretVersions? read = await secrets.ReadAsync(PushVapidKeys, cancellationToken).ConfigureAwait(false);
        if (read is not { Versions.Count: > 0 })
        {
            throw StartupFailure.Of(ChannelCodes.SecretUnavailable, ("key", PushVapidKeys));
        }

        SortedDictionary<int, byte[]> held = [];
        foreach (SecretVersion version in read.Versions)
        {
            if (version.Version < 1 || held.ContainsKey(version.Version) || !VapidKey.IsScalar(version.Value.Span))
            {
                foreach (byte[] value in held.Values)
                {
                    Array.Clear(value);
                }

                throw StartupFailure.Of(ChannelCodes.SecretInvalid, ("key", PushVapidKeys));
            }

            held[version.Version] = version.Value.ToArray();
        }

        return held;
    }
}