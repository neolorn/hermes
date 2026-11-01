using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using Hermes.Operations;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Npgsql;

namespace Hermes.Conversations.Live;

/// <summary>
/// Each process's listening for conversations' writes (<c>09</c> MSG-CONV-008; <c>06</c>
/// MSG-OPS-003): <c>LISTEN messaging_conversations</c> on a connection of the package's own,
/// opened from <c>listenConnection</c> where the deployment gives one and from the library's
/// connection otherwise, apart from the dispatcher's, so dispatch and conversations never wait
/// on each other; each notification pushed in its order. It also refreshes this process's
/// connections every 30 seconds and removes those past their lifetime.
/// </summary>
internal sealed partial class LiveListener(
    MessagingDatabase database,
    MessagingReady ready,
    LivePush push,
    LivePresence presence,
    TimeProvider clock,
    ILogger<LiveListener> logger) : IHostedService, IDisposable
{
    private const string Listen = "LISTEN messaging_conversations";

    private readonly CancellationTokenSource stopping = new();

    private readonly Channel<string> told = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });

    private Task running = Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // The engine's startup checks, run before any service's start, opened the database.
        ready.ThrowIfNotReady();
        running = Task.WhenAll(
            Task.Run(() => ListenAsync(stopping.Token), CancellationToken.None),
            Task.Run(() => PushAsync(stopping.Token), CancellationToken.None),
            Task.Run(() => RefreshAsync(stopping.Token), CancellationToken.None));
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await stopping.CancelAsync().ConfigureAwait(false);
        await running.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        stopping.Dispose();
    }

    private async Task ListenAsync(CancellationToken token)
    {
        TimeSpan wait = TimeSpan.FromSeconds(1);
        while (!token.IsCancellationRequested)
        {
            try
            {
                NpgsqlConnection connection = new(database.ListenConnection);
                await using (connection.ConfigureAwait(false))
                {
                    connection.Notification += (_, notification) => told.Writer.TryWrite(notification.Payload);
                    await connection.OpenAsync(token).ConfigureAwait(false);
                    NpgsqlCommand listen = new(Listen, connection);
                    await using (listen.ConfigureAwait(false))
                    {
                        await listen.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    wait = TimeSpan.FromSeconds(1);
                    while (true)
                    {
                        await connection.WaitAsync(token).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (Exception failure) when (failure is NpgsqlException or SocketException or IOException or TimeoutException or InvalidOperationException)
            {
                ListenLost(logger, failure.GetType().Name, (int)wait.TotalSeconds);
                if (!await DelayAsync(wait, token).ConfigureAwait(false))
                {
                    return;
                }

                wait = wait * 2 < LivePresence.Refresh ? wait * 2 : LivePresence.Refresh;
            }
        }
    }

    private async Task PushAsync(CancellationToken token)
    {
        try
        {
            await foreach (string payload in told.Reader.ReadAllAsync(token).ConfigureAwait(false))
            {
                try
                {
                    await push.PushAsync(payload, token).ConfigureAwait(false);
                }
                catch (Exception failure) when (failure is not OperationCanceledException || !token.IsCancellationRequested)
                {
                    PushFailed(logger, failure.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }

    private async Task RefreshAsync(CancellationToken token)
    {
        try
        {
            using PeriodicTimer timer = new(LivePresence.Refresh, clock);
            while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
            {
                try
                {
                    await presence.RefreshAsync(token).ConfigureAwait(false);
                }
                catch (Exception failure) when (failure is not OperationCanceledException || !token.IsCancellationRequested)
                {
                    RefreshFailed(logger, failure.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }

    private async Task<bool> DelayAsync(TimeSpan wait, CancellationToken token)
    {
        try
        {
            await Task.Delay(wait, clock, token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    [LoggerMessage(EventId = 71, EventName = "ConversationsListenLost", Level = LogLevel.Warning, Message = "The conversations' listening connection failed with {Exception}; it connects again in {Seconds} seconds.")]
    private static partial void ListenLost(ILogger logger, string exception, int seconds);

    [LoggerMessage(EventId = 72, EventName = "ConversationsPushFailed", Level = LogLevel.Warning, Message = "Pushing a conversation's notification failed with {Exception}.")]
    private static partial void PushFailed(ILogger logger, string exception);

    [LoggerMessage(EventId = 73, EventName = "ConversationsPresenceFailed", Level = LogLevel.Warning, Message = "Refreshing the staff hub's connections failed with {Exception}.")]
    private static partial void RefreshFailed(ILogger logger, string exception);
}