namespace Hermes.Endpoints;

/// <summary>The answer of <c>GET /inapp/unread</c> (<c>05</c> MSG-API-008).</summary>
/// <param name="Unread">The number of the person's entries not yet read.</param>
internal sealed record UnreadBody(int Unread);