using System;

namespace Hermes.Sending;

/// <summary>
/// A person's push subscription (<c>02</c> MSG-PUSH-003): its endpoint and keys protected, its
/// endpoint's keyed hash, the VAPID key version it was made with, its push service and its
/// instants.
/// </summary>
internal sealed class SubscriptionRow
{
    public required Guid Id { get; set; }

    public required string Person { get; set; }

    /// <summary>The keyed hash of the endpoint, domain <c>push-endpoint</c>, unique.</summary>
    public required byte[] EndpointHash { get; set; }

    public required int HashVersion { get; set; }

    /// <summary>The endpoint, protected.</summary>
    public required byte[] Endpoint { get; set; }

    /// <summary>The 65 bytes of <c>p256dh</c> and the 16 of <c>auth</c>, protected.</summary>
    public required byte[] Keys { get; set; }

    /// <summary>The <c>contentKeys</c> version the library's protector wrote the values under, or none for a host's protector.</summary>
    public int? ContentKeyVersion { get; set; }

    /// <summary>The <c>pushVapidKeys</c> version whose public key the browser subscribed with (<c>02</c> MSG-PUSH-007).</summary>
    public required int VapidVersion { get; set; }

    /// <summary>The push service, read from the endpoint's host (<c>08</c> section 2).</summary>
    public required string Service { get; set; }

    public required DateTimeOffset MadeAt { get; set; }

    /// <summary>The expiry the browser gave, or none.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset? LastAcceptedAt { get; set; }
}