namespace Hermes.Core;

/// <summary>One delivery of a recorded send.</summary>
public sealed record DeliveryReceipt
{
    /// <summary>The delivery's channel.</summary>
    public required Channel Channel { get; init; }

    /// <summary>The delivery.</summary>
    public required DeliveryId Delivery { get; init; }
}