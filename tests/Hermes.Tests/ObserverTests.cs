using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Xunit;

namespace Hermes.Tests;

/// <summary>The <c>observer</c> job, run by each host's <see cref="ManualClock"/>, which no test moves unless it says so.</summary>
[Trait("Category", "integration")]
public sealed class ObserverTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task MSG_SEND_017_AC2_TheObserverIsToldOfAFinalStatusAtTheJobsRunAfterItCommitted()
    {
        DeliveryObserverFake observer = new();
        await using EngineHost host = await StartAsync(observer);
        string? seen = null;
        observer.Before = async (notice, _) => seen = await host.ColumnAsync<string>(notice.Delivery.Value, "status");
        SendReceipt receipt = (await host.SendAsync(SendTests.Code(channels: [Channel.Sms]))).Value!;
        await host.DispatchAsync();
        int beforeTheJob = observer.Calls.Count;

        await RunAsync(host);

        DeliveryNotice notice = Assert.Single(observer.Notices);
        Assert.Equal(0, beforeTheJob);
        Assert.Equal("sent", seen);
        Assert.Equal(
            (receipt.Send, receipt.Deliveries.Single().Delivery, EngineHost.Kind, EngineHost.Category, Channel.Sms, (string?)"person-1", DeliveryStatus.Sent, (AttemptReason?)null, EngineHost.Start),
            (notice.Send, notice.Delivery, notice.Kind, notice.Category, notice.Channel, notice.Person, notice.Status, notice.Reason, notice.At));
        Assert.Equal(0, await NoticesAsync(host));
    }

    [Fact]
    public async Task MSG_SEND_017_AC2_AnObserverThatThrewIsToldAgainAtTheJobsNextRun()
    {
        DeliveryObserverFake observer = new();
        int calls = 0;
        observer.Before = (_, _) => Interlocked.Increment(ref calls) == 1 ? throw new InvalidOperationException("The observer failed.") : Task.CompletedTask;
        await using EngineHost host = await StartAsync(observer);
        await host.SendAsync(SendTests.Code(channels: [Channel.Sms]));
        await host.DispatchAsync();

        await RunAsync(host);
        (int Calls, int Taken, long Kept, long Claimed) first = (observer.Calls.Count, observer.Notices.Count, await NoticesAsync(host), await NoticesAsync(host, claimed: true));
        await RunAsync(host);

        Assert.Equal((1, 0, 1L, 0L), first);
        Assert.Equal((2, 1, 0L), (observer.Calls.Count, observer.Notices.Count, await NoticesAsync(host)));
    }

    [Fact]
    public async Task MSG_SEND_017_AC2_ANoticeIsToldOnlyOnceNoLowerSequenceOfItsDeliveryIsPending()
    {
        DeliveryObserverFake observer = new();
        int sentCalls = 0;
        observer.Before = (notice, _) => notice.Status == DeliveryStatus.Sent && Interlocked.Increment(ref sentCalls) == 1 ? throw new InvalidOperationException("The observer failed.") : Task.CompletedTask;
        await using EngineHost host = await StartAsync(observer, configuration => configuration["Hermes:Observer:MaxCalls"] = "4");
        SendReceipt receipt = (await host.SendAsync(SendTests.Code(channels: [Channel.Sms]))).Value!;
        await host.DispatchAsync();
        Guid delivery = receipt.Deliveries.Single().Delivery.Value;
        await host.RunAsync(connection =>
        {
            NpgsqlCommand moved = new(
                """
                UPDATE messaging.deliveries SET status = 'delivered', next_sequence = 3 WHERE id = @id;
                INSERT INTO messaging.notices (delivery_id, sequence, status, reason, at) VALUES (@id, 2, 'delivered', NULL, @at)
                """,
                connection);
            moved.Parameters.AddWithValue("id", delivery);
            moved.Parameters.AddWithValue("at", EngineHost.Start.AddSeconds(1));
            return moved;
        });

        await RunAsync(host);
        DeliveryStatus[] first = [.. observer.Calls.Select(notice => notice.Status)];
        await RunAsync(host);

        Assert.Equal([DeliveryStatus.Sent], first);
        Assert.Equal([DeliveryStatus.Sent, DeliveryStatus.Sent, DeliveryStatus.Delivered], observer.Calls.Select(notice => notice.Status));
        Assert.Equal([DeliveryStatus.Sent, DeliveryStatus.Delivered], observer.Notices.Select(notice => notice.Status));
    }

    [Fact]
    public async Task MSG_SEND_017_ACallPastTheTimeoutIsNotTakenAndIsToldAgainAtTheNextRun()
    {
        DeliveryObserverFake observer = new();
        int calls = 0;
        observer.Before = (_, token) => Interlocked.Increment(ref calls) == 1 ? Task.Delay(Timeout.InfiniteTimeSpan, token) : Task.CompletedTask;
        await using EngineHost host = await StartAsync(observer, configuration => configuration["Hermes:Dispatch:Timeout"] = "00:00:01");
        await host.SendAsync(SendTests.Code(channels: [Channel.Sms]));
        await host.DispatchAsync();

        host.Clock.Advance(Interval);
        await UntilAsync(() => Task.FromResult(observer.Calls.Count == 1));
        host.Clock.Advance(TimeSpan.FromSeconds(1));
        await UntilAsync(async () => await CompletedAsync(host) is not null);
        (int Taken, long Kept, long Claimed) first = (observer.Notices.Count, await NoticesAsync(host), await NoticesAsync(host, claimed: true));
        await RunAsync(host);

        Assert.Equal((0, 1L, 0L), first);
        Assert.Equal((2, 1, 0L), (observer.Calls.Count, observer.Notices.Count, await NoticesAsync(host)));
    }

    [Fact]
    public async Task MSG_OPS_004_TwoProcessesRunningTheObserverJobTellEachNoticeOnce()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        DeliveryObserverFake first = new();
        DeliveryObserverFake second = new();
        await using EngineHost one = await StartAsync(first, connection: connection);
        await using EngineHost two = await StartAsync(second, connection: connection);
        for (int index = 0; index < 6; index++)
        {
            await one.SendAsync(SendTests.Code(channels: [Channel.Sms], recipient: new Recipient.Person($"person-{index}")));
        }

        await one.DispatchAsync();

        for (int run = 0; run < 3; run++)
        {
            DateTimeOffset? before = await CompletedAsync(one);
            one.Clock.Advance(Interval);
            two.Clock.Advance(Interval);
            await UntilAsync(async () => await CompletedAsync(one) is { } completed && (before is null || completed > before));
        }

        await UntilAsync(async () => await NoticesAsync(one) == 0);
        DeliveryNotice[] calls = [.. first.Calls, .. second.Calls];
        Assert.Equal(6, calls.Length);
        Assert.Equal(6, calls.Select(notice => notice.Delivery).Distinct().Count());
    }

    [Fact]
    public async Task MSG_OPS_004_AC3_AJobClaimOfAProcessThatDiedIsTakenOnceItExpires()
    {
        DeliveryObserverFake observer = new();
        await using EngineHost host = await StartAsync(observer);
        await host.SendAsync(SendTests.Code(channels: [Channel.Sms]));
        await host.DispatchAsync();
        await host.RunAsync(connection =>
        {
            NpgsqlCommand held = new(
                """
                INSERT INTO messaging.jobs (name, claimed_by, claimed_at, claim_expires_at, first_started_at, last_started_at)
                VALUES ('observer', @holder, @at, @expires, @at, @at)
                """,
                connection);
            held.Parameters.AddWithValue("holder", Guid.CreateVersion7());
            held.Parameters.AddWithValue("at", EngineHost.Start.AddMinutes(-1));
            held.Parameters.AddWithValue("expires", EngineHost.Start.AddMinutes(9));
            return held;
        });

        host.Clock.Advance(Interval);
        await Task.Delay(300, TestContext.Current.CancellationToken);
        int whileHeld = observer.Calls.Count;
        host.Clock.Advance(TimeSpan.FromMinutes(9));
        await UntilAsync(() => Task.FromResult(observer.Notices.Count == 1));

        Assert.Equal(0, whileHeld);
    }

    [Fact]
    public async Task MSG_OPS_004_WithTheObserverOffTheObserverJobCompletesAtOnceTellingNothing()
    {
        await using EngineHost host = await EngineHost.StartAsync(
            await EngineHost.DatabaseAsync(postgres),
            configure: EngineHost.Dispatching,
            services: Providers);
        await host.SendAsync(SendTests.Code(channels: [Channel.Sms]));
        await host.DispatchAsync();

        await RunAsync(host);

        Assert.Equal(0, await NoticesAsync(host));
        Assert.Equal(0, await host.RunAsync(connection => new NpgsqlCommand("UPDATE messaging.jobs SET name = name WHERE name = 'observer' AND claimed_by IS NOT NULL", connection)));
    }

    [Fact]
    public async Task MSG_OPS_004_AC1_WithDispatchOffTheObserverJobDoesNotRun()
    {
        await using EngineHost host = await EngineHost.StartAsync(
            await EngineHost.DatabaseAsync(postgres),
            configure: configuration => configuration["Hermes:Dispatch:Enabled"] = "false",
            services: Providers);

        host.Clock.Advance(Interval);
        await Task.Delay(300, TestContext.Current.CancellationToken);

        Assert.Null(await CompletedAsync(host));
    }

    private async Task<EngineHost> StartAsync(DeliveryObserverFake observer, Action<Dictionary<string, string?>>? configure = null, string? connection = null)
    {
        return await EngineHost.StartAsync(
            connection ?? await EngineHost.DatabaseAsync(postgres),
            configure: configuration =>
            {
                EngineHost.Dispatching(configuration);
                configuration["Hermes:Observer:Enabled"] = "true";
                configure?.Invoke(configuration);
            },
            services: services =>
            {
                Providers(services);
                services.AddSingleton<IDeliveryObserver>(observer);
            });
    }

    private static void Providers(IServiceCollection services)
    {
        services.AddSingleton<IEmailProvider>(_ => new RecordingEmailProvider());
        services.AddSingleton<ISmsProvider>(_ => new RecordingSmsProvider());
    }

    /// <summary>Moves the clock to the job's next run and waits for that run to complete.</summary>
    internal static async Task RunAsync(EngineHost host)
    {
        DateTimeOffset? before = await CompletedAsync(host);
        host.Clock.Advance(Interval);
        await UntilAsync(async () => await CompletedAsync(host) is { } completed && (before is null || completed > before));
    }

    private static async Task<DateTimeOffset?> CompletedAsync(EngineHost host)
    {
        await using NpgsqlConnection connection = new(host.Connection);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("SELECT last_completed_at FROM messaging.jobs WHERE name = 'observer'", connection);
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) is DateTime completed ? new DateTimeOffset(completed, TimeSpan.Zero) : null;
    }

    private static async Task<long> NoticesAsync(EngineHost host, bool claimed = false)
    {
        await using NpgsqlConnection connection = new(host.Connection);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("SELECT count(*) FROM messaging.notices WHERE NOT @claimed OR claimed_by IS NOT NULL", connection);
        command.Parameters.AddWithValue("claimed", claimed);
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
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
}