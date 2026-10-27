namespace Hermes.Endpoints;

/// <summary>The body of <c>POST /push/subscriptions</c>: the browser's <c>PushSubscription.toJSON()</c> with the key it subscribed with added (<c>05</c> MSG-API-004).</summary>
/// <param name="Endpoint">The subscription's endpoint.</param>
/// <param name="ExpirationTime">The expiry the browser gave, in milliseconds since the epoch, or none.</param>
/// <param name="Keys">The subscription's keys.</param>
/// <param name="ApplicationServerKey">The base64url of the key the browser subscribed with.</param>
internal sealed record SubscriptionBody(string? Endpoint, double? ExpirationTime, SubscriptionKeysBody? Keys, string? ApplicationServerKey);