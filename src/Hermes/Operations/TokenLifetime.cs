using System;

namespace Hermes.Operations;

/// <summary>
/// The key table's one row of the longest <c>Hermes:Unsubscribe:TokenLifetime</c> of any
/// process that reads <c>tokenKeys</c>; none while no token has been made (<c>06</c> MSG-OPS-008).
/// </summary>
internal sealed class TokenLifetime
{
    public short Id { get; set; } = 1;

    public required TimeSpan Longest { get; set; }
}