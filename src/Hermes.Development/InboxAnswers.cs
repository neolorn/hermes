using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;

namespace Hermes.Development;

/// <summary>The codes the inbox's group answers (<c>08</c> section 1).</summary>
internal static class InboxCodes
{
    internal const string NotFound = "messaging.inbox.notfound";

    internal const string NotLocal = "messaging.inbox.notlocal";

    internal const string RequestMalformed = "messaging.request.malformed";
}

/// <summary>A 200 answer whose body is written through the inbox's serializer context, never the application's (<c>05</c> MSG-API-001).</summary>
/// <typeparam name="T">The body's type.</typeparam>
internal sealed class InboxAnswer<T>(T body, JsonTypeInfo<T> type) : IResult, IEndpointMetadataProvider
{
    public static void PopulateMetadata(MethodInfo method, EndpointBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Metadata.Add(new ProducesResponseTypeMetadata(StatusCodes.Status200OK, typeof(T), ["application/json"]));
    }

    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        httpContext.Response.StatusCode = StatusCodes.Status200OK;
        httpContext.Response.ContentType = "application/json; charset=utf-8";
        return JsonSerializer.SerializeAsync(httpContext.Response.Body, body, type, httpContext.RequestAborted);
    }
}

/// <summary>A refusal answered as <c>{ "code", "details" }</c> with its status (<c>05</c> MSG-API-001).</summary>
internal sealed class InboxRefusal(int status, string code, IReadOnlyDictionary<string, string> details) : IResult
{
    private static readonly IReadOnlyDictionary<string, string> None = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>A 404 <c>messaging.inbox.notfound</c>.</summary>
    internal static InboxRefusal NotFound { get; } = new(StatusCodes.Status404NotFound, InboxCodes.NotFound, None);

    /// <summary>A 403 <c>messaging.inbox.notlocal</c>.</summary>
    internal static InboxRefusal NotLocal { get; } = new(StatusCodes.Status403Forbidden, InboxCodes.NotLocal, None);

    /// <summary>A 400 <c>messaging.request.malformed</c> naming <paramref name="member"/>.</summary>
    internal static InboxRefusal Malformed(string member)
    {
        return new InboxRefusal(StatusCodes.Status400BadRequest, InboxCodes.RequestMalformed, new Dictionary<string, string>(StringComparer.Ordinal) { ["member"] = member });
    }

    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/json; charset=utf-8";
        return JsonSerializer.SerializeAsync(httpContext.Response.Body, new InboxError(code, details), InboxJson.Default.InboxError, httpContext.RequestAborted);
    }
}