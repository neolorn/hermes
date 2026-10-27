namespace Hermes.Endpoints;

/// <summary>The body of <c>DELETE /push/subscriptions</c> (<c>05</c> MSG-API-004).</summary>
/// <param name="Endpoint">The subscription's endpoint.</param>
internal sealed record RemovalBody(string? Endpoint);