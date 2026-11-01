using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.SignalR.Client;

namespace Hermes.Conversations.Tests;

/// <summary>What the staff hub pushes to one connection, each push with its method and arguments, in order.</summary>
internal sealed class HubInbox
{
    private readonly ConcurrentQueue<(string Method, JsonElement[] Arguments)> received = new();

    private HubInbox()
    {
    }

    /// <summary>Every push so far.</summary>
    internal IReadOnlyList<(string Method, JsonElement[] Arguments)> All => [.. received];

    /// <summary>Records the pushes of each of the hub's methods to <paramref name="hub"/>.</summary>
    internal static HubInbox Of(HubConnection hub)
    {
        HubInbox inbox = new();
        inbox.On(hub, "message", 2);
        inbox.On(hub, "messageChanged", 2);
        inbox.On(hub, "read", 3);
        inbox.On(hub, "typing", 2);
        inbox.On(hub, "presence", 2);
        inbox.On(hub, "conversation", 1);
        return inbox;
    }

    /// <summary>The first push of <paramref name="method"/> matching <paramref name="match"/>, waiting up to ten seconds for it.</summary>
    internal async Task<JsonElement[]> NextAsync(string method, Func<JsonElement[], bool>? match = null)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
        while (true)
        {
            if (received.FirstOrDefault(each => each.Method == method && (match?.Invoke(each.Arguments) ?? true)) is { Method: not null } found)
            {
                return found.Arguments;
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(20), timeout.Token);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException($"No push of {method} arrived in ten seconds.");
            }
        }
    }

    private void On(HubConnection hub, string method, int count)
    {
        Type[] types = [.. Enumerable.Repeat(typeof(JsonElement), count)];
        hub.On(method, types, arguments =>
        {
            received.Enqueue((method, [.. arguments.Select(argument => (JsonElement)argument!)]));
            return Task.CompletedTask;
        });
    }
}