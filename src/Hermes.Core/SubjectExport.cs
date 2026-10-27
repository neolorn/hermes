using System.Collections.Generic;

namespace Hermes.Core;

/// <summary>What the library holds for a person (<c>01</c> MSG-SEND-020).</summary>
public sealed record SubjectExport
{
    /// <summary>The person's preferences, every declared category with the channels its kinds use.</summary>
    public required IReadOnlyList<CategoryPreferences> Preferences { get; init; }

    /// <summary>The person's push subscriptions, never their endpoints or keys.</summary>
    public required IReadOnlyList<SubscriptionExport> Subscriptions { get; init; }

    /// <summary>Every delivery whose record names the person.</summary>
    public required IReadOnlyList<DeliveryExport> Deliveries { get; init; }

    /// <summary>The person's live idempotency rows.</summary>
    public required IReadOnlyList<IdempotencyKeyExport> IdempotencyKeys { get; init; }
}