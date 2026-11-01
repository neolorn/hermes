using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

using Hermes.Conversations.Attachments;
using Hermes.Conversations.Chats;
using Hermes.Conversations.Support;
using Hermes.Core;
using Hermes.Endpoints;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace Hermes.Conversations.Endpoints;

/// <summary>
/// An attachment's upload route (<c>05</c> MSG-API-009, MSG-API-010; <c>09</c> MSG-CONV-007):
/// a single-file <c>multipart/form-data</c> body, its file streamed to the upload as it is read,
/// never held whole. A body that is not one file answers <c>messaging.attachment.invalid</c>,
/// <c>field</c> <c>file</c>, a second file after the first forgetting the first again; a body
/// past the server's bound answers as one over <c>MaxBytes</c>.
/// </summary>
internal static class AttachmentUploads
{
    /// <summary>The room a multipart body takes beyond its file, for its boundaries and headers.</summary>
    private const long Framing = 64 * 1024;

    internal static async Task<Results<CreatedAnswer<AttachmentBody>, Refusal>> UploadAsync(
        HttpContext context,
        Func<string, string, Stream, Task<SupportAnswer<AttachmentRecord>>> upload,
        Func<Guid, Task> forget)
    {
        if (!MediaTypeHeaderValue.TryParse(context.Request.ContentType, out MediaTypeHeaderValue? type)
            || !type.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase)
            || HeaderUtilities.RemoveQuotes(type.Boundary).Value is not { Length: >= 1 and <= 70 } boundary)
        {
            return NotOneFile();
        }

        // The file's bound is the attachments' own, so the server's default does not refuse first.
        long most = context.RequestServices.GetRequiredService<ConversationsSettings>().Attachments.MaxBytes;
        if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } bound)
        {
            bound.MaxRequestBodySize = most + Framing;
        }

        try
        {
            MultipartReader reader = new(boundary, context.Request.Body);
            MultipartSection? section = await reader.ReadNextSectionAsync(context.RequestAborted).ConfigureAwait(false);
            if (section is null
                || !ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out ContentDispositionHeaderValue? disposition)
                || !disposition.IsFileDisposition()
                || HeaderUtilities.RemoveQuotes(disposition.FileNameStar.HasValue ? disposition.FileNameStar : disposition.FileName).Value is not { } name)
            {
                return NotOneFile();
            }

            SupportAnswer<AttachmentRecord> uploaded = await upload(name, section.ContentType ?? string.Empty, section.Body).ConfigureAwait(false);
            if (uploaded.Failure is { } refused)
            {
                if (uploaded.RetryAfter is { } wait)
                {
                    context.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                return StaffEndpoints.Refused(context, refused);
            }

            if (await reader.ReadNextSectionAsync(context.RequestAborted).ConfigureAwait(false) is not null)
            {
                await forget(uploaded.Value!.Attachment.Value).ConfigureAwait(false);
                return NotOneFile();
            }

            return new CreatedAnswer<AttachmentBody>(StaffEndpoints.Of(uploaded.Value!), ConversationsJson.Default.AttachmentBody);
        }
        catch (BadHttpRequestException failure) when (failure.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return Refusal.Of(StatusCodes.Status413PayloadTooLarge, ChatCodes.AttachmentTooLarge);
        }
        catch (InvalidDataException)
        {
            // A multipart body that does not read as one.
            return NotOneFile();
        }
    }

    private static Refusal NotOneFile()
    {
        return Refusal.Of(StatusCodes.Status400BadRequest, ChatCodes.AttachmentInvalid, ("field", "file"));
    }
}