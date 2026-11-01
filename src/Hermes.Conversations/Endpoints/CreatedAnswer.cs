using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;

namespace Hermes.Conversations.Endpoints;

/// <summary>A 201 answer whose body is written through the package's serializer context, never the application's (<c>05</c> MSG-API-001).</summary>
/// <typeparam name="T">The body's type.</typeparam>
internal sealed class CreatedAnswer<T>(T body, JsonTypeInfo<T> type) : IResult, IEndpointMetadataProvider
{
    internal T Body { get; } = body;

    public static void PopulateMetadata(MethodInfo method, EndpointBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Metadata.Add(new ProducesResponseTypeMetadata(StatusCodes.Status201Created, typeof(T), ["application/json"]));
    }

    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        httpContext.Response.StatusCode = StatusCodes.Status201Created;
        httpContext.Response.ContentType = "application/json; charset=utf-8";
        return JsonSerializer.SerializeAsync(httpContext.Response.Body, Body, type, httpContext.RequestAborted);
    }
}