using System;
using System.Net;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Hermes.Development;

/// <summary>
/// The guard on every route of the inbox's group (<c>02</c> MSG-DEV-005): every answer is kept
/// by no cache, sniffed by no browser and sent with no referrer, and a request that does not
/// come from the machine itself under a loopback host name is answered 403, unless
/// <c>Hermes:DevelopmentInbox:AllowRemote</c> is on.
/// </summary>
internal static class InboxGuard
{
    /// <summary>The policy of the page, its script and its style.</summary>
    internal const string PagePolicy = "default-src 'none'; script-src 'self'; style-src 'self'; img-src 'self' data: http: https:; connect-src 'self'; frame-src 'self'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'";

    /// <summary>The policy of an email's HTML, which runs no script and is sandboxed.</summary>
    internal const string HtmlPolicy = "default-src 'none'; img-src data: http: https:; style-src 'unsafe-inline' http: https:; font-src data: http: https:; base-uri 'none'; form-action 'none'; frame-ancestors 'self'; sandbox allow-popups allow-popups-to-escape-sandbox";

    /// <summary>Sets the headers every answer carries, and answers 403 to a request from elsewhere.</summary>
    internal static ValueTask<object?> FilterAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        HttpContext http = context.HttpContext;
        http.Response.Headers.CacheControl = "no-store";
        http.Response.Headers.XContentTypeOptions = "nosniff";
        http.Response.Headers["Referrer-Policy"] = "no-referrer";
        if (!http.RequestServices.GetRequiredService<InboxSettings>().AllowRemote && !IsLocal(http))
        {
            return ValueTask.FromResult<object?>(InboxRefusal.NotLocal);
        }

        return next(context);
    }

    /// <summary>Whether the request comes from a loopback address under the host name <c>localhost</c>, <c>127.0.0.1</c> or <c>[::1]</c>, whatever its port.</summary>
    internal static bool IsLocal(HttpContext http)
    {
        if (http.Connection.RemoteIpAddress is not { } remote)
        {
            return false;
        }

        IPAddress address = remote.IsIPv4MappedToIPv6 ? remote.MapToIPv4() : remote;
        string host = http.Request.Host.Host;
        return IPAddress.IsLoopback(address)
            && (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
                || string.Equals(host, "127.0.0.1", StringComparison.Ordinal)
                || string.Equals(host, "[::1]", StringComparison.Ordinal));
    }
}