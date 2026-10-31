using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Operations;
using Hermes.Sending;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Hermes.Endpoints;

/// <summary>
/// The in-app group (<c>05</c> MSG-API-008): the person signed in reads their list a page at a
/// time, in the language of the request's <c>Accept-Language</c>, and their unread count, and
/// marks one entry, or all of theirs, read.
/// </summary>
internal static class InAppEndpoints
{
    private const string Instant = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    private const int MostSize = 100;

    internal static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/inapp", (Delegate)ListAsync)
            .Produces<ErrorBody>(StatusCodes.Status400BadRequest)
            .Produces<ErrorBody>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
        group.MapGet("/inapp/unread", (Delegate)UnreadAsync)
            .Produces<ErrorBody>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
        group.MapPost("/inapp/{id}/read", MarkAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorBody>(StatusCodes.Status400BadRequest)
            .Produces<ErrorBody>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorBody>(StatusCodes.Status404NotFound)
            .Produces<ErrorBody>(StatusCodes.Status413PayloadTooLarge)
            .Produces<ErrorBody>(StatusCodes.Status415UnsupportedMediaType)
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
        group.MapPost("/inapp/read", (Delegate)MarkAllAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorBody>(StatusCodes.Status400BadRequest)
            .Produces<ErrorBody>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorBody>(StatusCodes.Status413PayloadTooLarge)
            .Produces<ErrorBody>(StatusCodes.Status415UnsupportedMediaType)
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
    }

    /// <summary>
    /// The declared language the first tag of <paramref name="header"/>, in order of weight,
    /// matches as a send's language is matched (<c>01</c> MSG-SEND-003), or none where none
    /// matches or there is no header; a tag of weight 0 and the wildcard match nothing.
    /// </summary>
    internal static string? Language(StringValues header, IReadOnlyList<string> declared)
    {
        if (!StringWithQualityHeaderValue.TryParseList(header, out IList<StringWithQualityHeaderValue>? tags))
        {
            return null;
        }

        foreach (StringWithQualityHeaderValue tag in tags.Select((tag, index) => (tag, index)).OrderByDescending(each => each.tag.Quality ?? 1).ThenBy(each => each.index).Select(each => each.tag))
        {
            string value = tag.Value.ToString();
            if (tag.Quality is 0 || value == "*")
            {
                continue;
            }

            if (declared.FirstOrDefault(language => string.Equals(language, value, StringComparison.OrdinalIgnoreCase)) is { } exact)
            {
                return exact;
            }

            int dash = value.IndexOf('-', StringComparison.Ordinal);
            string primary = dash < 0 ? value : value[..dash];
            if (declared.FirstOrDefault(language => string.Equals(language, primary, StringComparison.OrdinalIgnoreCase)) is { } matched)
            {
                return matched;
            }
        }

        return null;
    }

    private static async Task<Results<JsonAnswer<EntriesBody>, Refusal>> ListAsync(HttpContext context)
    {
        if (await Identity.PersonAsync(context).ConfigureAwait(false) is not { } person)
        {
            return Identity.Unauthenticated();
        }

        IQueryCollection query = context.Request.Query;
        int? size = null;
        if (query.TryGetValue("size", out StringValues sizes))
        {
            if (sizes.Count != 1 || !int.TryParse(sizes[0], NumberStyles.None, CultureInfo.InvariantCulture, out int read) || read is < 1 or > MostSize)
            {
                return RequestBodies.Malformed("size");
            }

            size = read;
        }

        string? cursor = null;
        if (query.TryGetValue("cursor", out StringValues cursors))
        {
            if (cursors.Count != 1)
            {
                return Refusal.Of(StatusCodes.Status400BadRequest, Codes.InAppCursorInvalid);
            }

            cursor = cursors[0];
        }

        MessagingOptions current = Options(context);
        Result<InAppPage> page;
        try
        {
            page = await context.RequestServices.GetRequiredService<IInApp>()
                .ListAsync(person, Language(context.Request.Headers.AcceptLanguage, current.Languages), cursor, size, context.RequestAborted)
                .ConfigureAwait(false);
        }
        catch (Exception failure) when (DispatchPasses.IsLockTimeout(failure))
        {
            return Refusal.Unavailable(current.Dispatch.Interval);
        }

        if (page.Failure is { } refused)
        {
            return refused.Code == Codes.InAppCursorInvalid
                ? Refusal.Of(StatusCodes.Status400BadRequest, Codes.InAppCursorInvalid)
                : Refusal.Unavailable(current.Dispatch.Interval);
        }

        EntriesBody body = new([.. page.Value!.Entries.Select(Of)], page.Value.Next);
        return new JsonAnswer<EntriesBody>(body, EndpointJson.Default.EntriesBody);
    }

    private static async Task<Results<JsonAnswer<UnreadBody>, Refusal>> UnreadAsync(HttpContext context)
    {
        if (await Identity.PersonAsync(context).ConfigureAwait(false) is not { } person)
        {
            return Identity.Unauthenticated();
        }

        try
        {
            int unread = await context.RequestServices.GetRequiredService<IInApp>().UnreadCountAsync(person, context.RequestAborted).ConfigureAwait(false);
            return new JsonAnswer<UnreadBody>(new UnreadBody(unread), EndpointJson.Default.UnreadBody);
        }
        catch (Exception failure) when (DispatchPasses.IsLockTimeout(failure))
        {
            return Refusal.Unavailable(Options(context).Dispatch.Interval);
        }
    }

    private static async Task<Results<NoContent, Refusal>> MarkAsync(HttpContext context, string id)
    {
        if (await Identity.PersonAsync(context).ConfigureAwait(false) is not { } person)
        {
            return Identity.Unauthenticated();
        }

        if (await EmptyAsync(context).ConfigureAwait(false) is { } refusal)
        {
            return refusal;
        }

        if (!Guid.TryParseExact(id, "D", out Guid entry))
        {
            return Refusal.Of(StatusCodes.Status404NotFound, Codes.InAppUnknown);
        }

        Result result;
        try
        {
            result = await context.RequestServices.GetRequiredService<IInApp>().MarkReadAsync(person, new EntryId(entry), context.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception failure) when (DispatchPasses.IsLockTimeout(failure))
        {
            return Refusal.Unavailable(Options(context).Dispatch.Interval);
        }

        return result.Failure is null ? TypedResults.NoContent() : Refusal.Of(StatusCodes.Status404NotFound, Codes.InAppUnknown);
    }

    private static async Task<Results<NoContent, Refusal>> MarkAllAsync(HttpContext context)
    {
        if (await Identity.PersonAsync(context).ConfigureAwait(false) is not { } person)
        {
            return Identity.Unauthenticated();
        }

        if (await EmptyAsync(context).ConfigureAwait(false) is { } refusal)
        {
            return refusal;
        }

        try
        {
            await context.RequestServices.GetRequiredService<IInApp>().MarkAllReadAsync(person, context.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception failure) when (DispatchPasses.IsLockTimeout(failure))
        {
            return Refusal.Unavailable(Options(context).Dispatch.Interval);
        }

        return TypedResults.NoContent();
    }

    /// <summary>The refusal of a body that is not the empty object as <c>application/json</c>, or none.</summary>
    private static async Task<Refusal?> EmptyAsync(HttpContext context)
    {
        (_, Refusal? refusal) = await RequestBodies.JsonAsync(context.Request, EndpointJson.Default.EmptyBody, _ => null).ConfigureAwait(false);
        return refusal;
    }

    private static MessagingOptions Options(HttpContext context)
    {
        return context.RequestServices.GetRequiredService<IOptionsMonitor<MessagingOptions>>().CurrentValue;
    }

    private static EntryBody Of(InAppEntry entry)
    {
        return new EntryBody(
            entry.Entry.Value.ToString("D"),
            entry.Kind,
            entry.Category,
            Text(entry.WrittenAt),
            entry.ReadAt is { } readAt ? Text(readAt) : null,
            entry.Language,
            entry.Title,
            entry.Body,
            entry.Target,
            Values(entry.TargetValues));
    }

    private static string Text(DateTimeOffset instant)
    {
        return instant.UtcDateTime.ToString(Instant, CultureInfo.InvariantCulture);
    }

    /// <summary>The target values as one JSON object: each a string, a number, a boolean or an instant as <c>writtenAt</c> is written.</summary>
    private static JsonElement Values(IReadOnlyDictionary<string, SendValue> values)
    {
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            foreach ((string name, SendValue value) in values)
            {
                switch (value)
                {
                    case SendValue.PlainText text:
                        writer.WriteString(name, text.Value);
                        break;
                    case SendValue.WholeNumber whole:
                        writer.WriteNumber(name, whole.Value);
                        break;
                    case SendValue.DecimalNumber number:
                        writer.WriteNumber(name, number.Value);
                        break;
                    case SendValue.Flag flag:
                        writer.WriteBoolean(name, flag.Value);
                        break;
                    case SendValue.Instant instant:
                        writer.WriteString(name, Text(instant.Value));
                        break;
                    default:
                        break;
                }
            }

            writer.WriteEndObject();
        }

        using JsonDocument document = JsonDocument.Parse(buffer.ToArray());
        return document.RootElement.Clone();
    }
}