using Microsoft.AspNetCore.Http;

namespace Hermes.Endpoints;

/// <summary>
/// The headers every answer of the unsubscribe group carries (<c>05</c> MSG-API-005), so the
/// token in the address leaves the page with nothing it loads, and no answer is kept.
/// </summary>
internal static class PageHeaders
{
    internal const string ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; form-action 'self'; base-uri 'none'; frame-ancestors 'none'";

    internal static void Apply(HttpResponse response)
    {
        response.Headers.CacheControl = "no-store";
        response.Headers["Referrer-Policy"] = "no-referrer";
        response.Headers.XContentTypeOptions = "nosniff";
        response.Headers.ContentSecurityPolicy = ContentSecurityPolicy;
    }
}