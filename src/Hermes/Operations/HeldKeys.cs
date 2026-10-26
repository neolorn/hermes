using System;
using System.Collections.Generic;
using System.Linq;

namespace Hermes.Operations;

/// <summary>The keys the startup checks read for this process's used parts (<c>06</c> MSG-OPS-008).</summary>
internal sealed class HeldKeys
{
    private IReadOnlyList<HeldKey> keys = [];

    /// <summary>The keys held, in the order of <c>08</c> section 7.</summary>
    internal IReadOnlyList<HeldKey> All => keys;

    internal void Hold(IReadOnlyList<HeldKey> read)
    {
        keys = read;
    }

    /// <summary>The key <paramref name="name"/>, or none where no used part needs it.</summary>
    internal HeldKey? Find(string name)
    {
        return keys.FirstOrDefault(key => string.Equals(key.Name, name, StringComparison.Ordinal));
    }
}