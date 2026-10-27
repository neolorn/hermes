using System.Collections.Generic;

namespace Hermes.Core;

/// <summary>What a recorded send answers: its identifier and one delivery per channel (<c>01</c> MSG-SEND-003).</summary>
public sealed record SendReceipt
{
    /// <summary>The send.</summary>
    public required SendId Send { get; init; }

    /// <summary>The send's deliveries, one per channel.</summary>
    public required IReadOnlyList<DeliveryReceipt> Deliveries { get; init; }
}