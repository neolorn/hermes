using System;

namespace Hermes.Sending;

/// <summary>A row of the call table: one provider call of an attempt, its class, reason and status, never the provider's text (<c>01</c> MSG-SEND-007).</summary>
internal sealed class CallRow
{
    public required Guid DeliveryId { get; set; }

    public required int Attempt { get; set; }

    public required int Number { get; set; }

    public required string Class { get; set; }

    public string? Reason { get; set; }

    public int? Status { get; set; }

    /// <summary>The push subscription's row, never its endpoint.</summary>
    public Guid? SubscriptionId { get; set; }

    public string? PushService { get; set; }

    public required DateTimeOffset At { get; set; }
}