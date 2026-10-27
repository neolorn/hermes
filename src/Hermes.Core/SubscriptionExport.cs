using System;

namespace Hermes.Core;

/// <summary>A person's push subscription as an export holds it: its service and instants, never its endpoint or keys (<c>01</c> MSG-SEND-020).</summary>
public sealed record SubscriptionExport
{
    /// <summary>The push service its endpoint belongs to.</summary>
    public required PushService Service { get; init; }

    /// <summary>The instant it was registered.</summary>
    public required DateTimeOffset MadeAt { get; init; }

    /// <summary>The instant a push service last accepted a message for it, or none.</summary>
    public DateTimeOffset? LastAcceptedAt { get; init; }
}