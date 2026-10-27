using System;
using System.Collections.Generic;

namespace Hermes.Core;

/// <summary>A delivery's status, its instants and its attempts (<c>01</c> MSG-SEND-017).</summary>
public sealed record DeliveryRecord
{
    /// <summary>The delivery.</summary>
    public required DeliveryId Delivery { get; init; }

    /// <summary>The delivery's channel.</summary>
    public required Channel Channel { get; init; }

    /// <summary>The delivery's status.</summary>
    public required DeliveryStatus Status { get; init; }

    /// <summary>The reason of its status, or none.</summary>
    public AttemptReason? Reason { get; init; }

    /// <summary>When it was recorded.</summary>
    public required DateTimeOffset RecordedAt { get; init; }

    /// <summary>When its status last moved.</summary>
    public required DateTimeOffset LastMovedAt { get; init; }

    /// <summary>Its attempts, in order.</summary>
    public required IReadOnlyList<AttemptRecord> Attempts { get; init; }
}