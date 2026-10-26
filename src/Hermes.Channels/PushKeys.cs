using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

namespace Hermes.Channels;

/// <summary>The versions of <c>pushVapidKeys</c> this process holds, each by its version (<c>02</c> MSG-PUSH-007).</summary>
internal sealed class PushKeys(TimeProvider time)
{
    private FrozenDictionary<int, VapidKey> held = FrozenDictionary<int, VapidKey>.Empty;

    /// <summary>Holds <paramref name="versions"/>, each a P-256 scalar, in place of those held.</summary>
    internal void Hold(IEnumerable<KeyValuePair<int, byte[]>> versions)
    {
        held = versions.ToFrozenDictionary(version => version.Key, version => VapidKey.FromScalar(version.Value, time));
    }

    /// <summary>The key of <paramref name="version"/>, or none where this process does not hold it.</summary>
    internal VapidKey? Find(int version)
    {
        return held.GetValueOrDefault(version);
    }
}