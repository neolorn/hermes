namespace Hermes.Endpoints;

/// <summary>A subscription's keys, each base64url (<c>02</c> MSG-PUSH-004).</summary>
/// <param name="P256dh">The P-256 public key, 65 bytes.</param>
/// <param name="Auth">The authentication secret, 16 bytes.</param>
internal sealed record SubscriptionKeysBody(string? P256dh, string? Auth);