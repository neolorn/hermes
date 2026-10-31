using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Sending;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Xunit;

namespace Hermes.Tests;

/// <summary>The <c>retention</c> job (<c>01</c> MSG-SEND-019; <c>06</c> MSG-OPS-004), run by the test unless it says so.</summary>
[Trait("Category", "integration")]
public sealed class RetentionTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    /// <summary>A trigger counting each idempotency row deleted.</summary>
    private const string Counting = """
        CREATE TABLE public.deleted_keys (key text NOT NULL);
        CREATE FUNCTION public.count_deleted() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN INSERT INTO public.deleted_keys VALUES (OLD.key); RETURN OLD; END $$;
        CREATE TRIGGER count_deleted AFTER DELETE ON messaging.idempotency FOR EACH ROW EXECUTE FUNCTION public.count_deleted();
        """;

    /// <summary>250 lapsed idempotency rows, more than two batches of 100.</summary>
    private const string Lapsed = """
        INSERT INTO messaging.idempotency (kind, anonymous, scope, key, send_id, receipt, digest, hash_version, recorded_at, lapses_at)
        SELECT 'sign.in', true, '\x00', 'key-' || g, gen_random_uuid(), '{}', '\x00', 1, @now, @now FROM generate_series(1, 250) g
        """;

    private const string Subscription = """
        INSERT INTO messaging.subscriptions (id, person, endpoint_hash, hash_version, endpoint, keys, vapid_version, service, made_at, last_accepted_at)
        VALUES (gen_random_uuid(), @person, @hash, 1, '\x00', '\x00', 1, 'other', @made, @accepted)
        """;

    private static readonly string[] ContentColumns = ["destination", "unsubscribe", "subject", "text", "html", "idempotency_key", "content_key_version"];

    [Fact]
    public async Task MSG_SEND_019_AC1_ContentGoes31DaysPastTheFinalStatusTheRecord366DaysPastItAndAWaitingDeliveryKeepsEverything()
    {
        await using DispatchEngine engine = await DispatchEngine.StartAsync(postgres, configure: UnsubscribeLinkTests.Configure, declare: UnsubscribeLinkTests.Declare, templates: UnsubscribeLinkTests.Templates);
        SendReceipt sent = (await engine.Host.SendAsync(UnsubscribeLinkTests.LetterTo("someone@example.com") with { IdempotencyKey = "letter-1" })).Value!;
        await engine.Host.DispatchAsync();
        SendReceipt waiting = (await engine.Host.SendAsync(UnsubscribeLinkTests.LetterTo("other@example.com") with { IdempotencyKey = "letter-2" })).Value!;
        Guid letter = sent.Deliveries[0].Delivery.Value;
        Guid kept = waiting.Deliveries[0].Delivery.Value;

        engine.Host.Clock.Advance(TimeSpan.FromDays(30) - TimeSpan.FromSeconds(1));
        Assert.True(await RunAsync(engine.Host));
        bool heldAt30 = await engine.Host.ColumnAsync<byte[]>(letter, "unsubscribe") is not null;
        engine.Host.Clock.Advance(TimeSpan.FromDays(1) + TimeSpan.FromSeconds(1));
        Assert.True(await RunAsync(engine.Host));

        Assert.True(heldAt30);
        foreach (string column in ContentColumns)
        {
            Assert.Null(await engine.Host.ColumnAsync<object>(letter, column));
        }

        DeliveryRecord record = Assert.Single((await engine.Host.ReadAsync(sent.Send)).Deliveries);
        Assert.Equal((DeliveryStatus.Sent, 1), (record.Status, record.Attempts.Count));
        Assert.Equal("person-1", await engine.Host.ColumnAsync<string>(letter, "person"));

        engine.Host.Clock.Advance(TimeSpan.FromDays(335));
        Assert.True(await RunAsync(engine.Host));

        Assert.Equal("messaging.send.unknown", (await engine.Host.WithMessagesAsync(messages => messages.ReadAsync(sent.Send, TestContext.Current.CancellationToken).AsTask())).Failure!.Code);
        Assert.Equal(0L, await CountAsync(engine.Host, database => new("SELECT count(*) FROM messaging.attempts", database)));
        Assert.Equal(DeliveryStatus.Waiting, Assert.Single((await engine.Host.ReadAsync(waiting.Send)).Deliveries).Status);
        foreach (string column in ContentColumns)
        {
            Assert.NotNull(await engine.Host.ColumnAsync<object>(kept, column));
        }
    }

    [Fact]
    public async Task MSG_SEND_019_AC2_ALapsedIdempotencyRowAndALapsedLimitPlaceAreGoneAndTheLiveOnesStay()
    {
        await using DispatchEngine engine = await DispatchEngine.StartAsync(postgres, declare: SubjectTests.Declare);
        await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Email]) with { IdempotencyKey = "lapsed" });
        await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Email]) with { IdempotencyKey = "live" });
        await engine.Host.SendAsync(LimitTests.ReminderTo("+201000000001"));
        await engine.Host.SendAsync(LimitTests.ReminderTo("+201000000002"));
        await engine.Host.DispatchAsync();
        await RunSqlAsync(engine.Host, database => new("UPDATE messaging.idempotency SET lapses_at = @now WHERE key = 'lapsed'", database));
        await RunSqlAsync(engine.Host, database => new("UPDATE messaging.places SET lapses_at = @now WHERE id = (SELECT min(id) FROM messaging.places)", database));

        Assert.True(await RunAsync(engine.Host));

        Assert.Equal(["live"], await TextsAsync(engine.Host, database => new("SELECT key FROM messaging.idempotency", database)));
        Assert.Equal(1L, await CountAsync(engine.Host, database => new("SELECT count(*) FROM messaging.places", database)));
    }

    [Fact]
    public async Task MSG_SEND_019_AC2_ASuppressionPastItsLifetimeAndASubscriptionIdlePastIdleRetentionAreGone()
    {
        await using DispatchEngine engine = await DispatchEngine.StartAsync(
            postgres,
            configure: configuration => configuration["Hermes:Retention:Suppression:Sms"] = "30.00:00:00",
            declare: SuppressionTests.Declare);
        Assert.True((await SuppressionTests.AddAsync(engine.Host, Channel.Sms, "+201000000001", SuppressionTests.Offers)).IsSuccess);
        Assert.True((await SuppressionTests.AddAsync(engine.Host, Channel.Sms, "+201000000002", SuppressionTests.Offers)).IsSuccess);
        Assert.True((await SuppressionTests.AddAsync(engine.Host, Channel.Email, "someone@example.com", SuppressionTests.Offers)).IsSuccess);
        await RunSqlAsync(engine.Host, database => new("UPDATE messaging.suppressions SET made_at = @now - interval '30 days' WHERE hash = (SELECT hash FROM messaging.suppressions WHERE channel = 'sms' LIMIT 1)", database));
        await RunSqlAsync(engine.Host, database => new("UPDATE messaging.suppressions SET made_at = @now - interval '3000 days' WHERE channel = 'email'", database));
        await SubscribeAsync(engine.Host, "idle", 200, 180);
        await SubscribeAsync(engine.Host, "used", 200, 179);
        await SubscribeAsync(engine.Host, "never", 180, null);
        await SubscribeAsync(engine.Host, "new", 179, null);

        Assert.True(await RunAsync(engine.Host));

        Assert.Equal(["email", "sms"], await TextsAsync(engine.Host, database => new("SELECT channel FROM messaging.suppressions ORDER BY channel", database)));
        Assert.Equal(["new", "used"], await TextsAsync(engine.Host, database => new("SELECT person FROM messaging.subscriptions ORDER BY person", database)));
    }

    [Fact]
    public async Task MSG_SEND_019_AC2_ANoticeOlderThanTheRecordsRetentionIsGoneAndItsDeletionLogged()
    {
        using LogCapture logs = new();
        await using DispatchEngine engine = await DispatchEngine.StartAsync(postgres, declare: SubjectTests.Declare, logs: logs);
        SendReceipt receipt = (await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]))).Value!;
        Guid delivery = receipt.Deliveries[0].Delivery.Value;
        await engine.Host.RunAsync(database =>
        {
            NpgsqlCommand command = new(
                """
                INSERT INTO messaging.notices (delivery_id, sequence, status, reason, at)
                VALUES (@id, 1, 'cancelled', NULL, @now - interval '365 days'), (@id, 2, 'cancelled', NULL, @now - interval '364 days')
                """,
                database);
            command.Parameters.AddWithValue("id", delivery);
            command.Parameters.AddWithValue("now", engine.Host.Clock.GetUtcNow());
            return command;
        });

        Assert.True(await RunAsync(engine.Host));

        Assert.Equal(["2"], await TextsAsync(engine.Host, database => new("SELECT sequence::text FROM messaging.notices", database)));
        Assert.Contains(logs.Entries, entry => entry.Message == "The retention job deleted 1 observer notices older than the record's retention.");
    }

    [Fact]
    public async Task MSG_SEND_019_AC2_ADeliveryOfAKindNeitherDeclaredNorRetiredGoesTheRecordsRetentionAfterTheLaterOfItsRecordingAndDueInstantLoggedByCount()
    {
        using LogCapture logs = new();
        await using DispatchEngine engine = await DispatchEngine.StartAsync(postgres, declare: SubjectTests.Declare, logs: logs);
        SendReceipt old = (await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]))).Value!;
        SendReceipt due = (await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]))).Value!;
        SendReceipt known = (await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]))).Value!;
        await RunSqlAsync(engine.Host, database => new("UPDATE messaging.deliveries SET kind = 'gone.kind', recorded_at = @now - interval '365 days', due_at = @now - interval '365 days'", database));
        await RunSqlAsync(engine.Host, database => new("UPDATE messaging.sends SET recorded_at = @now - interval '365 days'", database));
        await engine.Host.RunAsync(database =>
        {
            NpgsqlCommand command = new("UPDATE messaging.deliveries SET due_at = @now - interval '364 days' WHERE send_id = @due; UPDATE messaging.deliveries SET kind = @kind WHERE send_id = @known", database);
            command.Parameters.AddWithValue("now", engine.Host.Clock.GetUtcNow());
            command.Parameters.AddWithValue("due", due.Send.Value);
            command.Parameters.AddWithValue("known", known.Send.Value);
            command.Parameters.AddWithValue("kind", EngineHost.Kind);
            return command;
        });

        Assert.True(await RunAsync(engine.Host));

        List<string> left = await TextsAsync(engine.Host, database => new("SELECT send_id::text FROM messaging.deliveries", database));
        Assert.Equal([.. new[] { due.Send.Value, known.Send.Value }.Order()], [.. left.Select(Guid.Parse).Order()]);
        Assert.Equal(2L, await CountAsync(engine.Host, database => new("SELECT count(*) FROM messaging.sends", database)));
        Assert.Equal("messaging.send.unknown", (await engine.Host.WithMessagesAsync(messages => messages.ReadAsync(old.Send, TestContext.Current.CancellationToken).AsTask())).Failure!.Code);
        Assert.Contains(logs.Entries, entry => entry.Message == "The retention job deleted 1 deliveries of a channel, status, reason or kind this release or application does not know.");
    }

    [Fact]
    public async Task MSG_SEND_019_AC2_OldBalanceReadingsSaveTheLastBalanceAndOldReportAuthFailureAndRateCountersAreGone()
    {
        await using DispatchEngine engine = await DispatchEngine.StartAsync(
            postgres,
            configure: configuration =>
            {
                configuration["Hermes:Sms:DrainBaseline"] = "7.00:00:00";
                configuration["Hermes:Sms:ReportRejectWindow"] = "01:00:00";
                configuration["Hermes:Push:AuthFailWindow"] = "01:00:00";
            },
            declare: SubjectTests.Declare);
        await RunSqlAsync(engine.Host, database => new(
            """
            INSERT INTO messaging.balance_readings (read_at, units) VALUES
                (@now - interval '10 days', 100), (@now - interval '9 days', 90), (@now - interval '8 days', NULL), (@now - interval '1 day', NULL);
            INSERT INTO messaging.rejected_reports (minute, count) VALUES (@now - interval '61 minutes', 1), (@now - interval '60 minutes', 1);
            INSERT INTO messaging.push_auth_failures (service, minute, count) VALUES ('fcm', @now - interval '61 minutes', 1), ('fcm', @now - interval '60 minutes', 1);
            INSERT INTO messaging.rate_counters (channel, second, count) VALUES ('sms', @now - interval '1 second', 1), ('sms', @now, 1);
            """,
            database));

        Assert.True(await RunAsync(engine.Host));

        DateTimeOffset now = engine.Host.Clock.GetUtcNow();
        Assert.Equal([now.AddDays(-9), now.AddDays(-1)], await InstantsAsync(engine.Host, database => new("SELECT read_at FROM messaging.balance_readings ORDER BY read_at", database)));
        Assert.Equal([now.AddMinutes(-60)], await InstantsAsync(engine.Host, database => new("SELECT minute FROM messaging.rejected_reports", database)));
        Assert.Equal([now.AddMinutes(-60)], await InstantsAsync(engine.Host, database => new("SELECT minute FROM messaging.push_auth_failures", database)));
        Assert.Equal([now], await InstantsAsync(engine.Host, database => new("SELECT second FROM messaging.rate_counters", database)));
    }

    [Fact]
    public async Task MSG_OPS_004_AC2_TwoProcessesRunningTheRetentionJobAtOnceDeleteEachRowOnce()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        ManualClock clock = new(EngineHost.Start);
        Action<Dictionary<string, string?>> configure = configuration => configuration["Hermes:Jobs:BatchSize"] = "100";
        await using DispatchEngine first = await DispatchEngine.StartAsync(postgres, configure: configure, connection: connection, clock: clock);
        await using DispatchEngine second = await DispatchEngine.StartAsync(postgres, configure: configure, connection: connection, clock: clock);
        await RunSqlAsync(first.Host, database => new(Lapsed, database));
        await RunSqlAsync(first.Host, database => new("UPDATE messaging.idempotency SET lapses_at = @now", database));
        await RunSqlAsync(first.Host, database => new(Counting, database));

        bool[] ran = await Task.WhenAll(RunAsync(first.Host), RunAsync(second.Host));

        Assert.Single(ran, run => run);
        Assert.Equal(0L, await CountAsync(first.Host, database => new("SELECT count(*) FROM messaging.idempotency", database)));
        Assert.Equal(250L, await CountAsync(first.Host, database => new("SELECT count(*) FROM public.deleted_keys", database)));
        Assert.Equal(250L, await CountAsync(first.Host, database => new("SELECT count(DISTINCT key) FROM public.deleted_keys", database)));
        Assert.False(await RunAsync(first.Host));
    }

    [Fact]
    public async Task MSG_SEND_001_AC3_ADroppedKindsDeliveryWaitsUntilTheRetentionJobDeletesItOnceTheRecordsRetentionHasPassed()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        ManualClock clock = new(EngineHost.Start);
        SendReceipt dropped;
        await using (DispatchEngine before = await DispatchEngine.StartAsync(postgres, declare: SubjectTests.Declare, connection: connection, clock: clock))
        {
            dropped = (await before.Host.SendAsync(LimitTests.ReminderTo("+201000000001"))).Value!;
        }

        await using DispatchEngine after = await DispatchEngine.StartAsync(postgres, connection: connection, clock: clock);
        await after.Host.DispatchAsync();
        clock.Advance(TimeSpan.FromDays(365) - TimeSpan.FromSeconds(1));
        Assert.True(await RunAsync(after.Host));
        string status = (await after.Host.ColumnAsync<string>(dropped.Deliveries[0].Delivery.Value, "status"))!;
        clock.Advance(TimeSpan.FromDays(1));
        Assert.True(await RunAsync(after.Host));

        Assert.Equal("waiting", status);
        Assert.Equal(0L, await CountAsync(after.Host, database => new("SELECT count(*) FROM messaging.deliveries", database)));
        Assert.Equal(0L, await CountAsync(after.Host, database => new("SELECT count(*) FROM messaging.sends", database)));
    }

    [Fact]
    public async Task MSG_SEND_007_AC5_ARowOfAChannelOrReasonThisReleaseDoesNotKnowIsDeletedOnlyOnceTheRecordsRetentionHasPassedSinceTheLaterOfItsRecordingAndDueInstant()
    {
        const string Widen = """
            ALTER TABLE messaging.deliveries DROP CONSTRAINT ck_deliveries_channel;
            ALTER TABLE messaging.deliveries DROP CONSTRAINT ck_deliveries_reason;
            UPDATE messaging.deliveries SET channel = 'fax', due_at = due_at + interval '10 days' WHERE id = @channel;
            UPDATE messaging.deliveries SET reason = 'carrierlost' WHERE id = @reason;
            """;
        await using DispatchEngine engine = await DispatchEngine.StartAsync(postgres);
        Guid channel = (await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]))).Value!.Deliveries[0].Delivery.Value;
        Guid reason = (await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]))).Value!.Deliveries[0].Delivery.Value;
        await engine.Host.RunAsync(database =>
        {
            NpgsqlCommand command = new(Widen, database);
            command.Parameters.AddWithValue("channel", channel);
            command.Parameters.AddWithValue("reason", reason);
            return command;
        });

        engine.Host.Clock.Advance(TimeSpan.FromDays(365) - TimeSpan.FromSeconds(1));
        Assert.True(await RunAsync(engine.Host));
        long beforeTheRecords = await CountAsync(engine.Host, database => new("SELECT count(*) FROM messaging.deliveries", database));
        engine.Host.Clock.Advance(TimeSpan.FromDays(1));
        Assert.True(await RunAsync(engine.Host));
        List<string> afterTheRecords = await TextsAsync(engine.Host, database => new("SELECT channel FROM messaging.deliveries", database));
        engine.Host.Clock.Advance(TimeSpan.FromDays(10));
        Assert.True(await RunAsync(engine.Host));

        Assert.Equal(2L, beforeTheRecords);
        Assert.Equal(["fax"], afterTheRecords);
        Assert.Equal(0L, await CountAsync(engine.Host, database => new("SELECT count(*) FROM messaging.deliveries", database)));
    }

    [Fact]
    public async Task MSG_SEND_009_AC4_TheRetentionJobOfAnApplicationWhoseWindowIs1DayLeavesARowWrittenUnder7DaysUntilItsOwnLapse()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        ManualClock clock = new(EngineHost.Start);
        await using (EngineHost week = await EngineHost.StartAsync(connection, configure: configuration => configuration["Hermes:Idempotency:Window"] = "7.00:00:00", clock: clock))
        {
            Assert.True((await week.SendAsync(SendTests.Code(channels: [Channel.Email]) with { IdempotencyKey = "order-1001" })).IsSuccess);
        }

        await using EngineHost day = await EngineHost.StartAsync(connection, clock: clock);
        clock.Advance(TimeSpan.FromDays(2));
        Assert.True(await RunAsync(day));
        long onDay2 = await CountAsync(day, database => new("SELECT count(*) FROM messaging.idempotency", database));
        clock.Advance(TimeSpan.FromDays(5));
        Assert.True(await RunAsync(day));

        Assert.Equal(1L, onDay2);
        Assert.Equal(0L, await CountAsync(day, database => new("SELECT count(*) FROM messaging.idempotency", database)));
    }

    [Fact]
    public async Task MSG_SEND_014_AC6_APlaceWrittenUnderRetentionPlacesOf90DaysIsKeptByAJobWhoseOwnIs31DaysUntilThePlacesLapse()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        ManualClock clock = new(EngineHost.Start);
        await using (DispatchEngine writer = await DispatchEngine.StartAsync(postgres, configure: configuration => configuration["Hermes:Retention:Places"] = "90.00:00:00", declare: SubjectTests.Declare, connection: connection, clock: clock))
        {
            await writer.Host.SendAsync(LimitTests.ReminderTo("+201000000001"));
            await writer.Host.DispatchAsync();
        }

        await using DispatchEngine reader = await DispatchEngine.StartAsync(postgres, declare: SubjectTests.Declare, connection: connection, clock: clock);
        long taken = await CountAsync(reader.Host, database => new("SELECT count(*) FROM messaging.places", database));
        clock.Advance(TimeSpan.FromDays(90) - TimeSpan.FromSeconds(1));
        Assert.True(await RunAsync(reader.Host));
        long beforeTheLapse = await CountAsync(reader.Host, database => new("SELECT count(*) FROM messaging.places", database));
        clock.Advance(TimeSpan.FromDays(1));
        Assert.True(await RunAsync(reader.Host));

        Assert.Equal((1L, 1L), (taken, beforeTheLapse));
        Assert.Equal(0L, await CountAsync(reader.Host, database => new("SELECT count(*) FROM messaging.places", database)));
    }

    [Fact]
    public async Task Retention_WithJobsOn_RunsAtRetentionAtInTheTimeZoneAndNotBefore()
    {
        TimeZoneInfo cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        await using DispatchEngine engine = await DispatchEngine.StartAsync(
            postgres,
            configure: configuration =>
            {
                configuration["Hermes:Jobs:Enabled"] = "true";
                configuration["Hermes:TimeZone"] = "Africa/Cairo";
                configuration["Hermes:Jobs:RetentionAt"] = "04:30:00";
            });
        DateTimeOffset at = RetentionJob.Next(EngineHost.Start, new TimeOnly(4, 30), cairo);

        // The host starts the job's loop on its own thread; its wait must be set before the clock moves.
        for (int tries = 0; tries < 400 && !engine.Host.Clock.IsDue(at); tries++)
        {
            await Task.Delay(25, TestContext.Current.CancellationToken);
        }

        engine.Host.Clock.Advance(at - EngineHost.Start - TimeSpan.FromSeconds(1));
        await Task.Delay(200, TestContext.Current.CancellationToken);
        DateTimeOffset? before = await StartedAsync(engine.Host);
        engine.Host.Clock.Advance(TimeSpan.FromSeconds(1));
        for (int tries = 0; tries < 400 && await StartedAsync(engine.Host) is null; tries++)
        {
            await Task.Delay(25, TestContext.Current.CancellationToken);
        }

        Assert.Equal(new TimeOnly(4, 30), TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, cairo).DateTime));
        Assert.Null(before);
        Assert.Equal(at, await StartedAsync(engine.Host));
    }

    private static Task<bool> RunAsync(EngineHost host)
    {
        return host.Services.GetRequiredService<RetentionJob>().RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Records a subscription of <paramref name="person"/> made <paramref name="madeDaysAgo"/> days ago and last accepted <paramref name="acceptedDaysAgo"/> days ago, or never.</summary>
    private static Task<int> SubscribeAsync(EngineHost host, string person, int madeDaysAgo, int? acceptedDaysAgo)
    {
        DateTimeOffset now = host.Clock.GetUtcNow();
        return host.RunAsync(database =>
        {
            NpgsqlCommand command = new(Subscription, database);
            command.Parameters.AddWithValue("person", person);
            command.Parameters.AddWithValue("hash", System.Text.Encoding.UTF8.GetBytes(person));
            command.Parameters.AddWithValue("made", now.AddDays(-madeDaysAgo));
            command.Parameters.Add(new NpgsqlParameter<DateTimeOffset?>("accepted", acceptedDaysAgo is { } days ? now.AddDays(-days) : null));
            return command;
        });
    }

    /// <summary>Runs the command <paramref name="made"/> makes, its parameter <c>@now</c> the clock's instant.</summary>
    private static Task<int> RunSqlAsync(EngineHost host, Func<NpgsqlConnection, NpgsqlCommand> made)
    {
        return host.RunAsync(database =>
        {
            NpgsqlCommand command = made(database);
            command.Parameters.AddWithValue("now", host.Clock.GetUtcNow());
            return command;
        });
    }

    private static async Task<long> CountAsync(EngineHost host, Func<NpgsqlConnection, NpgsqlCommand> made)
    {
        await using NpgsqlConnection database = new(host.Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = made(database);
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private static async Task<List<T>> ReadAsync<T>(EngineHost host, Func<NpgsqlConnection, NpgsqlCommand> made, Func<NpgsqlDataReader, T> read)
    {
        List<T> values = [];
        await using NpgsqlConnection database = new(host.Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = made(database);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            values.Add(read(reader));
        }

        return values;
    }

    private static Task<List<string>> TextsAsync(EngineHost host, Func<NpgsqlConnection, NpgsqlCommand> made)
    {
        return ReadAsync(host, made, reader => reader.GetString(0));
    }

    private static Task<List<DateTimeOffset>> InstantsAsync(EngineHost host, Func<NpgsqlConnection, NpgsqlCommand> made)
    {
        return ReadAsync(host, made, reader => new DateTimeOffset(reader.GetDateTime(0), TimeSpan.Zero));
    }

    private static async Task<DateTimeOffset?> StartedAsync(EngineHost host)
    {
        List<DateTimeOffset> started = await InstantsAsync(host, database => new("SELECT last_started_at FROM messaging.jobs WHERE name = 'retention' AND last_started_at IS NOT NULL", database));
        return started.Count == 0 ? null : started[0];
    }
}