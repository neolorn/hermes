using System;
using System.Reflection;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Net.Http.Headers;

namespace Hermes.Conversations.Endpoints;

/// <summary>
/// A clean attachment's download (<c>05</c> MSG-API-009, MSG-API-010; <c>09</c>
/// MSG-CONV-007): 200, its media type, streamed through the library as its chunks open, with
/// <c>Content-Disposition: attachment</c> naming its file and <c>X-Content-Type-Options:
/// nosniff</c>, never a link to the store.
/// </summary>
/// <param name="download">The attachment and its content, disposed once written.</param>
internal sealed class AttachmentAnswer(AttachmentDownload download) : IResult, IEndpointMetadataProvider
{
    public static void PopulateMetadata(MethodInfo method, EndpointBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Metadata.Add(new ProducesResponseTypeMetadata(StatusCodes.Status200OK, typeof(byte[]), ["application/octet-stream"]));
    }

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        await using (download.Content.ConfigureAwait(false))
        {
            ContentDispositionHeaderValue disposition = new("attachment");
            disposition.SetHttpFileName(download.Attachment.Name);
            httpContext.Response.StatusCode = StatusCodes.Status200OK;
            httpContext.Response.ContentType = download.Attachment.MediaType;
            httpContext.Response.ContentLength = download.Attachment.Size;
            httpContext.Response.Headers.ContentDisposition = disposition.ToString();
            httpContext.Response.Headers.XContentTypeOptions = "nosniff";
            await download.Content.CopyToAsync(httpContext.Response.Body, httpContext.RequestAborted).ConfigureAwait(false);
        }
    }
}