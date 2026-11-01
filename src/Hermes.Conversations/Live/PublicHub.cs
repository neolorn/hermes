using System;
using System.Linq;
using System.Threading.Tasks;

using Hermes.Conversations.Chats;
using Hermes.Conversations.Endpoints;
using Hermes.Conversations.Support;
using Hermes.Sending;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace Hermes.Conversations.Live;

/// <summary>
/// The public hub (<c>09</c> MSG-CONV-008; <c>05</c> MSG-API-010), mapped at <c>/live</c> in the
/// public conversations group: its caller is the signed-in customer the identity seam answers,
/// or the visitor the cookie <c>messaging_visitor</c> names, and a connection with neither is
/// refused. It pushes <c>message</c>, <c>messageChanged</c>, <c>typing</c>, <c>place</c>,
/// <c>assigned</c> and <c>closed</c> of the caller's conversations, never a <c>note</c>, and
/// takes <see cref="Typing"/> alone from the client.
/// </summary>
internal sealed class PublicHub(IServiceProvider services, PublicConnections connections, KeyedHashes hashes) : Hub
{
    /// <summary>The visitor's place in the queue: <c>position</c>.</summary>
    internal const string PlaceMethod = "place";

    /// <summary>The conversation was assigned: <c>conversationId</c>.</summary>
    internal const string AssignedMethod = "assigned";

    /// <summary>The conversation was closed: <c>conversationId</c>.</summary>
    internal const string ClosedMethod = "closed";

    /// <summary>The methods the hub sends the client, each with its arguments, as <c>05</c> gives them.</summary>
    internal static (string Name, string[] Arguments)[] ServerMethods { get; } =
    [
        (StaffHub.MessageMethod, ["conversationId", "message"]),
        (StaffHub.MessageChangedMethod, ["conversationId", "message"]),
        (StaffHub.TypingMethod, ["conversationId", "participantId"]),
        (PlaceMethod, ["position"]),
        (AssignedMethod, ["conversationId"]),
        (ClosedMethod, ["conversationId"]),
    ];

    public override async Task OnConnectedAsync()
    {
        if (await CallerAsync().ConfigureAwait(false) is not { } caller)
        {
            Context.Abort();
            return;
        }

        string[] keys = caller.Customer is { } customer
            ? [PublicConnections.CustomerKey(customer)]
            : [.. caller.Hashes(hashes).Select(PublicConnections.VisitorKey)];
        connections.Add(Context.ConnectionId, keys);
        Context.Items[nameof(SupportCaller)] = caller;
        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        connections.Remove(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
    }

    /// <summary>Tells the conversation's assignee that the caller is typing; it is never stored.</summary>
    /// <param name="conversationId">The conversation's identifier, lower-case and hyphenated.</param>
    /// <returns>A task that completes once the processes are told.</returns>
    public async Task Typing(string conversationId)
    {
        if (Context.Items[nameof(SupportCaller)] is not SupportCaller caller || !Guid.TryParseExact(conversationId, "D", out Guid conversation))
        {
            return;
        }

        await services.GetRequiredService<StaffChats>().CallerTypingAsync(caller, conversation, Context.ConnectionAborted).ConfigureAwait(false);
    }

    /// <summary>The caller of the connection's request, as the public group reads it, drawing no visitor.</summary>
    private async Task<SupportCaller?> CallerAsync()
    {
        if (Context.GetHttpContext() is not { } http)
        {
            return null;
        }

        if (services.GetService<IHttpContextAccessor>() is { } accessor)
        {
            accessor.HttpContext = http;
        }

        return await PublicEndpoints.HubCallerAsync(http).ConfigureAwait(false);
    }
}