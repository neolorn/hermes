using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Hermes.Endpoints;

/// <summary>Reads the endpoints' request bodies, each bounded at 16 KiB (<c>05</c> MSG-API-001).</summary>
internal static class RequestBodies
{
    /// <summary>The most bytes a request body holds.</summary>
    internal const int MostBytes = 16 * 1024;

    /// <summary>
    /// Reads a JSON body through the library's serializer context: a body sent as any type but
    /// <c>application/json</c> is 415, one over 16 KiB 413, and one that does not read 400 naming
    /// the member at fault where there is one; <paramref name="reads"/> answers the member a body
    /// that parsed lacks, or none.
    /// </summary>
    internal static async Task<(T? Body, Refusal? Refusal)> JsonAsync<T>(HttpRequest request, JsonTypeInfo<T> type, Func<T, string?> reads)
        where T : class
    {
        if (!IsJson(request.ContentType))
        {
            return (null, Refusal.Of(StatusCodes.Status415UnsupportedMediaType, Codes.RequestMediaType));
        }

        if (await ReadAsync(request, request.HttpContext.RequestAborted).ConfigureAwait(false) is not { } bytes)
        {
            return (null, TooLarge());
        }

        T? body;
        try
        {
            body = JsonSerializer.Deserialize(bytes, type);
        }
        catch (JsonException failure)
        {
            return (null, Malformed(Member(failure.Path)));
        }

        if (body is null)
        {
            return (null, Malformed(member: null));
        }

        return reads(body) is { } missing ? (null, Malformed(missing)) : (body, null);
    }

    /// <summary>The body, or none where it is over 16 KiB, read no further than one byte past.</summary>
    internal static async Task<byte[]?> ReadAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength > MostBytes)
        {
            return null;
        }

        byte[] buffer = new byte[MostBytes + 1];
        int length = 0;
        while (length < buffer.Length)
        {
            int read = await request.Body.ReadAsync(buffer.AsMemory(length), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            length += read;
        }

        return length > MostBytes ? null : buffer.AsSpan(0, length).ToArray();
    }

    internal static Refusal TooLarge()
    {
        return Refusal.Of(StatusCodes.Status413RequestEntityTooLarge, Codes.RequestTooLarge);
    }

    internal static Refusal Malformed(string? member)
    {
        return member is null
            ? Refusal.Of(StatusCodes.Status400BadRequest, Codes.RequestMalformed)
            : Refusal.Of(StatusCodes.Status400BadRequest, Codes.RequestMalformed, ("member", member));
    }

    private static bool IsJson(string? contentType)
    {
        return MediaTypeHeaderValue.TryParse(contentType, out MediaTypeHeaderValue? media)
            && media.MediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The member a reader's path names, <c>$.keys.auth</c> naming <c>keys.auth</c>, or none for the whole body.</summary>
    private static string? Member(string? path)
    {
        return path is { Length: > 2 } && path.StartsWith("$.", StringComparison.Ordinal) ? path[2..] : null;
    }
}