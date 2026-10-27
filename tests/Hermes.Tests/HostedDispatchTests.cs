using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Xunit;

namespace Hermes.Tests;

/// <summary>The hosted dispatcher: its clock a <see cref="ManualClock"/> that no test moves unless it says so, so its interval never comes and only its notifications start passes.</summary>
[Trait("Category", "integration")]
public sealed class HostedDispatchTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task MSG_SEND_004_AC2_ASendInAHostTransactionIsDispatchedOnItsCommitWithoutWaitingForTheIntervalAndNotSeenBefore()
    {
        RecordingSmsProvider sms = new();
        await using EngineHost host = await StartAsync(sms);
        await ListeningAsync(host);
        await using NpgsqlConnection connection = new(host.Connection);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using (scope)
        {
            Assert.True((await scope.ServiceProvider.GetRequiredService<IMessages>().SendAsync(SendTests.Code(channels: [Channel.Sms]), transaction, TestContext.Current.CancellationToken)).IsSuccess);
        }

        await NotifyAsync(host);
        await Task.Delay(300, TestContext.Current.CancellationToken);
        int before = sms.Received.Count;
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        await UntilAsync(() => sms.Received.Count == 1);
        Assert.Equal(0, before);
    }

    [Fact]
    public async Task MSG_SEND_007_AC12_NotificationsWhileMaxPassesPassesRunStartOneMoreAsSoonAsOneEndsAndNeverAFifthBesideThem()
    {
        RecordingSmsProvider sms = new();
        int running = 0;
        int most = 0;
        using SemaphoreSlim release = new(0);
        sms.Before = async (_, _) =>
        {
            int now = Interlocked.Increment(ref running);
            InterlockedMax(ref most, now);
            await release.WaitAsync(TestContext.Current.CancellationToken);
            Interlocked.Decrement(ref running);
        };
        await using EngineHost host = await StartAsync(sms, configuration =>
        {
            configuration["Hermes:Dispatch:MaxPasses"] = "4";
            configuration["Hermes:Dispatch:BatchSize"] = "1";
        });
        await ListeningAsync(host);
        for (int index = 0; index < 6; index++)
        {
            await host.SendAsync(SendTests.Code(channels: [Channel.Sms], recipient: new Recipient.Person($"person-{index}")));
        }

        await UntilAsync(() => Volatile.Read(ref running) == 4);
        for (int notice = 0; notice < 20; notice++)
        {
            await NotifyAsync(host);
        }

        await Task.Delay(300, TestContext.Current.CancellationToken);
        int besideFour = Volatile.Read(ref most);
        release.Release();
        await UntilAsync(() => sms.Received.Count == 5);
        release.Release(5);
        await UntilAsync(() => sms.Received.Count == 6);
        release.Release(5);

        Assert.Equal(4, besideFour);
        Assert.Equal(4, Volatile.Read(ref most));
    }

    [Fact]
    public async Task MSG_SEND_007_AC13_WithFourPassesHeldByAHangingConsentSourceAnEssentialDeliveryRecordedAfterThemIsSentWithinIntervalAndOneTimeout()
    {
        RecordingSmsProvider sms = new();
        int hanging = 0;
        ConsentSourceFake consent = new()
        {
            Answer = async (_, cancellationToken) =>
            {
                Interlocked.Increment(ref hanging);
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return ConsentAnswer.Given;
            },
        };
        await using EngineHost host = await StartAsync(
            sms,
            configuration =>
            {
                configuration["Hermes:Dispatch:MaxPasses"] = "4";
                configuration["Hermes:Dispatch:BatchSize"] = "1";
                configuration["Hermes:Dispatch:Timeout"] = "00:00:10";
            },
            declare: declare => ConsentTests.Declare(declare),
            consent: consent);
        await ListeningAsync(host);
        for (int index = 0; index < 4; index++)
        {
            await host.SendAsync(ConsentTests.PromotionTo($"+20120000000{index}") with { Recipient = new Recipient.Person($"person-{index}") });
        }

        await UntilAsync(() => Volatile.Read(ref hanging) == 4);
        await host.SendAsync(SendTests.Code(channels: [Channel.Sms]));
        await Task.Delay(300, TestContext.Current.CancellationToken);
        int before = sms.Received.Count;
        host.Clock.Advance(TimeSpan.FromSeconds(10));

        await UntilAsync(() => sms.Received.Count == 1);
        Assert.Equal(0, before);
        Assert.Equal(SendTests.Phone, Assert.Single(sms.Received).ToNumber);
        Assert.Equal(4, consent.Queries.Count);
    }

    [Fact]
    public async Task MSG_OPS_004_AC1_WithHostedOffNoPassRunsUntilRunPassAsyncIsCalled()
    {
        RecordingSmsProvider sms = new();
        await using EngineHost host = await StartAsync(sms, configuration => configuration["Hermes:Dispatch:Hosted"] = "false");
        await host.SendAsync(SendTests.Code(channels: [Channel.Sms]));

        await Task.Delay(500, TestContext.Current.CancellationToken);
        int before = sms.Received.Count;
        await host.DispatchAsync();

        Assert.Equal((0, 1), (before, sms.Received.Count));
    }

    [Fact]
    public async Task Dispatcher_TheListeningConnection_IsOnListenConnectionWhereTheDeploymentGivesOne()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        string listen = new NpgsqlConnectionStringBuilder(connection) { ApplicationName = "hermes-listen" }.ConnectionString;
        RecordingSmsProvider sms = new();
        await using EngineHost host = await StartAsync(sms, connection: connection, secrets: Secrets.Held(connection).With("listenConnection", Encoding.UTF8.GetBytes(listen)));

        await UntilAsync(async () => await ListenersAsync(host, "hermes-listen") == 1);
        await host.SendAsync(SendTests.Code(channels: [Channel.Sms]));
        await UntilAsync(() => sms.Received.Count == 1);
    }

    [Fact]
    public async Task Dispatcher_ItsListeningConnectionDropped_ConnectsAgainAfterASecondAndRunsAPassOfEachChannel()
    {
        RecordingSmsProvider sms = new();
        await using EngineHost host = await StartAsync(sms);
        await ListeningAsync(host);
        await host.RunAsync(connection => new NpgsqlCommand("SELECT count(pg_terminate_backend(pid)) FROM pg_stat_activity WHERE query = 'LISTEN messaging_dispatch' AND datname = current_database()", connection));
        await UntilAsync(async () => await ListenersAsync(host, null) == 0);
        Result<SendReceipt> sent = await host.SendAsync(SendTests.Code(channels: [Channel.Sms]) with { NotBefore = EngineHost.Start.AddSeconds(1) });

        await UntilAsync(async () =>
        {
            host.Clock.Advance(TimeSpan.FromMilliseconds(250));
            return await ListenersAsync(host, null) == 1;
        });

        await UntilAsync(() => sms.Received.Count == 1);
        Assert.True(sent.IsSuccess);
    }

    private async Task<EngineHost> StartAsync(
        RecordingSmsProvider sms,
        Action<Dictionary<string, string?>>? configure = null,
        string? connection = null,
        SecretSourceFake? secrets = null,
        Action<DeclarationBuilder>? declare = null,
        IConsentSource? consent = null)
    {
        return await EngineHost.StartAsync(
            connection ?? await EngineHost.DatabaseAsync(postgres),
            declare: declare,
            configure: configuration =>
            {
                EngineHost.Dispatching(configuration);
                configuration["Hermes:Dispatch:Hosted"] = "true";
                configure?.Invoke(configuration);
            },
            services: services =>
            {
                services.AddSingleton<IEmailProvider>(new RecordingEmailProvider());
                services.AddSingleton<ISmsProvider>(sms);
                if (consent is not null)
                {
                    services.AddSingleton(consent);
                }
            },
            secrets: secrets);
    }

    private static Task ListeningAsync(EngineHost host)
    {
        return UntilAsync(async () => await ListenersAsync(host, null) == 1);
    }

    private static async Task<long> ListenersAsync(EngineHost host, string? application)
    {
        const string Listeners = """
            SELECT count(*) FROM pg_stat_activity
            WHERE query = 'LISTEN messaging_dispatch' AND datname = current_database() AND (@application::text IS NULL OR application_name = @application::text)
            """;
        await using NpgsqlConnection connection = new(host.Connection);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new(Listeners, connection);
        command.Parameters.AddWithValue("application", (object?)application ?? DBNull.Value);
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private static Task<int> NotifyAsync(EngineHost host)
    {
        return host.RunAsync(connection => new NpgsqlCommand("SELECT pg_notify('messaging_dispatch', '')", connection));
    }

    private static Task UntilAsync(Func<bool> condition)
    {
        return UntilAsync(() => Task.FromResult(condition()));
    }

    private static async Task UntilAsync(Func<Task<bool>> condition)
    {
        for (int tries = 0; tries < 400; tries++)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(25, TestContext.Current.CancellationToken);
        }

        Assert.Fail("The condition did not come to hold.");
    }

    private static void InterlockedMax(ref int most, int value)
    {
        int seen;
        do
        {
            seen = Volatile.Read(ref most);
        }
        while (value > seen && Interlocked.CompareExchange(ref most, value, seen) != seen);
    }
}