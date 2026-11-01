using System;
using System.Threading.Tasks;

using Hermes.Conversations.Chats;
using Hermes.Core;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace Hermes.Conversations.Live;

/// <summary>
/// The staff hub (<c>09</c> MSG-CONV-008; <c>05</c> MSG-API-009), mapped at <c>/live</c> in the
/// staff conversations group: the person comes from the identity seam when a client
/// connects, and a connection without one is refused. It pushes <c>message</c>,
/// <c>messageChanged</c>, <c>read</c>, <c>typing</c>, <c>presence</c> and <c>conversation</c>,
/// and takes <see cref="Typing"/> alone from the client: writes stay HTTP.
/// </summary>
internal sealed class StaffHub(IServiceProvider services, LiveConnections connections, LivePresence presence) : Hub
{
    /// <summary>A new message: <c>conversationId</c>, <c>message</c>.</summary>
    internal const string MessageMethod = "message";

    /// <summary>An edit or a delete: <c>conversationId</c>, <c>message</c>.</summary>
    internal const string MessageChangedMethod = "messageChanged";

    /// <summary>A read position moved: <c>conversationId</c>, <c>participantId</c>, <c>messageId</c>.</summary>
    internal const string ReadMethod = "read";

    /// <summary>A participant typing: <c>conversationId</c>, <c>participantId</c>.</summary>
    internal const string TypingMethod = "typing";

    /// <summary>A participant's presence: <c>participantId</c>, <c>online</c>.</summary>
    internal const string PresenceMethod = "presence";

    /// <summary>A conversation added to the person's list: <c>conversation</c>.</summary>
    internal const string ConversationMethod = "conversation";

    /// <summary>The methods the hub sends the client, each with its arguments, as <c>05</c> gives them.</summary>
    internal static (string Name, string[] Arguments)[] ServerMethods { get; } =
    [
        (MessageMethod, ["conversationId", "message"]),
        (MessageChangedMethod, ["conversationId", "message"]),
        (ReadMethod, ["conversationId", "participantId", "messageId"]),
        (TypingMethod, ["conversationId", "participantId"]),
        (PresenceMethod, ["participantId", "online"]),
        (ConversationMethod, ["conversation"]),
    ];

    public override async Task OnConnectedAsync()
    {
        if (await PersonAsync().ConfigureAwait(false) is not { } person)
        {
            Context.Abort();
            return;
        }

        await presence.ConnectAsync(Context.ConnectionId, person, Context.ConnectionAborted).ConfigureAwait(false);
        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await presence.DisconnectAsync(Context.ConnectionId, default).ConfigureAwait(false);
        await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
    }

    /// <summary>Tells the conversation's other connected readers that the person is typing; it is never stored.</summary>
    /// <param name="conversationId">The conversation's identifier, lower-case and hyphenated.</param>
    /// <returns>A task that completes once the processes are told.</returns>
    public async Task Typing(string conversationId)
    {
        if (connections.PersonOf(Context.ConnectionId) is not { } person || !Guid.TryParseExact(conversationId, "D", out Guid conversation))
        {
            return;
        }

        await services.GetRequiredService<StaffChats>().TypingAsync(person, conversation, Context.ConnectionAborted).ConfigureAwait(false);
    }

    /// <summary>The person the identity seam answers for the connection's request, or none.</summary>
    private async Task<string?> PersonAsync()
    {
        if (Context.GetHttpContext() is { } http && services.GetService<IHttpContextAccessor>() is { } accessor)
        {
            accessor.HttpContext = http;
        }

        string? person = await services.GetRequiredService<IRecipientIdentity>().CurrentAsync(Context.ConnectionAborted).ConfigureAwait(false);
        return StaffChats.IsReference(person) ? person : null;
    }
}