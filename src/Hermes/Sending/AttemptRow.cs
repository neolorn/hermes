using System;

namespace Hermes.Sending;

/// <summary>A row of the attempt table: one counted attempt of a delivery, its reference and its answer (<c>01</c> MSG-SEND-005, MSG-SEND-007).</summary>
internal sealed class AttemptRow
{
    public required Guid DeliveryId { get; set; }

    public required int Number { get; set; }

    /// <summary>The delivery reference, 128 random bits as base64url, none for an attempt answered at a check.</summary>
    public string? Reference { get; set; }

    public required DateTimeOffset At { get; set; }

    public string? Class { get; set; }

    public string? Reason { get; set; }
}