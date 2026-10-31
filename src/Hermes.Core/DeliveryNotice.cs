using System;
using System.Globalization;
using System.Text;

namespace Hermes.Core;

/// <summary>A status a delivery reached; it carries no destination or content, and never prints the person or the idempotency key.</summary>
public sealed record DeliveryNotice
{
    /// <summary>The send.</summary>
    public required SendId Send { get; init; }

    /// <summary>The delivery.</summary>
    public required DeliveryId Delivery { get; init; }

    /// <summary>The send's kind.</summary>
    public required string Kind { get; init; }

    /// <summary>The kind's category.</summary>
    public required string Category { get; init; }

    /// <summary>The delivery's channel.</summary>
    public required Channel Channel { get; init; }

    /// <summary>The person's reference, or none for an anonymous recipient.</summary>
    public string? Person { get; init; }

    /// <summary>The status reached.</summary>
    public required DeliveryStatus Status { get; init; }

    /// <summary>The reason the status was reached, or none.</summary>
    public AttemptReason? Reason { get; init; }

    /// <summary>When the status was reached.</summary>
    public required DateTimeOffset At { get; init; }

    /// <summary>The idempotency key the send was recorded with, as the host gave it, or none where it had none or its person was erased.</summary>
    public string? IdempotencyKey { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(
            CultureInfo.InvariantCulture,
            $"Send = {Send}, Delivery = {Delivery}, Kind = {Kind}, Category = {Category}, Channel = {Channel}, Status = {Status}, Reason = {Reason}, At = {At:O}");
        return true;
    }
}