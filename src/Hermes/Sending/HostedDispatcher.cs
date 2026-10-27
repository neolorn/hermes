using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Operations;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Hermes.Sending;

/// <summary>
/// The hosted dispatcher (<c>01</c> MSG-SEND-007; <c>06</c> MSG-OPS-004): where
/// <c>Hermes:Dispatch:Enabled</c> and <c>Hermes:Dispatch:Hosted</c> are both on, it runs passes
/// of each channel on each notification its listening connection receives, every
/// <c>Hermes:Dispatch:Interval</c>, and after each connection of the listening connection,
/// which it opens again after 1 second, doubling up to the interval, when it drops.
/// </summary>
internal sealed partial class HostedDispatcher(
    IOptionsMonitor<MessagingOptions> options,
    MessagingDatabase database,
    DispatchPasses passes,
    TimeProvider clock,
    ILogger<HostedDispatcher> logger) : IHostedService, IDisposable
{
    private const string Listen = "LISTEN messaging_dispatch";

    private readonly CancellationTokenSource stopping = new();

    private readonly List<ChannelPasses> channels = [];

    private Task listening = Task.CompletedTask;

    private Task ticking = Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        MessagingOptions current = options.CurrentValue;
        if (current.Dispatch is not { Enabled: true, Hosted: true })
        {
            return Task.CompletedTask;
        }

        foreach (Channel channel in DispatchPasses.Dispatched)
        {
            channels.Add(new ChannelPasses(channel, passes, () => options.CurrentValue.Dispatch.MaxPasses, Failed, stopping.Token));
        }

        listening = Task.Run(() => ListenAsync(stopping.Token), CancellationToken.None);
        ticking = Task.Run(() => TickAsync(stopping.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await stopping.CancelAsync().ConfigureAwait(false);
        await Task.WhenAll([listening, ticking, .. channels.Select(channel => channel.IdleAsync())]).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        channels.ForEach(channel => channel.Dispose());
        stopping.Dispose();
    }

    private void TriggerAll()
    {
        channels.ForEach(channel => channel.Trigger());
    }

    private void Failed(Channel channel, Exception failure)
    {
        PassFailed(logger, Spellings.Of(channel), failure.GetType().Name);
    }

    private async Task TickAsync(CancellationToken token)
    {
        try
        {
            using PeriodicTimer timer = new(options.CurrentValue.Dispatch.Interval, clock);
            while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
            {
                TriggerAll();
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
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
                    connection.Notification += (_, _) => TriggerAll();
                    await connection.OpenAsync(token).ConfigureAwait(false);
                    NpgsqlCommand listen = new(Listen, connection);
                    await using (listen.ConfigureAwait(false))
                    {
                        await listen.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    wait = TimeSpan.FromSeconds(1);
                    TriggerAll();
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
                try
                {
                    await Task.Delay(wait, clock, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                TimeSpan interval = options.CurrentValue.Dispatch.Interval;
                wait = wait * 2 < interval ? wait * 2 : interval;
            }
        }
    }

    [LoggerMessage(EventId = 41, EventName = "DispatchPassFailed", Level = LogLevel.Error, Message = "A pass of {Channel} failed with {Exception}; the channel's next pass runs at its next trigger.")]
    private static partial void PassFailed(ILogger logger, string channel, string exception);

    [LoggerMessage(EventId = 42, EventName = "DispatchListenLost", Level = LogLevel.Warning, Message = "The dispatcher's listening connection failed with {Exception}; it connects again in {Seconds} seconds.")]
    private static partial void ListenLost(ILogger logger, string exception, int seconds);
}