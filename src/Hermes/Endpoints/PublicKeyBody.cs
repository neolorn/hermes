namespace Hermes.Endpoints;

/// <summary>The answer of <c>GET /push/key</c> (<c>05</c> MSG-API-004).</summary>
/// <param name="PublicKey">The base64url of the VAPID public key new subscriptions use.</param>
internal sealed record PublicKeyBody(string PublicKey);