namespace Hermes.Endpoints;

/// <summary>The empty object the in-app list's <c>POST</c> routes read as <c>application/json</c> alone, so a cross-site form cannot reach them (<c>05</c> MSG-API-008).</summary>
internal sealed record EmptyBody;