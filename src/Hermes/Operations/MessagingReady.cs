using System;
using System.Threading;

namespace Hermes.Operations;

/// <summary>
/// Whether the startup checks have run (<c>04</c> MSG-LIB-003), so a send made before them, by
/// a host's own service started first, is a fault rather than a send recorded unchecked.
/// </summary>
internal sealed class MessagingReady
{
    private int ready;

    internal bool IsReady => Volatile.Read(ref ready) == 1;

    internal void Mark()
    {
        Volatile.Write(ref ready, 1);
    }

    /// <summary>Throws <see cref="InvalidOperationException"/> before the startup checks have run.</summary>
    internal void ThrowIfNotReady()
    {
        if (!IsReady)
        {
            throw new InvalidOperationException("The library is used before the host's start ran its startup checks.");
        }
    }
}