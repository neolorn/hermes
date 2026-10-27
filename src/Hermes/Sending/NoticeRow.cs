using System;

namespace Hermes.Sending;

/// <summary>A row of the notice table: a final status or a move from <c>sent</c>, kept until the observer takes it (<c>01</c> MSG-SEND-017).</summary>
internal sealed class NoticeRow
{
    public required Guid DeliveryId { get; set; }

    public required int Sequence { get; set; }

    public required string Status { get; set; }

    public string? Reason { get; set; }

    public required DateTimeOffset At { get; set; }

    public Guid? ClaimedBy { get; set; }

    public DateTimeOffset? ClaimExpiresAt { get; set; }
}