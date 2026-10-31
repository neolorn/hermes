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

/// <summary>A person's erasure and export (<c>01</c> MSG-SEND-020).</summary>
[Trait("Category", "integration")]
public sealed class SubjectTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Person = SendTests.Person;

    private const string Number = SendTests.Phone;

    private const string Later = "99991231000000_Later";

    private const string LinkedPlaces = "SELECT count(*) FROM messaging.places WHERE delivery_id = ANY(@ids)";

    private const string UnlinkedLimitPlaces = "SELECT count(*) FROM messaging.places WHERE delivery_id IS NULL AND kind = 'limit' AND cardinality(@ids) > 0";

    /// <summary>A session's function answering every row of every table of the library's schema as JSON, by table, save the tables it is given.</summary>
    private const string DumpFunction = """
        CREATE OR REPLACE FUNCTION pg_temp.dump(skipped text[]) RETURNS text LANGUAGE plpgsql AS $$
        DECLARE
            name text;
            part text;
            whole text := '';
        BEGIN
            FOR name IN SELECT table_name FROM information_schema.tables WHERE table_schema = 'messaging' AND NOT (table_name = ANY(skipped)) ORDER BY table_name LOOP
                EXECUTE format('SELECT coalesce(string_agg(row_to_json(t)::text, '' '' ORDER BY row_to_json(t)::text), '''') FROM messaging.%I t', name) INTO part;
                whole := whole || name || ': ' || part || E'\n';
            END LOOP;
            RETURN whole;
        END
        $$
        """;

    private const string HeldValues = """
        SELECT count(*) FROM messaging.deliveries
        WHERE id = ANY(@ids)
          AND (person IS NOT NULL OR destination IS NOT NULL OR unsubscribe IS NOT NULL OR subject IS NOT NULL OR text IS NOT NULL OR html IS NOT NULL
               OR title IS NOT NULL OR body IS NOT NULL OR url IS NOT NULL OR tag IS NOT NULL OR icon IS NOT NULL OR badge IS NOT NULL OR idempotency_key IS NOT NULL OR content_key_version IS NOT NULL)
        """;

    /// <summary>The tables a refused call may still change: an alert's state and a job's claim.</summary>
    private static readonly string[] Volatile = ["alerts", "jobs"];

    [Fact]
    public async Task MSG_SEND_020_AC1_AfterAnErasureNothingNamesThePersonOrHoldsTheirKeyNoKeptPlaceLinksTheirRecordsAndTheObserverIsToldOfTheCancellations()
    {
        DeliveryObserverFake observer = new();
        await using DispatchEngine engine = await DispatchEngine.StartAsync(
            postgres,
            configure: configuration => configuration["Hermes:Observer:Enabled"] = "true",
            declare: Declare,
            services: services => services.AddSingleton<IDeliveryObserver>(observer));
        SendReceipt sent = (await engine.Host.SendAsync(LimitTests.ReminderTo(Number))).Value!;
        await engine.Host.DispatchAsync();
        SendReceipt waiting = (await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Email, Channel.Sms]) with { IdempotencyKey = "order-1" })).Value!;
        Assert.True((await PreferenceTests.SetAsync(engine.Host, Person, SuppressionTests.Offers, Channel.Sms, isOn: false)).IsSuccess);
        Assert.True((await SuppressionTests.AddAsync(engine.Host, Channel.Sms, Number, SuppressionTests.Offers)).IsSuccess);
        Guid[] deliveries = [.. sent.Deliveries.Concat(waiting.Deliveries).Select(delivery => delivery.Delivery.Value)];

        Result erased = await EraseAsync(engine.Host, Person);

        Assert.True(erased.IsSuccess);
        string dump = await DumpAsync(engine.Host);
        Assert.DoesNotContain(Person, dump, StringComparison.Ordinal);
        Assert.DoesNotContain(Hex(Person), dump, StringComparison.Ordinal);
        Assert.DoesNotContain("order-1", dump, StringComparison.Ordinal);
        Assert.Equal(0L, await CountAsync(engine.Host, database => new NpgsqlCommand(LinkedPlaces, database), deliveries));
        Assert.Equal(1L, await CountAsync(engine.Host, database => new NpgsqlCommand(UnlinkedLimitPlaces, database), deliveries));
        Assert.Equal(0L, await CountAsync(engine.Host, database => new NpgsqlCommand(HeldValues, database), deliveries));
        Assert.Equal(DeliveryStatus.Sent, Assert.Single((await engine.Host.ReadAsync(sent.Send)).Deliveries).Status);
        Assert.All((await engine.Host.ReadAsync(waiting.Send)).Deliveries, delivery => Assert.Equal(DeliveryStatus.Cancelled, delivery.Status));

        SendReceipt offer = (await engine.Host.SendAsync(SuppressionTests.OfferTo(Number) with { Recipient = new Recipient.Anonymous() })).Value!;
        await engine.Host.DispatchAsync();
        await ObserverTests.RunAsync(engine.Host);

        Assert.Equal(DeliveryStatus.Suppressed, await SuppressionTests.StatusAsync(engine.Host, offer));
        Assert.Equal(
            [.. waiting.Deliveries.Select(delivery => delivery.Delivery.Value).Order()],
            [.. observer.Notices.Where(notice => notice.Status == DeliveryStatus.Cancelled).Select(notice => notice.Delivery.Value).Order()]);
        Assert.All(observer.Notices, notice => Assert.Equal(((string?)null, (string?)null), (notice.Person, notice.IdempotencyKey)));
    }

    [Fact]
    public async Task Erase_APersonTheLibraryHoldsNothingOf_SucceedsAndChangesNothing()
    {
        await using DispatchEngine engine = await DispatchEngine.StartAsync(postgres, declare: Declare);
        SendReceipt other = (await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]) with { Recipient = new Recipient.Person("person-2") })).Value!;
        string before = await DumpAsync(engine.Host);

        Result erased = await EraseAsync(engine.Host, Person);

        Assert.True(erased.IsSuccess);
        Assert.Equal(before, await DumpAsync(engine.Host));
        Assert.Equal(DeliveryStatus.Waiting, await SuppressionTests.StatusAsync(engine.Host, other));
    }

    [Fact]
    public async Task MSG_SEND_020_AC3_AHashKeysVersionInUseThatTheProcessDoesNotHoldAnswersKeyUnavailableAndChangesNothing()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        AlertSinkFake alerts = new();
        await using EngineHost one = await EngineHost.StartAsync(connection, declare: Declare, services: services => services.AddSingleton<IAlertSink>(alerts));
        await HoldUnderASecondHashKeysVersionAsync(one, connection);
        string before = await DumpAsync(one, Volatile);

        Result erased = await EraseAsync(one, Person);

        Assert.Equal("messaging.key.unavailable", erased.Failure!.Code);
        Assert.Equal(new Dictionary<string, string> { ["key"] = "hashKeys" }, erased.Failure.Details);
        Assert.Contains(alerts.Calls, call => call is { Raised: true, Condition: "key-unavailable", Scope: "hashKeys" });
        Assert.Equal(before, await DumpAsync(one, Volatile));
    }

    [Fact]
    public async Task MSG_SEND_020_AC5_AnUnknownMigrationAnswersReleaseNewerToAnErasureAndAnExportEvenWhereAKeyVersionIsNotHeld()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        await using EngineHost one = await EngineHost.StartAsync(connection, declare: Declare);
        await HoldUnderASecondHashKeysVersionAsync(one, connection);
        await one.RunAsync(database => new NpgsqlCommand($"INSERT INTO messaging.__migrations_history VALUES ('{Later}', '10.0.14')", database));
        string before = await DumpAsync(one, Volatile);

        Result erased = await EraseAsync(one, Person);
        Result<SubjectExport> exported = await ExportAsync(one, Person);

        Assert.Equal("messaging.release.newer", erased.Failure!.Code);
        Assert.Equal(new Dictionary<string, string> { ["migration"] = Later }, erased.Failure.Details);
        Assert.Equal("messaging.release.newer", exported.Failure!.Code);
        Assert.Equal(new Dictionary<string, string> { ["migration"] = Later }, exported.Failure.Details);
        Assert.Equal(before, await DumpAsync(one, Volatile));
    }

    [Fact]
    public async Task MSG_SEND_020_AC2_TheExportHoldsThePersonsPreferencesIdempotencyRowsAndDeliveriesWithTheirDestinationsContentAndKeptKeys()
    {
        await using DispatchEngine engine = await DispatchEngine.StartAsync(postgres, declare: Declare);
        await engine.Host.SendAsync(LimitTests.ReminderTo(Number));
        await engine.Host.DispatchAsync();
        engine.Host.Clock.Advance(TimeSpan.FromMinutes(1));
        await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Email, Channel.Sms]) with { IdempotencyKey = "order-1" });
        await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]) with { Recipient = new Recipient.Person("person-2") });
        Assert.True((await PreferenceTests.SetAsync(engine.Host, Person, SuppressionTests.Offers, Channel.Sms, isOn: false)).IsSuccess);

        SubjectExport export = (await ExportAsync(engine.Host, Person)).Value!;

        Assert.False(Assert.Single(Assert.Single(export.Preferences, category => category.Category == SuppressionTests.Offers).Channels).On);
        Assert.All(export.Preferences.Where(category => category.Category != SuppressionTests.Offers).SelectMany(category => category.Channels), channel => Assert.True(channel.On));
        Assert.Empty(export.Subscriptions);
        IdempotencyKeyExport key = Assert.Single(export.IdempotencyKeys);
        Assert.Equal((EngineHost.Kind, "order-1", EngineHost.Start.AddMinutes(1)), (key.Kind, key.Key, key.RecordedAt));
        Assert.Equal(3, export.Deliveries.Count);
        DeliveryExport reminder = Assert.Single(export.Deliveries, delivery => delivery.Kind == LimitTests.Reminder);
        Assert.Equal((Channel.Sms, DeliveryStatus.Sent, EngineHost.Start, (string?)Number), (reminder.Channel, reminder.Status, reminder.RecordedAt, reminder.Destination));
        Assert.Equal(new Dictionary<string, string> { ["text"] = "Sms en 123456" }, reminder.Content);
        DeliveryExport email = Assert.Single(export.Deliveries, delivery => delivery.Channel == Channel.Email);
        Assert.Equal((DeliveryStatus.Waiting, (string?)"someone@example.com"), (email.Status, email.Destination));
        Assert.Equal(new Dictionary<string, string> { ["subject"] = "Subject en 123456", ["text"] = "Text en 123456" }, email.Content);
        Assert.All(export.Deliveries, delivery => Assert.False(delivery.ContentDeleted || delivery.Erased || delivery.Rejected));
        Assert.Equal((string?)null, reminder.IdempotencyKey);
        Assert.All(export.Deliveries.Where(delivery => delivery.Kind == EngineHost.Kind), delivery => Assert.Equal("order-1", delivery.IdempotencyKey));
    }

    [Fact]
    public async Task MSG_SEND_020_AC2_ADeliveryPastContentRetentionIsExportedMarkedSoWithNoDestinationOrContent()
    {
        await using DispatchEngine engine = await DispatchEngine.StartAsync(postgres, declare: Declare);
        SendReceipt receipt = (await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]) with { IdempotencyKey = "order-1" })).Value!;
        await engine.Host.DispatchAsync();
        await engine.Host.RunAsync(database =>
        {
            NpgsqlCommand command = new("UPDATE messaging.deliveries SET destination = NULL, text = NULL, idempotency_key = NULL, content_key_version = NULL WHERE id = @id", database);
            command.Parameters.AddWithValue("id", receipt.Deliveries[0].Delivery.Value);
            return command;
        });

        DeliveryExport delivery = Assert.Single((await ExportAsync(engine.Host, Person)).Value!.Deliveries);

        Assert.Equal((true, false, false, (string?)null, DeliveryStatus.Sent, (string?)null), (delivery.ContentDeleted, delivery.Erased, delivery.Rejected, delivery.Destination, delivery.Status, delivery.IdempotencyKey));
        Assert.Empty(delivery.Content);
    }

    [Fact]
    public async Task MSG_SEND_020_AC2_AValueThatDoesNotOpenUnderItsBindingIsExportedMarkedRejectedAndRaisesContentRejected()
    {
        AlertSinkFake alerts = new();
        await using DispatchEngine engine = await DispatchEngine.StartAsync(postgres, declare: Declare, services: services => services.AddSingleton<IAlertSink>(alerts));
        SendReceipt receipt = (await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]) with { IdempotencyKey = "order-1" })).Value!;
        await engine.Host.RunAsync(database =>
        {
            NpgsqlCommand command = new("UPDATE messaging.deliveries SET text = set_byte(text, length(text) - 1, get_byte(text, length(text) - 1) # 1) WHERE id = @id", database);
            command.Parameters.AddWithValue("id", receipt.Deliveries[0].Delivery.Value);
            return command;
        });

        DeliveryExport delivery = Assert.Single((await ExportAsync(engine.Host, Person)).Value!.Deliveries);

        Assert.Equal((false, false, true, (string?)null, (string?)null), (delivery.ContentDeleted, delivery.Erased, delivery.Rejected, delivery.Destination, delivery.IdempotencyKey));
        Assert.Empty(delivery.Content);
        Assert.Contains(alerts.Calls, call => call is { Raised: true, Condition: "content-rejected", Scope: "deliveries" } && call.Details!["table"] == "deliveries");
    }

    [Fact]
    public async Task MSG_SEND_020_AC2_AValueWhoseProtectorAnswersErasedIsExportedMarkedErasedWithNoDestinationOrContent()
    {
        ContentProtectorFake vault = new("vault");
        await using DispatchEngine engine = await DispatchEngine.StartAsync(postgres, declare: Declare, services: services => services.AddSingleton<IContentProtector>(vault));
        await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Email]) with { IdempotencyKey = "order-1" });
        vault.Erased = true;

        DeliveryExport delivery = Assert.Single((await ExportAsync(engine.Host, Person)).Value!.Deliveries);

        Assert.Equal((false, true, false, (string?)null, DeliveryStatus.Waiting, (string?)null), (delivery.ContentDeleted, delivery.Erased, delivery.Rejected, delivery.Destination, delivery.Status, delivery.IdempotencyKey));
        Assert.Empty(delivery.Content);
    }

    [Fact]
    public async Task MSG_SEND_020_AC2_AValueThatCannotOpenYetAnswersTheExportKeyUnavailable()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        await using EngineHost holding = await EngineHost.StartAsync(connection, declare: Declare, services: services => services.AddSingleton<IContentProtector>(new ContentProtectorFake("vault")));
        await using EngineHost lacking = await EngineHost.StartAsync(connection, declare: Declare);
        await holding.SendAsync(SendTests.Code(channels: [Channel.Sms]) with { IdempotencyKey = "order-1" });

        Result<SubjectExport> exported = await ExportAsync(lacking, Person);

        Assert.Equal("messaging.key.unavailable", exported.Failure!.Code);
        Assert.Equal(new Dictionary<string, string> { ["key"] = "vault" }, exported.Failure.Details);
        Assert.True((await ExportAsync(holding, Person)).IsSuccess);
    }

    [Fact]
    public async Task MSG_SEND_020_AC2_AProtectorThatThrowsOrDoesNotAnswerInTimeAnswersTheExportKeyUnavailable()
    {
        ContentProtectorFake vault = new("vault");
        await using DispatchEngine engine = await DispatchEngine.StartAsync(postgres, declare: Declare, services: services => services.AddSingleton<IContentProtector>(vault));
        await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]));
        vault.BeforeOpen = () => throw new InvalidOperationException("The vault cannot be reached.");

        Result<SubjectExport> thrown = await ExportAsync(engine.Host, Person);
        vault.BeforeOpen = () => Task.Delay(Timeout.Infinite, TestContext.Current.CancellationToken);
        Task<Result<SubjectExport>> waiting = ExportAsync(engine.Host, Person);
        for (int tries = 0; tries < 400 && !waiting.IsCompleted; tries++)
        {
            engine.Host.Clock.Advance(TimeSpan.FromSeconds(30));
            await Task.Delay(25, TestContext.Current.CancellationToken);
        }

        Assert.Equal("messaging.key.unavailable", thrown.Failure!.Code);
        Assert.Equal("messaging.key.unavailable", (await waiting).Failure!.Code);
    }

    /// <summary>
    /// An essential category of every channel, the optional category <c>offers</c> with an SMS
    /// kind, and the category <c>reminders</c> with an SMS limit.
    /// </summary>
    internal static void Declare(DeclarationBuilder declare)
    {
        ArgumentNullException.ThrowIfNull(declare);
        SuppressionTests.Declare(declare);
        declare.Category(LimitTests.Reminders).Limit(Channel.Sms, 5, TimeSpan.FromDays(1));
        declare.Kind(LimitTests.Reminder, LimitTests.Reminders).Sms();
    }

    internal static Task<Result> EraseAsync(EngineHost host, string person)
    {
        return host.WithSubjectsAsync(subjects => subjects.EraseAsync(person, TestContext.Current.CancellationToken).AsTask());
    }

    internal static Task<Result<SubjectExport>> ExportAsync(EngineHost host, string person)
    {
        return host.WithSubjectsAsync(subjects => subjects.ExportAsync(person, TestContext.Current.CancellationToken).AsTask());
    }

    /// <summary>Every row of every table of the library's schema, as JSON, save the tables in <paramref name="except"/>.</summary>
    internal static async Task<string> DumpAsync(EngineHost host, params string[] except)
    {
        await using NpgsqlConnection database = new(host.Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using (NpgsqlCommand made = new(DumpFunction, database))
        {
            await made.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await using NpgsqlCommand dump = new("SELECT pg_temp.dump(@except)", database);
        dump.Parameters.AddWithValue("except", except);
        return (string)(await dump.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    /// <summary>The text's UTF-8 bytes in lower-case hexadecimal, as a JSON dump writes a <c>bytea</c>.</summary>
    internal static string Hex(string text)
    {
        return Convert.ToHexStringLower(Encoding.UTF8.GetBytes(text));
    }

    private static async Task<long> CountAsync(EngineHost host, Func<NpgsqlConnection, NpgsqlCommand> made, Guid[] ids)
    {
        await using NpgsqlConnection database = new(host.Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = made(database);
        command.Parameters.AddWithValue("ids", ids);
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    /// <summary>Leaves the person with a send and a preference, and a suppression under a second <c>hashKeys</c> version <paramref name="one"/> does not hold.</summary>
    private static async Task HoldUnderASecondHashKeysVersionAsync(EngineHost one, string connection)
    {
        Assert.True((await one.SendAsync(SendTests.Code(channels: [Channel.Email]))).IsSuccess);
        Assert.True((await PreferenceTests.SetAsync(one, Person, SuppressionTests.Offers, Channel.Sms, isOn: false)).IsSuccess);
        await using EngineHost two = await EngineHost.StartAsync(
            connection,
            declare: Declare,
            configure: configuration => configuration["Hermes:Secrets:HashKeys:WriteVersion"] = "2",
            secrets: Secrets.Held(connection).WithVersions("hashKeys", (1, Secrets.Bytes(32, 2)), (2, Secrets.Bytes(32, 5))));
        Assert.True((await SuppressionTests.AddAsync(two, Channel.Sms, Number, SuppressionTests.Offers)).IsSuccess);
    }
}