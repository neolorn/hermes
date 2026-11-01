using System;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;

namespace Hermes.Conversations.Endpoints;

/// <summary>
/// A closed conversation's transcript (<c>05</c> MSG-API-010): 200, plain text in UTF-8, as an
/// attachment named <c>transcript-&lt;id&gt;.txt</c>.
/// </summary>
/// <param name="conversation">The conversation's identifier.</param>
/// <param name="text">The transcript.</param>
internal sealed class TranscriptAnswer(Guid conversation, string text) : IResult, IEndpointMetadataProvider
{
    public static void PopulateMetadata(MethodInfo method, EndpointBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Metadata.Add(new ProducesResponseTypeMetadata(StatusCodes.Status200OK, typeof(string), ["text/plain"]));
    }

    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        httpContext.Response.StatusCode = StatusCodes.Status200OK;
        httpContext.Response.ContentType = "text/plain; charset=utf-8";
        httpContext.Response.Headers.ContentDisposition = $"attachment; filename=\"transcript-{conversation:D}.txt\"";
        return httpContext.Response.WriteAsync(text, httpContext.RequestAborted);
    }
}