using System.Collections.Generic;

namespace Hermes.Core;

/// <summary>A send as the library holds it: its kind and its deliveries (<c>01</c> MSG-SEND-017).</summary>
public sealed record SendRecord
{
    /// <summary>The send.</summary>
    public required SendId Send { get; init; }

    /// <summary>The send's kind.</summary>
    public required string Kind { get; init; }

    /// <summary>The send's deliveries.</summary>
    public required IReadOnlyList<DeliveryRecord> Deliveries { get; init; }
}