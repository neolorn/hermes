using System;
using System.Buffers.Text;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading.Tasks;

using Hermes.Conversations.Chats;
using Hermes.Conversations.Support;
using Hermes.Core;
using Hermes.Endpoints;
using Hermes.Operations;
using Hermes.Sending;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Hermes.Conversations.Endpoints;

/// <summary>
/// The public conversations group (<c>05</c> MSG-API-010): a visitor or a signed-in customer
/// starts a support conversation, or leaves one through the offline form, and reads and writes
/// their own. No route needs a signed-in person: where the identity seam answers one, the
/// conversation is theirs; otherwise the visitor is known by the cookie
/// <c>messaging_visitor</c>, drawn on the first call that needs one (<c>09</c> MSG-CONV-001).
/// A conversation that is not the caller's answers as one unknown.
/// </summary>
internal static class PublicEndpoints
{
    /// <summary>The cookie a visitor's reference is kept in, a name two releases share (<c>08</c> section 8).</summary>
    internal const string Cookie = "messaging_visitor";

    private const string VisitorPrefix = "visitor:";

    private const int MostVersionCharacters = 64;

    internal static void Map(RouteGroupBuilder group, string prefix)
    {
        string path = prefix.Length == 0 ? "/" : prefix;
        group.MapGet("/support/topics", (Delegate)TopicsAsync);
        group.MapPost("/support/notice", (Delegate)((HttpContext context) => NoticeAsync(context, path)))
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorBody>(StatusCodes.Status400BadRequest)
            .Produces<ErrorBody>(StatusCodes.Status403Forbidden)
            .Produces<ErrorBody>(StatusCodes.Status413PayloadTooLarge)
            .Produces<ErrorBody>(StatusCodes.Status415UnsupportedMediaType)
            .Produces<ErrorBody>(StatusCodes.Status429TooManyRequests);
        group.MapPost("/support/conversations", (Delegate)((HttpContext context) => StartAsync(context, path, offline: false)))
            .Produces<ErrorBody>(StatusCodes.Status400BadRequest)
            .Produces<ErrorBody>(StatusCodes.Status403Forbidden)
            .Produces<ErrorBody>(StatusCodes.Status409Conflict)
            .Produces<ErrorBody>(StatusCodes.Status413PayloadTooLarge)
            .Produces<ErrorBody>(StatusCodes.Status415UnsupportedMediaType)
            .Produces<ErrorBody>(StatusCodes.Status429TooManyRequests)
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
        group.MapPost("/support/offline", (Delegate)((HttpContext context) => StartAsync(context, path, offline: true)))
            .Produces<ErrorBody>(StatusCodes.Status400BadRequest)
            .Produces<ErrorBody>(StatusCodes.Status403Forbidden)
            .Produces<ErrorBody>(StatusCodes.Status409Conflict)
            .Produces<ErrorBody>(StatusCodes.Status413PayloadTooLarge)
            .Produces<ErrorBody>(StatusCodes.Status415UnsupportedMediaType)
            .Produces<ErrorBody>(StatusCodes.Status429TooManyRequests)
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
        group.MapGet("/support/conversations/current", (Delegate)((HttpContext context) => CurrentAsync(context, path)))
            .Produces<ErrorBody>(StatusCodes.Status404NotFound)
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
        group.MapGet("/support/conversations/{id}/messages", (HttpContext context, string id) => MessagesAsync(context, path, id))
            .Produces<ErrorBody>(StatusCodes.Status400BadRequest)
            .Produces<ErrorBody>(StatusCodes.Status404NotFound)
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
        group.MapPost("/support/conversations/{id}/messages", (HttpContext context, string id) => WriteAsync(context, path, id))
            .Produces<ErrorBody>(StatusCodes.Status400BadRequest)
            .Produces<ErrorBody>(StatusCodes.Status403Forbidden)
            .Produces<ErrorBody>(StatusCodes.Status404NotFound)
            .Produces<ErrorBody>(StatusCodes.Status409Conflict)
            .Produces<ErrorBody>(StatusCodes.Status413PayloadTooLarge)
            .Produces<ErrorBody>(StatusCodes.Status415UnsupportedMediaType)
            .Produces<ErrorBody>(StatusCodes.Status429TooManyRequests)
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
        group.MapPost("/support/conversations/{id}/read", (HttpContext context, string id) => MarkReadAsync(context, path, id))
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorBody>(StatusCodes.Status400BadRequest)
            .Produces<ErrorBody>(StatusCodes.Status404NotFound)
            .Produces<ErrorBody>(StatusCodes.Status413PayloadTooLarge)
            .Produces<ErrorBody>(StatusCodes.Status415UnsupportedMediaType)
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
        group.MapPost("/support/conversations/{id}/close", (HttpContext context, string id) => CloseAsync(context, path, id))
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorBody>(StatusCodes.Status400BadRequest)
            .Produces<ErrorBody>(StatusCodes.Status404NotFound)
            .Produces<ErrorBody>(StatusCodes.Status413PayloadTooLarge)
            .Produces<ErrorBody>(StatusCodes.Status415UnsupportedMediaType)
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
    }

    /// <summary>
    /// The caller of <paramref name="context"/>: the person the identity seam answers, where one
    /// is registered and answers one; otherwise the visitor the cookie names, renewing it, or one
    /// drawn now where <paramref name="draw"/>; none where there is none.
    /// </summary>
    internal static async Task<SupportCaller?> CallerAsync(HttpContext context, string path, bool draw, bool renew = true)
    {
        if (context.RequestServices.GetService<IRecipientIdentity>() is { } identity
            && await identity.CurrentAsync(context.RequestAborted).ConfigureAwait(false) is { } person
            && StaffChats.IsReference(person))
        {
            return SupportCaller.OfCustomer(person);
        }

        string? reference = context.Request.Cookies[Cookie] is { } kept && IsVisitorReference(kept) ? kept : null;
        if (reference is null && draw)
        {
            reference = VisitorPrefix + Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));
        }

        if (reference is null)
        {
            return null;
        }

        if (renew && !context.Response.HasStarted)
        {
            TimeSpan lives = context.RequestServices.GetRequiredService<ConversationsSettings>().Support.RetentionVisitor;
            context.Response.Cookies.Append(Cookie, reference, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Path = path,
                MaxAge = lives,
            });
        }

        return SupportCaller.OfVisitor(reference);
    }

    /// <summary>The caller of a hub connection's request, as <see cref="CallerAsync"/> reads it, drawing and renewing none.</summary>
    internal static Task<SupportCaller?> HubCallerAsync(HttpContext context)
    {
        return CallerAsync(context, "/", draw: false, renew: false);
    }

    /// <summary>The source address a limit counts: the request's remote address, IPv4 as its four bytes, IPv6 grouped by its /64; none where the request has none.</summary>
    internal static byte[]? Source(HttpContext context)
    {
        if (context.Connection.RemoteIpAddress is not { } address)
        {
            return null;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        byte[] bytes = address.GetAddressBytes();
        return address.AddressFamily == AddressFamily.InterNetworkV6 ? bytes[..8] : bytes;
    }

    private static bool IsVisitorReference(string text)
    {
        return text.Length == VisitorPrefix.Length + 22
            && text.StartsWith(VisitorPrefix, StringComparison.Ordinal)
            && Base64Url.IsValid(text.AsSpan(VisitorPrefix.Length), out int decoded)
            && decoded == 16;
    }

    private static async Task<JsonAnswer<TopicsBody>> TopicsAsync(HttpContext context)
    {
        StaffChats chats = context.RequestServices.GetRequiredService<StaffChats>();
        System.Collections.Generic.IReadOnlyList<(string Name, bool Open)> topics = await chats.TopicsAsync(context.RequestAborted).ConfigureAwait(false);
        return new JsonAnswer<TopicsBody>(new TopicsBody([.. topics.Select(topic => new TopicBody(topic.Name, topic.Open))]), ConversationsJson.Default.TopicsBody);
    }

    private static async Task<Results<NoContent, Refusal>> NoticeAsync(HttpContext context, string path)
    {
        (NoticeRequest? body, Refusal? malformed) = await RequestBodies.JsonAsync(
            context.Request,
            ConversationsJson.Default.NoticeRequest,
            read => read.Version is not { Length: >= 1 and <= MostVersionCharacters } ? "version" : null).ConfigureAwait(false);
        if (malformed is not null)
        {
            return malformed;
        }

        SupportCaller caller = (await CallerAsync(context, path, draw: true).ConfigureAwait(false))!;
        MessagingOptions current = context.RequestServices.GetRequiredService<IOptionsMonitor<MessagingOptions>>().CurrentValue;
        string language = InAppEndpoints.Language(context.Request.Headers.AcceptLanguage, current.Languages) ?? current.DefaultLanguage ?? current.Languages.FirstOrDefault() ?? string.Empty;
        return await CallAsync<NoContent>(context, async chats =>
        {
            SupportAnswer<bool> answer = await chats.AcknowledgeAsync(caller, body!.Version!, language, Source(context), context.RequestAborted).ConfigureAwait(false);
            return answer.Failure is { } refused ? Refused(context, refused, answer.RetryAfter) : TypedResults.NoContent();
        }).ConfigureAwait(false);
    }

    private static async Task<Results<CreatedAnswer<PublicConversationBody>, Refusal>> StartAsync(HttpContext context, string path, bool offline)
    {
        string? topic;
        string? text;
        string? email = null;
        Refusal? malformed;
        if (offline)
        {
            OfflineRequest? body;
            (body, malformed) = await RequestBodies.JsonAsync(context.Request, ConversationsJson.Default.OfflineRequest, read => read.Topic is null ? "topic" : read.Email is null ? "email" : read.Text is null ? "text" : null).ConfigureAwait(false);
            (topic, email, text) = (body?.Topic, body?.Email, body?.Text);
        }
        else
        {
            StartRequest? body;
            (body, malformed) = await RequestBodies.JsonAsync(context.Request, ConversationsJson.Default.StartRequest, read => read.Topic is null ? "topic" : read.Text is null ? "text" : null).ConfigureAwait(false);
            (topic, text) = (body?.Topic, body?.Text);
        }

        if (malformed is not null)
        {
            return malformed;
        }

        SupportCaller caller = (await CallerAsync(context, path, draw: true).ConfigureAwait(false))!;
        return await CallAsync<CreatedAnswer<PublicConversationBody>>(context, async chats =>
        {
            MessagingOptions current = context.RequestServices.GetRequiredService<IOptionsMonitor<MessagingOptions>>().CurrentValue;
            string language = InAppEndpoints.Language(context.Request.Headers.AcceptLanguage, current.Languages) ?? current.DefaultLanguage ?? current.Languages.FirstOrDefault() ?? string.Empty;
            SupportAnswer<ConversationRecord> started = await chats.StartSupportAsync(caller, topic!, text!, email, Source(context), language, context.RequestAborted).ConfigureAwait(false);
            return started.Failure is { } refused
                ? Refused(context, refused, started.RetryAfter)
                : new CreatedAnswer<PublicConversationBody>(StaffEndpoints.OfPublic(started.Value!), ConversationsJson.Default.PublicConversationBody);
        }).ConfigureAwait(false);
    }

    private static async Task<Results<JsonAnswer<PublicConversationBody>, Refusal>> CurrentAsync(HttpContext context, string path)
    {
        if (await CallerAsync(context, path, draw: false).ConfigureAwait(false) is not { } caller)
        {
            return StaffEndpoints.NotFound();
        }

        return await CallAsync<JsonAnswer<PublicConversationBody>>(context, async chats =>
            await chats.CurrentAsync(caller, context.RequestAborted).ConfigureAwait(false) is { } current
                ? new JsonAnswer<PublicConversationBody>(StaffEndpoints.OfPublic(current), ConversationsJson.Default.PublicConversationBody)
                : StaffEndpoints.NotFound()).ConfigureAwait(false);
    }

    private static async Task<Results<JsonAnswer<MessagesBody>, Refusal>> MessagesAsync(HttpContext context, string path, string id)
    {
        if (await CallerAsync(context, path, draw: false).ConfigureAwait(false) is not { } caller || !Guid.TryParseExact(id, "D", out Guid conversation))
        {
            return StaffEndpoints.NotFound();
        }

        IQueryCollection query = context.Request.Query;
        if (!StaffEndpoints.TrySize(query, out int? size))
        {
            return RequestBodies.Malformed("size");
        }

        if (!TryMessage(query, "before", out MessageId? before))
        {
            return RequestBodies.Malformed("before");
        }

        if (!TryMessage(query, "after", out MessageId? after) || (after is not null && before is not null))
        {
            return RequestBodies.Malformed("after");
        }

        return await CallAsync<JsonAnswer<MessagesBody>>(context, async chats =>
        {
            Result<MessagePage> page = await chats.CallerMessagesAsync(caller, conversation, before, after, size, context.RequestAborted).ConfigureAwait(false);
            return page.Failure is { } refused
                ? Refused(context, refused, null)
                : new JsonAnswer<MessagesBody>(new MessagesBody([.. page.Value!.Messages.Select(StaffEndpoints.Of)]), ConversationsJson.Default.MessagesBody);
        }).ConfigureAwait(false);
    }

    private static async Task<Results<CreatedAnswer<MessageBody>, Refusal>> WriteAsync(HttpContext context, string path, string id)
    {
        (EditRequest? body, Refusal? malformed) = await RequestBodies.JsonAsync(context.Request, ConversationsJson.Default.EditRequest, read => read.Text is null ? "text" : null).ConfigureAwait(false);
        if (malformed is not null)
        {
            return malformed;
        }

        if (await CallerAsync(context, path, draw: false).ConfigureAwait(false) is not { } caller || !Guid.TryParseExact(id, "D", out Guid conversation))
        {
            return StaffEndpoints.NotFound();
        }

        return await CallAsync<CreatedAnswer<MessageBody>>(context, async chats =>
        {
            SupportAnswer<MessageRecord> written = await chats.CallerWriteAsync(caller, conversation, body!.Text!, context.RequestAborted).ConfigureAwait(false);
            return written.Failure is { } refused
                ? Refused(context, refused, written.RetryAfter)
                : new CreatedAnswer<MessageBody>(StaffEndpoints.Of(written.Value!), ConversationsJson.Default.MessageBody);
        }).ConfigureAwait(false);
    }

    private static async Task<Results<NoContent, Refusal>> MarkReadAsync(HttpContext context, string path, string id)
    {
        (ReadRequest? body, Refusal? malformed) = await RequestBodies.JsonAsync(context.Request, ConversationsJson.Default.ReadRequest, read => read.MessageId is null ? "messageId" : null).ConfigureAwait(false);
        if (malformed is not null)
        {
            return malformed;
        }

        if (!Guid.TryParseExact(body!.MessageId, "D", out Guid message))
        {
            return RequestBodies.Malformed("messageId");
        }

        if (await CallerAsync(context, path, draw: false).ConfigureAwait(false) is not { } caller || !Guid.TryParseExact(id, "D", out Guid conversation))
        {
            return StaffEndpoints.NotFound();
        }

        return await CallAsync<NoContent>(context, async chats =>
        {
            Result marked = await chats.CallerMarkReadAsync(caller, conversation, message, context.RequestAborted).ConfigureAwait(false);
            return marked.Failure is { } refused ? Refused(context, refused, null) : TypedResults.NoContent();
        }).ConfigureAwait(false);
    }

    private static async Task<Results<NoContent, Refusal>> CloseAsync(HttpContext context, string path, string id)
    {
        (_, Refusal? malformed) = await RequestBodies.JsonAsync(context.Request, EndpointJson.Default.EmptyBody, _ => null).ConfigureAwait(false);
        if (malformed is not null)
        {
            return malformed;
        }

        if (await CallerAsync(context, path, draw: false).ConfigureAwait(false) is not { } caller || !Guid.TryParseExact(id, "D", out Guid conversation))
        {
            return StaffEndpoints.NotFound();
        }

        return await CallAsync<NoContent>(context, async chats =>
        {
            Result closed = await chats.CallerCloseAsync(caller, conversation, context.RequestAborted).ConfigureAwait(false);
            return closed.Failure is { } refused ? Refused(context, refused, null) : TypedResults.NoContent();
        }).ConfigureAwait(false);
    }

    private static bool TryMessage(IQueryCollection query, string name, out MessageId? message)
    {
        message = null;
        if (!query.TryGetValue(name, out StringValues values))
        {
            return true;
        }

        if (values.Count != 1 || !Guid.TryParseExact(values[0], "D", out Guid named))
        {
            return false;
        }

        message = new MessageId(named);
        return true;
    }

    /// <summary>Calls the operation, a customer's locks not had in time, or a value that cannot open yet, answering 503.</summary>
    private static async Task<Results<T, Refusal>> CallAsync<T>(HttpContext context, Func<StaffChats, Task<Results<T, Refusal>>> call)
        where T : IResult
    {
        try
        {
            return await call(context.RequestServices.GetRequiredService<StaffChats>()).ConfigureAwait(false);
        }
        catch (Exception failure) when (DispatchPasses.IsLockTimeout(failure))
        {
            return StaffEndpoints.Unavailable(context);
        }
    }

    /// <summary>A failure an operation answered, a limit's with <c>Retry-After</c> in whole seconds.</summary>
    private static Refusal Refused(HttpContext context, Failure failure, TimeSpan? retryAfter)
    {
        if (retryAfter is { } wait)
        {
            context.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        return StaffEndpoints.Refused(context, failure);
    }
}