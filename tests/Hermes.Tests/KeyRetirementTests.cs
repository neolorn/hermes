using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Sending;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Xunit;

namespace Hermes.Tests;

/// <summary>The retention job's work on the key versions (<c>06</c> MSG-OPS-008): values protected again under the write version, and versions nothing needs marked retired.</summary>
[Trait("Category", "integration")]
public sealed class KeyRetirementTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    /// <summary>A trigger that holds each reservation of <c>contentKeys</c> values until the test lets go of advisory lock 4243.</summary>
    private const string Holding = """
        CREATE FUNCTION public.hold_reservation() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN PERFORM pg_advisory_xact_lock_shared(4243); RETURN NEW; END $$;
        CREATE TRIGGER hold_reservation BEFORE UPDATE ON messaging.key_versions FOR EACH ROW
        WHEN (OLD.value_count IS DISTINCT FROM NEW.value_count) EXECUTE FUNCTION public.hold_reservation();
        """;

    private const string Later = "99991231000000_Later";

    private const string Person = SendTests.Person;

    private static readonly string[] ContentColumns = ["destination", "unsubscribe", "subject", "text", "html", "content_key_version"];

    [Fact]
    public async Task MSG_OPS_008_AC4_TheRetentionJobProtectsADeliveryAndASubscriptionAgainUnderTheWriteVersionAndRetiresTheOldOne()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        Guid delivery;
        Guid subscription = Guid.CreateVersion7();
        await using (EngineHost one = await EngineHost.StartAsync(connection))
        {
            delivery = (await one.SendAsync(SendTests.Code(channels: [Channel.Email]))).Value!.Deliveries[0].Delivery.Value;
            await SubscribeAsync(one, subscription);
        }

        await using EngineHost two = await StartRotatedAsync(connection, days: 8);
        Assert.True(await RunAsync(two));

        Assert.Equal(2, await two.ColumnAsync<int>(delivery, "content_key_version"));
        Assert.Equal(SendTests.Email, await two.OpenAsync(delivery, "destination"));
        Assert.Equal(2, await ScalarAsync<int>(two, database => new("SELECT content_key_version FROM messaging.subscriptions", database)));
        Assert.Equal("https://push.example.com/send/first", await OpenSubscriptionAsync(two, subscription, "endpoint"));
        Assert.Equal("keys", await OpenSubscriptionAsync(two, subscription, "keys"));
        Assert.Contains("contentKeys:1", await RetiredAsync(two));
    }

    [Fact]
    public async Task MSG_OPS_008_AC5_AValueErasedWhileTheRetentionJobProtectsItAgainStaysErased()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        Guid delivery;
        await using (EngineHost one = await EngineHost.StartAsync(connection))
        {
            delivery = (await one.SendAsync(SendTests.Code(channels: [Channel.Email]))).Value!.Deliveries[0].Delivery.Value;
        }

        await using EngineHost two = await StartRotatedAsync(connection, days: 8);
        await using NpgsqlConnection gate = new(connection);
        await gate.OpenAsync(TestContext.Current.CancellationToken);
        await using (NpgsqlCommand held = new("SELECT pg_advisory_lock(4243)", gate))
        {
            await held.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await two.RunAsync(database => new NpgsqlCommand(Holding, database));
        Task<bool> run = RunAsync(two);
        await PushTests.UntilAsync(async () => await PushTests.WaitingAsync(two, "advisory") > 0);
        Task<Result> erased = SubjectTests.EraseAsync(two, Person);
        await PushTests.UntilAsync(async () => await PushTests.WaitingAsync(two, "transactionid") > 0);
        await using (NpgsqlCommand released = new("SELECT pg_advisory_unlock(4243)", gate))
        {
            await released.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        Assert.True((await erased).IsSuccess);
        Assert.True(await run);
        foreach (string column in ContentColumns)
        {
            Assert.Null(await two.ColumnAsync<object>(delivery, column));
        }

        Assert.Null(await two.ColumnAsync<string>(delivery, "person"));
    }

    [Fact]
    public async Task MSG_OPS_008_AC6_ARetiredHashKeysVersionCanBeRemovedWithoutStoppingAStartAndAProcessWritingUnderItClearsTheMark()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        await using (EngineHost one = await EngineHost.StartAsync(connection))
        {
            Assert.True((await one.SendAsync(SendTests.Code(channels: [Channel.Email]) with { IdempotencyKey = "order-1" })).IsSuccess);
        }

        ManualClock clock = new(EngineHost.Start.AddDays(8));
        await using (EngineHost two = await StartRotatedAsync(connection, clock))
        {
            await two.RunAsync(database => new NpgsqlCommand("UPDATE messaging.idempotency SET lapses_at = 'infinity'", database));
            Assert.True(await RunAsync(two));
            Assert.DoesNotContain("hashKeys:1", await RetiredAsync(two));

            clock.Advance(TimeSpan.FromDays(1));
            await two.RunAsync(database => new NpgsqlCommand("UPDATE messaging.idempotency SET lapses_at = '-infinity'", database));
            Assert.True(await RunAsync(two));
            Assert.Contains("hashKeys:1", await RetiredAsync(two));
        }

        SecretSourceFake withoutOne = Rotated(connection).WithVersions("hashKeys", (2, Secrets.Bytes(32, 5)));
        await using (EngineHost three = await EngineHost.StartAsync(connection, configure: WriteTwo, secrets: withoutOne, clock: new ManualClock(clock.GetUtcNow())))
        {
            Assert.Contains("hashKeys:1", await RetiredAsync(three));
        }

        await using EngineHost four = await EngineHost.StartAsync(
            connection,
            configure: configuration =>
            {
                WriteTwo(configuration);
                configuration["Hermes:Secrets:HashKeys:WriteVersion"] = "1";
            },
            secrets: Rotated(connection),
            clock: new ManualClock(clock.GetUtcNow()));
        Assert.DoesNotContain("hashKeys:1", await RetiredAsync(four));
    }

    [Fact]
    public async Task MSG_OPS_008_AC7_WithAMigrationThisReleaseDoesNotKnowTheRetentionJobRetiresNoVersionAndProtectsNoValueAgain()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        Guid delivery;
        await using (EngineHost one = await EngineHost.StartAsync(connection))
        {
            delivery = (await one.SendAsync(SendTests.Code(channels: [Channel.Email]) with { IdempotencyKey = "order-1" })).Value!.Deliveries[0].Delivery.Value;
        }

        await using EngineHost two = await StartRotatedAsync(connection, days: 8);
        await two.RunAsync(database => new NpgsqlCommand("UPDATE messaging.idempotency SET lapses_at = '-infinity'", database));
        await two.RunAsync(database => new NpgsqlCommand($"INSERT INTO messaging.__migrations_history VALUES ('{Later}', '10.0.14')", database));

        Assert.True(await RunAsync(two));

        Assert.Empty(await RetiredAsync(two));
        Assert.Equal(1, await two.ColumnAsync<int>(delivery, "content_key_version"));
        Assert.Equal(0L, await ScalarAsync<long>(two, database => new("SELECT count(*) FROM messaging.idempotency", database)));
    }

    [Fact]
    public async Task MSG_OPS_008_AC8_ARowTheRetentionJobCannotOpenKeepsItsValuesAndColumnAndItsContentKeysVersionIsNotRetired()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        Guid tampered;
        Guid clean;
        await using (EngineHost one = await EngineHost.StartAsync(connection))
        {
            tampered = (await one.SendAsync(SendTests.Code(channels: [Channel.Email]))).Value!.Deliveries[0].Delivery.Value;
            clean = (await one.SendAsync(SendTests.Code(email: "other@example.com", channels: [Channel.Email]))).Value!.Deliveries[0].Delivery.Value;
        }

        await using EngineHost two = await StartRotatedAsync(connection, days: 8);
        await two.RunAsync(database =>
        {
            NpgsqlCommand command = new("UPDATE messaging.deliveries SET subject = set_byte(subject, length(subject) - 1, get_byte(subject, length(subject) - 1) # 1) WHERE id = @id", database);
            command.Parameters.AddWithValue("id", tampered);
            return command;
        });
        byte[] subject = (await two.ColumnAsync<byte[]>(tampered, "subject"))!;
        byte[] destination = (await two.ColumnAsync<byte[]>(tampered, "destination"))!;

        Assert.True(await RunAsync(two));

        Assert.Equal(subject, await two.ColumnAsync<byte[]>(tampered, "subject"));
        Assert.Equal(destination, await two.ColumnAsync<byte[]>(tampered, "destination"));
        Assert.Equal(1, await two.ColumnAsync<int>(tampered, "content_key_version"));
        Assert.Equal(2, await two.ColumnAsync<int>(clean, "content_key_version"));
        Assert.DoesNotContain("contentKeys:1", await RetiredAsync(two));
    }

    [Fact]
    public async Task MSG_OPS_008_AC10_ATokenVersionIsKeptWhileItsEmailWaitsAndUntilTheLongestTokenLifetimeAndRetireAfterHavePassedSinceItWentOut()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        await using (EngineHost recording = await EngineHost.StartAsync(connection, UnsubscribeLinkTests.Declare, UnsubscribeLinkTests.Templates, UnsubscribeLinkTests.Configure))
        {
            Assert.True((await recording.SendAsync(UnsubscribeLinkTests.LetterTo("someone@example.com"))).IsSuccess);
        }

        await using (EngineHost mapping = await StartLettersAsync(connection, EngineHost.Start, configuration => configuration["Hermes:Unsubscribe:TokenLifetime"] = "1826.00:00:00"))
        {
            Assert.Equal(TimeSpan.FromDays(1826), await ScalarAsync<TimeSpan>(mapping, database => new("SELECT longest FROM messaging.token_lifetime", database)));
        }

        DateTimeOffset waited = EngineHost.Start + TimeSpan.FromDays(1826 + 7) + TimeSpan.FromHours(1);
        await using (EngineHost waiting = await StartLettersAsync(connection, waited))
        {
            Assert.True(await RunAsync(waiting));
            Assert.Empty(TokenVersions(await RetiredAsync(waiting)));
        }

        DateTimeOffset sent = waited + TimeSpan.FromHours(1);
        await using (DispatchEngine dispatching = await DispatchEngine.StartAsync(
            postgres,
            configure: configuration =>
            {
                UnsubscribeLinkTests.Configure(configuration);
                WriteTwo(configuration);
            },
            declare: UnsubscribeLinkTests.Declare,
            templates: UnsubscribeLinkTests.Templates,
            connection: connection,
            clock: new ManualClock(sent),
            secrets: Rotated(connection)))
        {
            await dispatching.Host.DispatchAsync();
            Assert.Single(dispatching.Email.Received);
        }

        await using (EngineHost twoYears = await StartLettersAsync(connection, sent + TimeSpan.FromDays(738)))
        {
            Assert.True(await RunAsync(twoYears));
            Assert.Empty(TokenVersions(await RetiredAsync(twoYears)));
        }

        await using EngineHost fiveYears = await StartLettersAsync(connection, sent + TimeSpan.FromDays(1826 + 7) + TimeSpan.FromHours(1));
        Assert.True(await RunAsync(fiveYears));
        Assert.Equal(["hashKeys:1", "tokenKeys:1"], TokenVersions(await RetiredAsync(fiveYears)));
    }

    /// <summary>The secrets of a process holding versions 1 and 2 of <c>contentKeys</c>, <c>hashKeys</c> and <c>tokenKeys</c>.</summary>
    private static SecretSourceFake Rotated(string connection)
    {
        return Secrets.Held(connection)
            .With("contentKeys", Secrets.Bytes(32, 1), Secrets.Bytes(32, 4))
            .With("hashKeys", Secrets.Bytes(32, 2), Secrets.Bytes(32, 5))
            .With("tokenKeys", Secrets.Bytes(32, 3), Secrets.Bytes(32, 6));
    }

    private static void WriteTwo(Dictionary<string, string?> configuration)
    {
        configuration["Hermes:Secrets:ContentKeys:WriteVersion"] = "2";
        configuration["Hermes:Secrets:HashKeys:WriteVersion"] = "2";
        configuration["Hermes:Secrets:TokenKeys:WriteVersion"] = "2";
    }

    private static Task<EngineHost> StartRotatedAsync(string connection, int days)
    {
        return StartRotatedAsync(connection, new ManualClock(EngineHost.Start.AddDays(days)));
    }

    private static Task<EngineHost> StartRotatedAsync(string connection, ManualClock clock)
    {
        return EngineHost.StartAsync(connection, configure: WriteTwo, secrets: Rotated(connection), clock: clock);
    }

    /// <summary>A process declaring the letter, writing under the second versions, its clock at <paramref name="at"/>.</summary>
    private static Task<EngineHost> StartLettersAsync(string connection, DateTimeOffset at, Action<Dictionary<string, string?>>? configure = null)
    {
        return EngineHost.StartAsync(
            connection,
            UnsubscribeLinkTests.Declare,
            UnsubscribeLinkTests.Templates,
            configuration =>
            {
                UnsubscribeLinkTests.Configure(configuration);
                WriteTwo(configuration);
                configure?.Invoke(configuration);
            },
            secrets: Rotated(connection),
            clock: new ManualClock(at));
    }

    private static Task<bool> RunAsync(EngineHost host)
    {
        return host.Services.GetRequiredService<RetentionJob>().RunAsync(TestContext.Current.CancellationToken);
    }

    private static List<string> TokenVersions(List<string> retired)
    {
        return retired.FindAll(version => version.StartsWith("hashKeys:", StringComparison.Ordinal) || version.StartsWith("tokenKeys:", StringComparison.Ordinal));
    }

    /// <summary>The versions marked retired, each as <c>key:version</c>, in order.</summary>
    private static async Task<List<string>> RetiredAsync(EngineHost host)
    {
        await using NpgsqlConnection database = new(host.Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("SELECT key || ':' || version FROM messaging.key_versions WHERE retired ORDER BY key, version", database);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        List<string> retired = [];
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            retired.Add(reader.GetString(0));
        }

        return retired;
    }

    private static async Task<T> ScalarAsync<T>(EngineHost host, Func<NpgsqlConnection, NpgsqlCommand> make)
    {
        await using NpgsqlConnection database = new(host.Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = make(database);
        return (T)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    /// <summary>Records a push subscription of the person whose endpoint and keys are protected under the write version.</summary>
    private static async Task SubscribeAsync(EngineHost host, Guid id)
    {
        byte[] endpoint = await ProtectAsync(host, id, "endpoint", "https://push.example.com/send/first");
        byte[] keys = await ProtectAsync(host, id, "keys", "keys");
        await host.RunAsync(database =>
        {
            NpgsqlCommand command = new(
                """
                INSERT INTO messaging.subscriptions (id, person, endpoint_hash, hash_version, endpoint, keys, content_key_version, vapid_version, service, made_at)
                VALUES (@id, @person, '\x00', 1, @endpoint, @keys, 1, 1, 'other', @now)
                """,
                database);
            command.Parameters.AddWithValue("id", id);
            command.Parameters.AddWithValue("person", Person);
            command.Parameters.AddWithValue("endpoint", endpoint);
            command.Parameters.AddWithValue("keys", keys);
            command.Parameters.AddWithValue("now", host.Clock.GetUtcNow());
            return command;
        });
    }

    private static async Task<byte[]> ProtectAsync(EngineHost host, Guid id, string field, string value)
    {
        AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using (scope)
        {
            ContentProtection protection = scope.ServiceProvider.GetRequiredService<ContentProtection>();
            return (await protection.ProtectAsync(Person, Encoding.UTF8.GetBytes(value), ContentContext.Of(ContentContext.Subscriptions, id, field), TestContext.Current.CancellationToken)).Stored!;
        }
    }

    private static async Task<string> OpenSubscriptionAsync(EngineHost host, Guid id, string field)
    {
        byte[] stored = await ScalarAsync<byte[]>(host, database =>
        {
            NpgsqlCommand command = new("SELECT CASE WHEN @field = 'keys' THEN keys ELSE endpoint END FROM messaging.subscriptions WHERE id = @id", database);
            command.Parameters.AddWithValue("field", field);
            command.Parameters.AddWithValue("id", id);
            return command;
        });
        AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using (scope)
        {
            ContentOpening opening = await scope.ServiceProvider.GetRequiredService<ContentProtection>().OpenAsync(Person, stored, ContentContext.Of(ContentContext.Subscriptions, id, field), TestContext.Current.CancellationToken);
            Assert.Equal(ContentOpeningStatus.Opened, opening.Status);
            return Encoding.UTF8.GetString(opening.Plaintext.Span);
        }
    }
}