using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
/// The staff conversations group (<c>05</c> MSG-API-009): each route thin over one operation of
/// <see cref="IConversations"/>, for the person the identity seam answers, its bodies and
/// refusals the engine's (<c>05</c> MSG-API-001). A route naming a conversation by an
/// identifier that does not read answers as one the person may not read.
/// </summary>
internal static class StaffEndpoints
{
    private const string Instant = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    /// <summary>The forms an instant is read in: ISO 8601 in UTC, with or without milliseconds.</summary>
    private static readonly string[] Instants = [Instant, "yyyy-MM-dd'T'HH:mm:ss'Z'"];

    private const int MostSize = 100;

    internal static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/conversations", (Delegate)ListAsync)
            .Produces<ErrorBody>(StatusCodes.Status400BadRequest)
            .Produces<ErrorBody>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
        group.MapPost("/conversations/direct", (Delegate)StartAsync)
            .Produces<ErrorBody>(StatusCodes.Status400BadRequest)
            .Produces<ErrorBody>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorBody>(StatusCodes.Status413PayloadTooLarge)
            .Produces<ErrorBody>(StatusCodes.Status415UnsupportedMediaType)
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
        group.MapGet("/conversations/group/{kind}/{reference}", (Delegate)OpenGroupAsync)
            .Produces<ErrorBody>(StatusCodes.Status400BadRequest)
            .Produces<ErrorBody>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorBody>(StatusCodes.Status404NotFound)
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
        group.MapGet("/conversations/{id}", (Delegate)ReadAsync)
            .Produces<ErrorBody>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorBody>(StatusCodes.Status404NotFound)
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
        group.MapGet("/conversations/{id}/messages", (Delegate)MessagesAsync)
            .Produces<ErrorBody>(StatusCodes.Status400BadRequest)
            .Produces<ErrorBody>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorBody>(StatusCodes.Status404NotFound)
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
        group.MapPost("/conversations/{id}/messages", (Delegate)WriteAsync)
            .Produces<ErrorBody>(StatusCodes.Status400BadRequest)
            .Produces<ErrorBody>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorBody>(StatusCodes.Status404NotFound)
            .Produces<ErrorBody>(StatusCodes.Status413PayloadTooLarge)
            .Produces<ErrorBody>(StatusCodes.Status415UnsupportedMediaType)
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
        group.MapPut("/conversations/{id}/messages/{messageId}", (Delegate)EditAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorBody>(StatusCodes.Status400BadRequest)
            .Produces<ErrorBody>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorBody>(StatusCodes.Status403Forbidden)
            .Produces<ErrorBody>(StatusCodes.Status404NotFound)
            .Produces<ErrorBody>(StatusCodes.Status409Conflict)
            .Produces<ErrorBody>(StatusCodes.Status413PayloadTooLarge)
            .Produces<ErrorBody>(StatusCodes.Status415UnsupportedMediaType)
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
        group.MapDelete("/conversations/{id}/messages/{messageId}", (Delegate)DeleteAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorBody>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorBody>(StatusCodes.Status403Forbidden)
            .Produces<ErrorBody>(StatusCodes.Status404NotFound)
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
        group.MapPost("/conversations/{id}/read", (Delegate)MarkReadAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorBody>(StatusCodes.Status400BadRequest)
            .Produces<ErrorBody>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorBody>(StatusCodes.Status404NotFound)
            .Produces<ErrorBody>(StatusCodes.Status413PayloadTooLarge)
            .Produces<ErrorBody>(StatusCodes.Status415UnsupportedMediaType)
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
        group.MapPut("/conversations/{id}/mute", (Delegate)MuteAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorBody>(StatusCodes.Status400BadRequest)
            .Produces<ErrorBody>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorBody>(StatusCodes.Status404NotFound)
            .Produces<ErrorBody>(StatusCodes.Status413PayloadTooLarge)
            .Produces<ErrorBody>(StatusCodes.Status415UnsupportedMediaType)
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
        group.MapPost("/conversations/{id}/participants", (Delegate)AddAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorBody>(StatusCodes.Status400BadRequest)
            .Produces<ErrorBody>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorBody>(StatusCodes.Status404NotFound)
            .Produces<ErrorBody>(StatusCodes.Status409Conflict)
            .Produces<ErrorBody>(StatusCodes.Status413PayloadTooLarge)
            .Produces<ErrorBody>(StatusCodes.Status415UnsupportedMediaType)
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
        group.MapPost("/conversations/{id}/leave", (Delegate)LeaveAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorBody>(StatusCodes.Status400BadRequest)
            .Produces<ErrorBody>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorBody>(StatusCodes.Status404NotFound)
            .Produces<ErrorBody>(StatusCodes.Status413PayloadTooLarge)
            .Produces<ErrorBody>(StatusCodes.Status415UnsupportedMediaType)
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
        group.MapGet("/support/queue", (Delegate)QueueAsync)
            .Produces<ErrorBody>(StatusCodes.Status400BadRequest)
            .Produces<ErrorBody>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
        group.MapPut("/support/availability", (Delegate)AvailabilityAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorBody>(StatusCodes.Status400BadRequest)
            .Produces<ErrorBody>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorBody>(StatusCodes.Status413PayloadTooLarge)
            .Produces<ErrorBody>(StatusCodes.Status415UnsupportedMediaType)
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
        group.MapPost("/support/conversations/{id}/transfer", (Delegate)TransferAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorBody>(StatusCodes.Status400BadRequest)
            .Produces<ErrorBody>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorBody>(StatusCodes.Status404NotFound)
            .Produces<ErrorBody>(StatusCodes.Status413PayloadTooLarge)
            .Produces<ErrorBody>(StatusCodes.Status415UnsupportedMediaType)
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
        group.MapPost("/support/conversations/{id}/close", (Delegate)CloseAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorBody>(StatusCodes.Status400BadRequest)
            .Produces<ErrorBody>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorBody>(StatusCodes.Status404NotFound)
            .Produces<ErrorBody>(StatusCodes.Status413PayloadTooLarge)
            .Produces<ErrorBody>(StatusCodes.Status415UnsupportedMediaType)
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
        group.MapPost("/support/conversations/{id}/block", (Delegate)BlockAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorBody>(StatusCodes.Status400BadRequest)
            .Produces<ErrorBody>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorBody>(StatusCodes.Status404NotFound)
            .Produces<ErrorBody>(StatusCodes.Status413PayloadTooLarge)
            .Produces<ErrorBody>(StatusCodes.Status415UnsupportedMediaType)
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
        group.MapDelete("/support/conversations/{id}/block", (Delegate)UnblockAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorBody>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorBody>(StatusCodes.Status404NotFound)
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
        group.MapPut("/support/conversations/{id}/tags", (Delegate)TagsAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorBody>(StatusCodes.Status400BadRequest)
            .Produces<ErrorBody>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorBody>(StatusCodes.Status404NotFound)
            .Produces<ErrorBody>(StatusCodes.Status409Conflict)
            .Produces<ErrorBody>(StatusCodes.Status413PayloadTooLarge)
            .Produces<ErrorBody>(StatusCodes.Status415UnsupportedMediaType)
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
        group.MapPost("/support/conversations/{id}/snooze", (Delegate)SnoozeAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorBody>(StatusCodes.Status400BadRequest)
            .Produces<ErrorBody>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorBody>(StatusCodes.Status404NotFound)
            .Produces<ErrorBody>(StatusCodes.Status409Conflict)
            .Produces<ErrorBody>(StatusCodes.Status413PayloadTooLarge)
            .Produces<ErrorBody>(StatusCodes.Status415UnsupportedMediaType)
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
        group.MapGet("/support/replies", (Delegate)RepliesAsync)
            .Produces<ErrorBody>(StatusCodes.Status400BadRequest)
            .Produces<ErrorBody>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorBody>(StatusCodes.Status404NotFound)
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
        group.MapPost("/support/replies", (Delegate)AddReplyAsync)
            .Produces<ErrorBody>(StatusCodes.Status400BadRequest)
            .Produces<ErrorBody>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorBody>(StatusCodes.Status404NotFound)
            .Produces<ErrorBody>(StatusCodes.Status409Conflict)
            .Produces<ErrorBody>(StatusCodes.Status413PayloadTooLarge)
            .Produces<ErrorBody>(StatusCodes.Status415UnsupportedMediaType)
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
        group.MapPut("/support/replies/{id}", (Delegate)ChangeReplyAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorBody>(StatusCodes.Status400BadRequest)
            .Produces<ErrorBody>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorBody>(StatusCodes.Status404NotFound)
            .Produces<ErrorBody>(StatusCodes.Status409Conflict)
            .Produces<ErrorBody>(StatusCodes.Status413PayloadTooLarge)
            .Produces<ErrorBody>(StatusCodes.Status415UnsupportedMediaType)
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
        group.MapDelete("/support/replies/{id}", (Delegate)RemoveReplyAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorBody>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorBody>(StatusCodes.Status404NotFound)
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<Results<NoContent, Refusal>> TagsAsync(HttpContext context, string id)
    {
        if (await Identity.PersonAsync(context).ConfigureAwait(false) is not { } person)
        {
            return Identity.Unauthenticated();
        }

        (TagsRequest? body, Refusal? malformed) = await RequestBodies.JsonAsync(context.Request, ConversationsJson.Default.TagsRequest, read => read.Tags is null || read.Tags.Any(tag => tag is null) ? "tags" : null).ConfigureAwait(false);
        if (malformed is not null)
        {
            return malformed;
        }

        if (!Guid.TryParseExact(id, "D", out Guid conversation))
        {
            return NotFound();
        }

        return await DoneAsync(context, conversations => conversations.SetTagsAsync(person, new ConversationId(conversation), [.. body!.Tags!], context.RequestAborted)).ConfigureAwait(false);
    }

    private static async Task<Results<NoContent, Refusal>> SnoozeAsync(HttpContext context, string id)
    {
        if (await Identity.PersonAsync(context).ConfigureAwait(false) is not { } person)
        {
            return Identity.Unauthenticated();
        }

        (SnoozeRequest? body, Refusal? malformed) = await RequestBodies.JsonAsync(
            context.Request,
            ConversationsJson.Default.SnoozeRequest,
            read => TryInstant(read.Until, out _) ? null : "until").ConfigureAwait(false);
        if (malformed is not null)
        {
            return malformed;
        }

        if (!Guid.TryParseExact(id, "D", out Guid conversation))
        {
            return NotFound();
        }

        _ = TryInstant(body!.Until, out DateTimeOffset until);
        return await DoneAsync(context, conversations => conversations.SnoozeAsync(person, new ConversationId(conversation), until, context.RequestAborted)).ConfigureAwait(false);
    }

    private static async Task<Results<JsonAnswer<RepliesBody>, Refusal>> RepliesAsync(HttpContext context)
    {
        if (await Identity.PersonAsync(context).ConfigureAwait(false) is not { } person)
        {
            return Identity.Unauthenticated();
        }

        IQueryCollection query = context.Request.Query;
        bool mine = query.TryGetValue("mine", out StringValues mines);
        bool shared = query.TryGetValue("topic", out StringValues topics);
        if (mine == shared || (mine && (mines.Count != 1 || mines[0] is not ("" or "true"))) || (shared && (topics.Count != 1 || topics[0] is null)))
        {
            return RequestBodies.Malformed("topic");
        }

        string? topic = shared ? topics[0] : null;
        return await CallAsync<JsonAnswer<RepliesBody>>(context, async conversations =>
        {
            Result<IReadOnlyList<SavedReplyRecord>> replies = await conversations.RepliesAsync(person, topic, context.RequestAborted).ConfigureAwait(false);
            return replies.Failure is { } refused
                ? Refused(context, refused)
                : new JsonAnswer<RepliesBody>(new RepliesBody([.. replies.Value!.Select(Of)]), ConversationsJson.Default.RepliesBody);
        }).ConfigureAwait(false);
    }

    private static async Task<Results<CreatedAnswer<ReplyBody>, Refusal>> AddReplyAsync(HttpContext context)
    {
        if (await Identity.PersonAsync(context).ConfigureAwait(false) is not { } person)
        {
            return Identity.Unauthenticated();
        }

        (ReplyRequest? body, Refusal? malformed) = await RequestBodies.JsonAsync(context.Request, ConversationsJson.Default.ReplyRequest, read => read.Name is null ? "name" : read.Text is null ? "text" : null).ConfigureAwait(false);
        if (malformed is not null)
        {
            return malformed;
        }

        return await CallAsync<CreatedAnswer<ReplyBody>>(context, async conversations =>
        {
            Result<SavedReplyRecord> added = await conversations.AddReplyAsync(person, body!.Name!, body.Text!, body.Topic, context.RequestAborted).ConfigureAwait(false);
            return added.Failure is { } refused ? Refused(context, refused) : new CreatedAnswer<ReplyBody>(Of(added.Value!), ConversationsJson.Default.ReplyBody);
        }).ConfigureAwait(false);
    }

    private static async Task<Results<NoContent, Refusal>> ChangeReplyAsync(HttpContext context, string id)
    {
        if (await Identity.PersonAsync(context).ConfigureAwait(false) is not { } person)
        {
            return Identity.Unauthenticated();
        }

        (ReplyChange? body, Refusal? malformed) = await RequestBodies.JsonAsync(context.Request, ConversationsJson.Default.ReplyChange, read => read.Name is null ? "name" : read.Text is null ? "text" : null).ConfigureAwait(false);
        if (malformed is not null)
        {
            return malformed;
        }

        if (!Guid.TryParseExact(id, "D", out Guid reply))
        {
            return Refusal.Of(StatusCodes.Status404NotFound, ChatCodes.ReplyNotFound);
        }

        return await DoneAsync(context, conversations => conversations.ChangeReplyAsync(person, new SavedReplyId(reply), body!.Name!, body.Text!, context.RequestAborted)).ConfigureAwait(false);
    }

    private static async Task<Results<NoContent, Refusal>> RemoveReplyAsync(HttpContext context, string id)
    {
        if (await Identity.PersonAsync(context).ConfigureAwait(false) is not { } person)
        {
            return Identity.Unauthenticated();
        }

        if (!Guid.TryParseExact(id, "D", out Guid reply))
        {
            return Refusal.Of(StatusCodes.Status404NotFound, ChatCodes.ReplyNotFound);
        }

        return await DoneAsync(context, conversations => conversations.RemoveReplyAsync(person, new SavedReplyId(reply), context.RequestAborted)).ConfigureAwait(false);
    }

    /// <summary>Reads <paramref name="text"/> as an instant a body gives, in UTC whatever the host's zone; false where it is none or not one.</summary>
    internal static bool TryInstant(string? text, out DateTimeOffset instant)
    {
        return DateTimeOffset.TryParseExact(text, Instants, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out instant);
    }

    private static ReplyBody Of(SavedReplyRecord record)
    {
        return new ReplyBody(record.Id.Value.ToString("D"), record.Name, record.Text, record.Topic);
    }

    private static async Task<Results<JsonAnswer<QueueBody>, Refusal>> QueueAsync(HttpContext context)
    {
        if (await Identity.PersonAsync(context).ConfigureAwait(false) is not { } person)
        {
            return Identity.Unauthenticated();
        }

        if (!context.Request.Query.TryGetValue("topic", out StringValues topics) || topics.Count != 1 || topics[0] is not { } topic)
        {
            return RequestBodies.Malformed("topic");
        }

        return await CallAsync<JsonAnswer<QueueBody>>(context, async conversations =>
        {
            Result<ConversationPage> queue = await conversations.QueueAsync(person, topic, context.RequestAborted).ConfigureAwait(false);
            return queue.Failure is { } refused
                ? Refused(context, refused)
                : new JsonAnswer<QueueBody>(new QueueBody([.. queue.Value!.Conversations.Select(Of)]), ConversationsJson.Default.QueueBody);
        }).ConfigureAwait(false);
    }

    private static async Task<Results<NoContent, Refusal>> AvailabilityAsync(HttpContext context)
    {
        if (await Identity.PersonAsync(context).ConfigureAwait(false) is not { } person)
        {
            return Identity.Unauthenticated();
        }

        (AvailabilityRequest? body, Refusal? malformed) = await RequestBodies.JsonAsync(context.Request, ConversationsJson.Default.AvailabilityRequest, read => read.Available is null ? "available" : null).ConfigureAwait(false);
        if (malformed is not null)
        {
            return malformed;
        }

        return await DoneAsync(context, conversations => conversations.SetAvailableAsync(person, body!.Available!.Value, context.RequestAborted)).ConfigureAwait(false);
    }

    private static async Task<Results<NoContent, Refusal>> TransferAsync(HttpContext context, string id)
    {
        if (await Identity.PersonAsync(context).ConfigureAwait(false) is not { } person)
        {
            return Identity.Unauthenticated();
        }

        (TransferRequest? body, Refusal? malformed) = await RequestBodies.JsonAsync(context.Request, ConversationsJson.Default.TransferRequest, read => (read.Person is null) == (read.Topic is null) ? "person" : null).ConfigureAwait(false);
        if (malformed is not null)
        {
            return malformed;
        }

        if (!Guid.TryParseExact(id, "D", out Guid conversation))
        {
            return NotFound();
        }

        return await DoneAsync(context, conversations => conversations.TransferAsync(person, new ConversationId(conversation), body!.Person, body.Topic, context.RequestAborted)).ConfigureAwait(false);
    }

    private static async Task<Results<NoContent, Refusal>> CloseAsync(HttpContext context, string id)
    {
        if (await Identity.PersonAsync(context).ConfigureAwait(false) is not { } person)
        {
            return Identity.Unauthenticated();
        }

        (_, Refusal? malformed) = await RequestBodies.JsonAsync(context.Request, EndpointJson.Default.EmptyBody, _ => null).ConfigureAwait(false);
        if (malformed is not null)
        {
            return malformed;
        }

        if (!Guid.TryParseExact(id, "D", out Guid conversation))
        {
            return NotFound();
        }

        return await DoneAsync(context, conversations => conversations.CloseAsync(person, new ConversationId(conversation), context.RequestAborted)).ConfigureAwait(false);
    }

    private static async Task<Results<NoContent, Refusal>> BlockAsync(HttpContext context, string id)
    {
        if (await Identity.PersonAsync(context).ConfigureAwait(false) is not { } person)
        {
            return Identity.Unauthenticated();
        }

        (BlockRequest? body, Refusal? malformed) = await RequestBodies.JsonAsync(context.Request, ConversationsJson.Default.BlockRequest, _ => null).ConfigureAwait(false);
        if (malformed is not null)
        {
            return malformed;
        }

        DateTimeOffset? until = null;
        if (body!.Until is { } text)
        {
            if (!DateTimeOffset.TryParseExact(text, Instants, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTimeOffset read))
            {
                return RequestBodies.Malformed("until");
            }

            until = read;
        }

        if (!Guid.TryParseExact(id, "D", out Guid conversation))
        {
            return NotFound();
        }

        return await DoneAsync(context, conversations => conversations.BlockAsync(person, new ConversationId(conversation), until, context.RequestAborted)).ConfigureAwait(false);
    }

    private static async Task<Results<NoContent, Refusal>> UnblockAsync(HttpContext context, string id)
    {
        if (await Identity.PersonAsync(context).ConfigureAwait(false) is not { } person)
        {
            return Identity.Unauthenticated();
        }

        if (!Guid.TryParseExact(id, "D", out Guid conversation))
        {
            return NotFound();
        }

        return await DoneAsync(context, conversations => conversations.UnblockAsync(person, new ConversationId(conversation), context.RequestAborted)).ConfigureAwait(false);
    }

    private static async Task<Results<JsonAnswer<ConversationsBody>, Refusal>> ListAsync(HttpContext context)
    {
        if (await Identity.PersonAsync(context).ConfigureAwait(false) is not { } person)
        {
            return Identity.Unauthenticated();
        }

        IQueryCollection query = context.Request.Query;
        if (!TrySize(query, out int? size))
        {
            return RequestBodies.Malformed("size");
        }

        string? cursor = null;
        if (query.TryGetValue("cursor", out StringValues cursors))
        {
            if (cursors.Count != 1)
            {
                return RequestBodies.Malformed("cursor");
            }

            cursor = cursors[0];
        }

        return await CallAsync<JsonAnswer<ConversationsBody>>(context, async conversations =>
        {
            Result<ConversationPage> page = await conversations.ListAsync(person, cursor, size, context.RequestAborted).ConfigureAwait(false);
            return page.Failure is { } refused
                ? Refused(context, refused)
                : new JsonAnswer<ConversationsBody>(new ConversationsBody([.. page.Value!.Conversations.Select(Of)], page.Value.Next), ConversationsJson.Default.ConversationsBody);
        }).ConfigureAwait(false);
    }

    private static async Task<Results<CreatedAnswer<ConversationBody>, Refusal>> StartAsync(HttpContext context)
    {
        if (await Identity.PersonAsync(context).ConfigureAwait(false) is not { } person)
        {
            return Identity.Unauthenticated();
        }

        (ParticipantsRequest? body, Refusal? malformed) = await RequestBodies.JsonAsync(context.Request, ConversationsJson.Default.ParticipantsRequest, read => read.Participants is null ? "participants" : null).ConfigureAwait(false);
        if (malformed is not null)
        {
            return malformed;
        }

        if (body!.Participants!.Any(participant => participant is null))
        {
            return RequestBodies.Malformed("participants");
        }

        return await CallAsync<CreatedAnswer<ConversationBody>>(context, async conversations =>
        {
            Result<ConversationRecord> started = await conversations.StartDirectAsync(person, body.Participants!, context.RequestAborted).ConfigureAwait(false);
            return started.Failure is { } refused ? Refused(context, refused) : new CreatedAnswer<ConversationBody>(Of(started.Value!), ConversationsJson.Default.ConversationBody);
        }).ConfigureAwait(false);
    }

    private static async Task<Results<JsonAnswer<ConversationBody>, Refusal>> OpenGroupAsync(HttpContext context, string kind, string reference)
    {
        if (await Identity.PersonAsync(context).ConfigureAwait(false) is not { } person)
        {
            return Identity.Unauthenticated();
        }

        if (ConversationSpellings.ReadScopeKind(kind) is not { } scopeKind)
        {
            return Refusal.Of(StatusCodes.Status400BadRequest, ChatCodes.ConversationInvalid, ("field", "kind"));
        }

        if (!StaffChats.IsReference(reference))
        {
            return RequestBodies.Malformed("reference");
        }

        return await CallAsync<JsonAnswer<ConversationBody>>(context, async conversations =>
        {
            Result<ConversationRecord> opened = await conversations.OpenGroupAsync(person, new ConversationScope { Kind = scopeKind, Reference = reference }, context.RequestAborted).ConfigureAwait(false);
            return opened.Failure is { } refused ? Refused(context, refused) : new JsonAnswer<ConversationBody>(Of(opened.Value!), ConversationsJson.Default.ConversationBody);
        }).ConfigureAwait(false);
    }

    private static async Task<Results<JsonAnswer<ConversationBody>, Refusal>> ReadAsync(HttpContext context, string id)
    {
        if (await Identity.PersonAsync(context).ConfigureAwait(false) is not { } person)
        {
            return Identity.Unauthenticated();
        }

        if (!Guid.TryParseExact(id, "D", out Guid conversation))
        {
            return NotFound();
        }

        return await CallAsync<JsonAnswer<ConversationBody>>(context, async conversations =>
        {
            Result<ConversationRecord> read = await conversations.ReadAsync(person, new ConversationId(conversation), context.RequestAborted).ConfigureAwait(false);
            return read.Failure is { } refused ? Refused(context, refused) : new JsonAnswer<ConversationBody>(Of(read.Value!), ConversationsJson.Default.ConversationBody);
        }).ConfigureAwait(false);
    }

    private static async Task<Results<JsonAnswer<MessagesBody>, Refusal>> MessagesAsync(HttpContext context, string id)
    {
        if (await Identity.PersonAsync(context).ConfigureAwait(false) is not { } person)
        {
            return Identity.Unauthenticated();
        }

        if (!Guid.TryParseExact(id, "D", out Guid conversation))
        {
            return NotFound();
        }

        IQueryCollection query = context.Request.Query;
        if (!TrySize(query, out int? size))
        {
            return RequestBodies.Malformed("size");
        }

        MessageId? before = null;
        if (query.TryGetValue("before", out StringValues befores))
        {
            if (befores.Count != 1 || !Guid.TryParseExact(befores[0], "D", out Guid named))
            {
                return RequestBodies.Malformed("before");
            }

            before = new MessageId(named);
        }

        MessageId? after = null;
        if (query.TryGetValue("after", out StringValues afters))
        {
            if (afters.Count != 1 || before is not null || !Guid.TryParseExact(afters[0], "D", out Guid named))
            {
                return RequestBodies.Malformed("after");
            }

            after = new MessageId(named);
        }

        return await CallAsync<JsonAnswer<MessagesBody>>(context, async conversations =>
        {
            Result<MessagePage> page = await conversations.MessagesAsync(person, new ConversationId(conversation), before, after, size, context.RequestAborted).ConfigureAwait(false);
            return page.Failure is { } refused
                ? Refused(context, refused)
                : new JsonAnswer<MessagesBody>(new MessagesBody([.. page.Value!.Messages.Select(Of)]), ConversationsJson.Default.MessagesBody);
        }).ConfigureAwait(false);
    }

    private static async Task<Results<CreatedAnswer<MessageBody>, Refusal>> WriteAsync(HttpContext context, string id)
    {
        if (await Identity.PersonAsync(context).ConfigureAwait(false) is not { } person)
        {
            return Identity.Unauthenticated();
        }

        (MessageRequest? body, Refusal? malformed) = await RequestBodies.JsonAsync(context.Request, ConversationsJson.Default.MessageRequest, read => read.Text is null ? "text" : null).ConfigureAwait(false);
        if (malformed is not null)
        {
            return malformed;
        }

        if (!Guid.TryParseExact(id, "D", out Guid conversation))
        {
            return NotFound();
        }

        if (body!.Mentions is { } mentions && mentions.Any(mention => mention is null))
        {
            return RequestBodies.Malformed("mentions");
        }

        MessageId? replyTo = null;
        if (body.ReplyTo is { } reply)
        {
            if (!Guid.TryParseExact(reply, "D", out Guid replied))
            {
                return RequestBodies.Malformed("replyTo");
            }

            replyTo = new MessageId(replied);
        }

        MessageType type = MessageType.Text;
        if (body.Type is { } spelled)
        {
            if (ConversationSpellings.ReadMessageType(spelled) is not { } read)
            {
                return Refusal.Of(StatusCodes.Status400BadRequest, ChatCodes.MessageInvalid, ("field", "type"));
            }

            type = read;
        }

        return await CallAsync<CreatedAnswer<MessageBody>>(context, async conversations =>
        {
            Result<MessageRecord> written = await conversations.WriteAsync(person, new ConversationId(conversation), body.Text!, type, replyTo, body.Mentions, context.RequestAborted).ConfigureAwait(false);
            return written.Failure is { } refused ? Refused(context, refused) : new CreatedAnswer<MessageBody>(Of(written.Value!), ConversationsJson.Default.MessageBody);
        }).ConfigureAwait(false);
    }

    private static async Task<Results<NoContent, Refusal>> EditAsync(HttpContext context, string id, string messageId)
    {
        if (await Identity.PersonAsync(context).ConfigureAwait(false) is not { } person)
        {
            return Identity.Unauthenticated();
        }

        (EditRequest? body, Refusal? malformed) = await RequestBodies.JsonAsync(context.Request, ConversationsJson.Default.EditRequest, read => read.Text is null ? "text" : null).ConfigureAwait(false);
        if (malformed is not null)
        {
            return malformed;
        }

        if (!Guid.TryParseExact(id, "D", out Guid conversation))
        {
            return NotFound();
        }

        if (!Guid.TryParseExact(messageId, "D", out Guid message))
        {
            return Refusal.Of(StatusCodes.Status404NotFound, ChatCodes.MessageNotFound);
        }

        return await DoneAsync(context, conversations => conversations.EditAsync(person, new ConversationId(conversation), new MessageId(message), body!.Text!, context.RequestAborted)).ConfigureAwait(false);
    }

    private static async Task<Results<NoContent, Refusal>> DeleteAsync(HttpContext context, string id, string messageId)
    {
        if (await Identity.PersonAsync(context).ConfigureAwait(false) is not { } person)
        {
            return Identity.Unauthenticated();
        }

        if (!Guid.TryParseExact(id, "D", out Guid conversation))
        {
            return NotFound();
        }

        if (!Guid.TryParseExact(messageId, "D", out Guid message))
        {
            return Refusal.Of(StatusCodes.Status404NotFound, ChatCodes.MessageNotFound);
        }

        return await DoneAsync(context, conversations => conversations.DeleteAsync(person, new ConversationId(conversation), new MessageId(message), context.RequestAborted)).ConfigureAwait(false);
    }

    private static async Task<Results<NoContent, Refusal>> MarkReadAsync(HttpContext context, string id)
    {
        if (await Identity.PersonAsync(context).ConfigureAwait(false) is not { } person)
        {
            return Identity.Unauthenticated();
        }

        (ReadRequest? body, Refusal? malformed) = await RequestBodies.JsonAsync(context.Request, ConversationsJson.Default.ReadRequest, read => read.MessageId is null ? "messageId" : null).ConfigureAwait(false);
        if (malformed is not null)
        {
            return malformed;
        }

        if (!Guid.TryParseExact(body!.MessageId, "D", out Guid message))
        {
            return RequestBodies.Malformed("messageId");
        }

        if (!Guid.TryParseExact(id, "D", out Guid conversation))
        {
            return NotFound();
        }

        return await DoneAsync(context, conversations => conversations.MarkReadAsync(person, new ConversationId(conversation), new MessageId(message), context.RequestAborted)).ConfigureAwait(false);
    }

    private static async Task<Results<NoContent, Refusal>> MuteAsync(HttpContext context, string id)
    {
        if (await Identity.PersonAsync(context).ConfigureAwait(false) is not { } person)
        {
            return Identity.Unauthenticated();
        }

        (MuteRequest? body, Refusal? malformed) = await RequestBodies.JsonAsync(context.Request, ConversationsJson.Default.MuteRequest, read => read.Muted is null ? "muted" : null).ConfigureAwait(false);
        if (malformed is not null)
        {
            return malformed;
        }

        if (!Guid.TryParseExact(id, "D", out Guid conversation))
        {
            return NotFound();
        }

        return await DoneAsync(context, conversations => conversations.MuteAsync(person, new ConversationId(conversation), body!.Muted!.Value, context.RequestAborted)).ConfigureAwait(false);
    }

    private static async Task<Results<NoContent, Refusal>> AddAsync(HttpContext context, string id)
    {
        if (await Identity.PersonAsync(context).ConfigureAwait(false) is not { } person)
        {
            return Identity.Unauthenticated();
        }

        (ParticipantsRequest? body, Refusal? malformed) = await RequestBodies.JsonAsync(context.Request, ConversationsJson.Default.ParticipantsRequest, read => read.Participants is null ? "participants" : null).ConfigureAwait(false);
        if (malformed is not null)
        {
            return malformed;
        }

        if (body!.Participants!.Any(participant => participant is null))
        {
            return RequestBodies.Malformed("participants");
        }

        if (!Guid.TryParseExact(id, "D", out Guid conversation))
        {
            return NotFound();
        }

        return await DoneAsync(context, conversations => conversations.AddParticipantsAsync(person, new ConversationId(conversation), body.Participants!, context.RequestAborted)).ConfigureAwait(false);
    }

    private static async Task<Results<NoContent, Refusal>> LeaveAsync(HttpContext context, string id)
    {
        if (await Identity.PersonAsync(context).ConfigureAwait(false) is not { } person)
        {
            return Identity.Unauthenticated();
        }

        (_, Refusal? malformed) = await RequestBodies.JsonAsync(context.Request, EndpointJson.Default.EmptyBody, _ => null).ConfigureAwait(false);
        if (malformed is not null)
        {
            return malformed;
        }

        if (!Guid.TryParseExact(id, "D", out Guid conversation))
        {
            return NotFound();
        }

        return await DoneAsync(context, conversations => conversations.LeaveAsync(person, new ConversationId(conversation), context.RequestAborted)).ConfigureAwait(false);
    }

    /// <summary>Reads the query's <c>size</c>, 1 to 100, none where it is not given; false where it does not read.</summary>
    internal static bool TrySize(IQueryCollection query, out int? size)
    {
        size = null;
        if (!query.TryGetValue("size", out StringValues sizes))
        {
            return true;
        }

        if (sizes.Count != 1 || !int.TryParse(sizes[0], NumberStyles.None, CultureInfo.InvariantCulture, out int read) || read is < 1 or > MostSize)
        {
            return false;
        }

        size = read;
        return true;
    }

    /// <summary>Calls the operation, a person's locks not had in time, or a value that cannot open yet, answering 503.</summary>
    private static async Task<Results<T, Refusal>> CallAsync<T>(HttpContext context, Func<IConversations, Task<Results<T, Refusal>>> call)
        where T : IResult
    {
        try
        {
            return await call(context.RequestServices.GetRequiredService<IConversations>()).ConfigureAwait(false);
        }
        catch (Exception failure) when (DispatchPasses.IsLockTimeout(failure))
        {
            return Unavailable(context);
        }
    }

    private static async Task<Results<NoContent, Refusal>> DoneAsync(HttpContext context, Func<IConversations, ValueTask<Result>> call)
    {
        return await CallAsync<NoContent>(context, async conversations =>
        {
            Result result = await call(conversations).ConfigureAwait(false);
            return result.Failure is { } refused ? Refused(context, refused) : TypedResults.NoContent();
        }).ConfigureAwait(false);
    }

    internal static Refusal Unavailable(HttpContext context)
    {
        return Refusal.Unavailable(context.RequestServices.GetRequiredService<IOptionsMonitor<MessagingOptions>>().CurrentValue.Dispatch.Interval);
    }

    /// <summary>A failure an operation answered, with the status <c>08</c> section 1 gives its code; a key unavailable answers 503.</summary>
    internal static Refusal Refused(HttpContext context, Failure failure)
    {
        int status = failure.Code switch
        {
            ChatCodes.ConversationNotFound or ChatCodes.MessageNotFound or ChatCodes.ReplyNotFound => StatusCodes.Status404NotFound,
            ChatCodes.ConversationInvalid or ChatCodes.MessageInvalid or ChatCodes.ReplyInvalid or Codes.RequestMalformed => StatusCodes.Status400BadRequest,
            ChatCodes.ConversationFull or ChatCodes.MessageDeleted or ChatCodes.ConversationClosed or ChatCodes.ReplyFull or ChatCodes.ReplyNameExists or SupportCodes.Offline or SupportCodes.NoticeRequired or SupportCodes.AlreadyOpen => StatusCodes.Status409Conflict,
            ChatCodes.MessageNotAuthor or SupportCodes.Blocked => StatusCodes.Status403Forbidden,
            SupportCodes.Limited => StatusCodes.Status429TooManyRequests,
            _ => StatusCodes.Status503ServiceUnavailable,
        };
        return status == StatusCodes.Status503ServiceUnavailable ? Unavailable(context) : Refusal.Of(status, failure);
    }

    internal static Refusal NotFound()
    {
        return Refusal.Of(StatusCodes.Status404NotFound, ChatCodes.ConversationNotFound);
    }

    internal static ConversationBody Of(ConversationRecord record)
    {
        return new ConversationBody(
            record.Conversation.Value.ToString("D"),
            ConversationSpellings.Of(record.Type),
            record.Scope is { } scope ? new ScopeBody(ConversationSpellings.Of(scope.Kind), scope.Reference) : null,
            [.. record.Participants.Select(participant => new ParticipantBody(participant.Participant.Value.ToString("D"), participant.Person, participant.Online))],
            record.Unread,
            record.Muted,
            Text(record.LastAt),
            record.Topic,
            record.Status is { } status ? ConversationSpellings.Of(status) : null,
            record.Place,
            record.Offline,
            record.Tags,
            record.SnoozedUntil is { } snoozed ? Text(snoozed) : null);
    }

    /// <summary>The conversation as the public side answers it, without its tags and waking instant.</summary>
    internal static PublicConversationBody OfPublic(ConversationRecord record)
    {
        ConversationBody staff = Of(record);
        return new PublicConversationBody(staff.Id, staff.Type, staff.Scope, staff.Participants, staff.Unread, staff.Muted, staff.LastAt, staff.Topic, staff.Status, staff.Place, staff.Offline);
    }

    internal static MessageBody Of(MessageRecord record)
    {
        return new MessageBody(
            record.Message.Value.ToString("D"),
            ConversationSpellings.Of(record.AuthorKind),
            record.Author,
            ConversationSpellings.Of(record.Type),
            record.Text,
            record.ReplyTo?.Value.ToString("D"),
            record.Mentions,
            Text(record.SentAt),
            record.EditedAt is { } edited ? Text(edited) : null,
            record.DeletedAt is { } deleted ? Text(deleted) : null,
            record.Erased);
    }

    private static string Text(DateTimeOffset instant)
    {
        return instant.UtcDateTime.ToString(Instant, CultureInfo.InvariantCulture);
    }
}