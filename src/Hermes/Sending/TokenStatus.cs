namespace Hermes.Sending;

/// <summary>Whether an unsubscribe token verifies (<c>05</c> MSG-API-005).</summary>
internal enum TokenStatus
{
    /// <summary>It verifies.</summary>
    Valid,

    /// <summary>It does not verify: answered 404, suppressing nothing.</summary>
    Invalid,

    /// <summary>It names a key version this process does not hold: 503 where the version is in use, 404 otherwise.</summary>
    NotHeld,

    /// <summary>Its format byte is one kept for a later release: 503, raising no alert.</summary>
    Later,
}