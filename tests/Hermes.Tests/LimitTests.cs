using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Sending;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "integration")]
public sealed class LimitTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    internal const string Reminders = "reminders";

    internal const string Reminder = "reminder.due";

    private const string Number = "+201000000001";

    [Fact]
    public async Task Limit_TheThirdDeliveryToOneNumberWithinItsWindowEndsLimitedAndAFailedOneGivesItsPlaceBack()
    {
        await using DispatchEngine engine = await StartAsync(category => category.Limit(Channel.Sms, 2, TimeSpan.FromDays(1)));
        int calls = 0;
        engine.Sms.Answer = _ => ++calls == 1 ? new Attempt.Permanent(AttemptReason.RecipientRefused, 400) : new Attempt.Accepted(null);
        List<SendReceipt> receipts = [];

        for (int send = 0; send < 4; send++)
        {
            receipts.Add((await engine.Host.SendAsync(ReminderTo(Number))).Value!);
            await engine.Host.DispatchAsync();
            engine.Host.Clock.Advance(TimeSpan.FromHours(1));
        }

        SendReceipt other = (await engine.Host.SendAsync(ReminderTo("+201000000002"))).Value!;
        await engine.Host.DispatchAsync();

        Assert.Equal(
            [DeliveryStatus.Failed, DeliveryStatus.Sent, DeliveryStatus.Sent, DeliveryStatus.Limited],
            await Task.WhenAll(receipts.Select(receipt => SuppressionTests.StatusAsync(engine.Host, receipt))));
        Assert.Equal(DeliveryStatus.Sent, await SuppressionTests.StatusAsync(engine.Host, other));
        Assert.Empty(Assert.Single((await engine.Host.ReadAsync(receipts[3].Send)).Deliveries).Attempts);
        Assert.Equal(4, engine.Sms.Received.Count);
    }

    [Fact]
    public async Task MSG_SEND_014_AC1_WithALimitOf2SmsADayTheThirdToOneNumberEndsLimitedAndAfterOneOfTheFirstTwoIsReportedFailedAFurtherOneIsSent()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        await using DispatchEngine engine = await DispatchEngine.StartAsync(
            postgres,
            declare: Declare(category => category.Limit(Channel.Sms, 2, TimeSpan.FromDays(1))),
            connection: connection,
            services: services => services.AddSingleton<ISecretSource>(Secrets.Held(connection).With("smsReportKey", Secrets.Bytes(16, 5))),
            map: SmsReportTests.Map);
        using System.Net.Http.HttpClient client = engine.Host.Client();
        List<SendReceipt> receipts = [];
        for (int send = 0; send < 3; send++)
        {
            receipts.Add((await engine.Host.SendAsync(ReminderTo(Number))).Value!);
            await engine.Host.DispatchAsync();
            engine.Host.Clock.Advance(TimeSpan.FromHours(1));
        }

        DeliveryStatus third = await SuppressionTests.StatusAsync(engine.Host, receipts[2]);
        using System.Net.Http.FormUrlEncodedContent body = new([new("ref", engine.Sms.Received[0].DeliveryReference), new("status", "failed")]);
        using System.Net.Http.HttpRequestMessage request = new(System.Net.Http.HttpMethod.Post, Endpoints.Prefix + SmsReportTests.Route()) { Content = body };
        request.Headers.Add(SmsReportTests.SourceHeader, "127.0.0.1");
        using System.Net.Http.HttpResponseMessage reported = await client.SendAsync(request, TestContext.Current.CancellationToken);
        SendReceipt further = (await engine.Host.SendAsync(ReminderTo(Number))).Value!;
        await engine.Host.DispatchAsync();

        Assert.Equal(DeliveryStatus.Limited, third);
        Assert.Equal(System.Net.HttpStatusCode.OK, reported.StatusCode);
        Assert.Equal(DeliveryStatus.Failed, await SuppressionTests.StatusAsync(engine.Host, receipts[0]));
        Assert.Equal(DeliveryStatus.Sent, await SuppressionTests.StatusAsync(engine.Host, further));
        Assert.Equal(3, engine.Sms.Received.Count);
    }

    [Fact]
    public async Task MSG_SEND_014_AC5_WithAnSmsRateOf5NoCounterSecondHoldsMoreThanFiveSmsAcrossTwoProcesses()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        ManualClock clock = new(EngineHost.Start);
        await using DispatchEngine first = await DispatchEngine.StartAsync(postgres, configure: configuration => configuration["Hermes:Sms:Rate"] = "5", connection: connection, clock: clock);
        await using DispatchEngine second = await DispatchEngine.StartAsync(postgres, configure: configuration => configuration["Hermes:Sms:Rate"] = "5", connection: connection, clock: clock);
        for (int index = 0; index < 12; index++)
        {
            await first.Host.SendAsync(SendTests.Code(channels: [Channel.Sms], recipient: new Recipient.Person($"person-{index}")));
        }

        int[] inFirst = await Task.WhenAll(first.Host.DispatchAsync(), second.Host.DispatchAsync());
        clock.Advance(TimeSpan.FromSeconds(1));
        int[] inSecond = await Task.WhenAll(first.Host.DispatchAsync(), second.Host.DispatchAsync());

        Assert.Equal((5, 5), (inFirst.Sum(), inSecond.Sum()));
        Assert.Equal(10, first.Sms.Received.Count + second.Sms.Received.Count);
        await using NpgsqlConnection database = new(connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("SELECT max(count) FROM messaging.rate_counters WHERE channel = 'sms'", database);
        Assert.Equal(5, Convert.ToInt32(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Limit_AfterItsWindow_ADeliveryToTheSameNumberFitsAgain()
    {
        await using DispatchEngine engine = await StartAsync(category => category.Limit(Channel.Sms, 1, TimeSpan.FromDays(1)));
        SendReceipt first = (await engine.Host.SendAsync(ReminderTo(Number))).Value!;
        await engine.Host.DispatchAsync();

        engine.Host.Clock.Advance(TimeSpan.FromDays(1));
        SendReceipt second = (await engine.Host.SendAsync(ReminderTo(Number))).Value!;
        await engine.Host.DispatchAsync();

        Assert.Equal(DeliveryStatus.Sent, await SuppressionTests.StatusAsync(engine.Host, first));
        Assert.Equal(DeliveryStatus.Sent, await SuppressionTests.StatusAsync(engine.Host, second));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MSG_SEND_014_AC2_TwoDispatchersCheckingTwoDeliveriesForTheLastPlaceAtOnceDispatchOne(bool differentWriteVersions)
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        ManualClock clock = new(EngineHost.Start);
        SecretSourceFake secrets = Secrets.Held(connection).With("hashKeys", Secrets.Bytes(32, 2), Secrets.Bytes(32, 9));
        Action<DeclarationBuilder> declare = Declare(category => category.Limit(Channel.Sms, 1, TimeSpan.FromDays(1)));
        await using DispatchEngine first = await DispatchEngine.StartAsync(
            postgres,
            configure: configuration => Configure(configuration, writeVersion: 1),
            declare: declare,
            connection: connection,
            clock: clock,
            services: services => services.AddSingleton<ISecretSource>(secrets));
        await using DispatchEngine second = await DispatchEngine.StartAsync(
            postgres,
            configure: configuration => Configure(configuration, writeVersion: differentWriteVersions ? 2 : 1),
            declare: declare,
            connection: connection,
            clock: clock,
            services: services => services.AddSingleton<ISecretSource>(secrets));
        SendReceipt one = (await first.Host.SendAsync(ReminderTo(Number))).Value!;
        SendReceipt two = (await second.Host.SendAsync(ReminderTo(Number))).Value!;

        int[] claimed = await Task.WhenAll(first.Host.DispatchAsync(), second.Host.DispatchAsync());

        DeliveryStatus[] statuses = await Task.WhenAll(SuppressionTests.StatusAsync(first.Host, one), SuppressionTests.StatusAsync(first.Host, two));
        Assert.Equal([1, 1], claimed);
        Assert.Equal([DeliveryStatus.Sent, DeliveryStatus.Limited], statuses.Order());
        Assert.Equal(1, first.Sms.Received.Count + second.Sms.Received.Count);
    }

    [Fact]
    public async Task MSG_SEND_014_AC3_WithADeploymentLimitOf3SmsAnHourTheFourthToAnyNumberEndsLimitedAndAFailureDoesNotLetAFourthThrough()
    {
        await using DispatchEngine engine = await StartAsync(category => category.DeploymentLimit(Channel.Sms, 3, TimeSpan.FromHours(1)));
        int calls = 0;
        engine.Sms.Answer = _ => ++calls == 1 ? new Attempt.Permanent(AttemptReason.RecipientRefused, 400) : new Attempt.Accepted(null);
        List<SendReceipt> receipts = [];

        for (int send = 1; send <= 4; send++)
        {
            receipts.Add((await engine.Host.SendAsync(ReminderTo("+20100000000" + send))).Value!);
            await engine.Host.DispatchAsync();
        }

        Assert.Equal(
            [DeliveryStatus.Failed, DeliveryStatus.Sent, DeliveryStatus.Sent, DeliveryStatus.Limited],
            await Task.WhenAll(receipts.Select(receipt => SuppressionTests.StatusAsync(engine.Host, receipt))));
        Assert.Equal(3, engine.Sms.Received.Count);
    }

    [Fact]
    public async Task MSG_SEND_014_AC4_WithLimitsOf2ADayAnd5AWeekOnOneChannelAFirstSmsLeavesEachWithRoomForTheRestOfItsCount()
    {
        await using DispatchEngine engine = await StartAsync(category => category.Limit(Channel.Sms, 2, TimeSpan.FromDays(1)).Limit(Channel.Sms, 5, TimeSpan.FromDays(7)));
        SendReceipt first = (await engine.Host.SendAsync(ReminderTo(Number))).Value!;
        await engine.Host.DispatchAsync();
        IReadOnlyList<(string Kind, int Window)> places = await PlacesAsync(engine.Host, first.Deliveries[0].Delivery.Value);
        List<DeliveryStatus> later = [];

        for (int day = 0; day < 3; day++)
        {
            for (int send = 0; send < 2; send++)
            {
                later.Add(await SuppressionTests.StatusAsync(engine.Host, await DispatchedAsync(engine)));
            }

            engine.Host.Clock.Advance(TimeSpan.FromDays(1));
        }

        Assert.Equal([("limit", 86_400), ("limit", 604_800)], places);
        Assert.Equal(DeliveryStatus.Sent, await SuppressionTests.StatusAsync(engine.Host, first));
        Assert.Equal(
            [DeliveryStatus.Sent, DeliveryStatus.Limited, DeliveryStatus.Sent, DeliveryStatus.Sent, DeliveryStatus.Sent, DeliveryStatus.Limited],
            later);
    }

    [Fact]
    public async Task Limit_APlace_RecordsItsLapseAsTheWritersRetentionPlacesAndNoDestination()
    {
        await using DispatchEngine engine = await StartAsync(
            category => category.Limit(Channel.Sms, 2, TimeSpan.FromDays(1)).DeploymentLimit(Channel.Sms, 10, TimeSpan.FromHours(1)),
            configuration => configuration["Hermes:Retention:Places"] = "90.00:00:00");
        SendReceipt receipt = (await engine.Host.SendAsync(ReminderTo(Number))).Value!;
        await engine.Host.DispatchAsync();

        await using NpgsqlConnection database = new(engine.Host.Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("SELECT kind, lapses_at, coalesce(encode(hash, 'hex'), '') FROM messaging.places WHERE delivery_id = @id ORDER BY kind", database);
        command.Parameters.AddWithValue("id", receipt.Deliveries[0].Delivery.Value);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        List<(string Kind, DateTime Lapses, string Hash)> rows = [];
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            rows.Add((reader.GetString(0), reader.GetDateTime(1), reader.GetString(2)));
        }

        Assert.Equal(["deployment", "limit"], rows.Select(row => row.Kind));
        Assert.All(rows, row => Assert.Equal(EngineHost.Start.AddDays(90).UtcDateTime, row.Lapses));
        Assert.Equal(string.Empty, rows[0].Hash);
        Assert.DoesNotContain(Convert.ToHexString(Encoding.UTF8.GetBytes("201000000001")), rows[1].Hash, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Limit_ALockThatTimesOut_HoldsTheDeliveryForTheIntervalWithoutCountingAnAttempt()
    {
        await using DispatchEngine engine = await StartAsync(
            category => category.DeploymentLimit(Channel.Sms, 3, TimeSpan.FromHours(1)),
            configuration => configuration["Hermes:Dispatch:Timeout"] = "00:00:01");
        SendReceipt receipt = (await engine.Host.SendAsync(ReminderTo(Number))).Value!;
        await using NpgsqlConnection holder = new(engine.Host.Connection);
        await holder.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlTransaction transaction = await holder.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await using (NpgsqlCommand command = new("SELECT pg_advisory_xact_lock(@key)", holder, transaction))
        {
            command.Parameters.AddWithValue("key", Places.Key("sms", Reminders, 3_600, hash: null));
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await engine.Host.DispatchAsync();

        DeliveryRecord delivery = Assert.Single((await engine.Host.ReadAsync(receipt.Send)).Deliveries);
        Assert.Equal(DeliveryStatus.Waiting, delivery.Status);
        Assert.Empty(delivery.Attempts);
        Assert.Equal(EngineHost.Start + TimeSpan.FromSeconds(5), await engine.Host.ColumnAsync<DateTime>(delivery.Delivery.Value, "due_at"));
        Assert.Empty(engine.Sms.Received);
        Assert.Empty(await PlacesAsync(engine.Host, delivery.Delivery.Value));
    }

    [Fact]
    public async Task Limit_AnEssentialCategorysLimit_EndsItsDeliveryLimitedToo()
    {
        await using DispatchEngine engine = await DispatchEngine.StartAsync(postgres, declare: declare =>
        {
            declare.Category(EngineHost.Category).Essential().Limit(Channel.Sms, 1, TimeSpan.FromDays(1));
            declare.Kind(EngineHost.Kind, EngineHost.Category).Email().Sms().Push();
        });
        SendReceipt first = (await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]))).Value!;
        await engine.Host.DispatchAsync();
        SendReceipt second = (await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]))).Value!;
        await engine.Host.DispatchAsync();

        Assert.Equal(DeliveryStatus.Sent, await SuppressionTests.StatusAsync(engine.Host, first));
        Assert.Equal(DeliveryStatus.Limited, await SuppressionTests.StatusAsync(engine.Host, second));
    }

    /// <summary>An essential category of every channel, and the optional category <c>reminders</c> with an SMS kind and the limits <paramref name="limits"/> declares.</summary>
    internal static Action<DeclarationBuilder> Declare(Action<CategoryBuilder> limits)
    {
        return declare =>
        {
            EngineHost.Essential(declare);
            CategoryBuilder category = declare.Category(Reminders);
            limits(category);
            declare.Kind(Reminder, Reminders).Sms();
        };
    }

    internal static Send ReminderTo(string number)
    {
        return SendTests.Code(phone: number, channels: [Channel.Sms]) with { Kind = Reminder };
    }

    private static void Configure(Dictionary<string, string?> configuration, int writeVersion)
    {
        configuration["Hermes:Dispatch:BatchSize"] = "1";
        configuration["Hermes:Secrets:HashKeys:WriteVersion"] = writeVersion.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<SendReceipt> DispatchedAsync(DispatchEngine engine)
    {
        SendReceipt receipt = (await engine.Host.SendAsync(ReminderTo(Number))).Value!;
        await engine.Host.DispatchAsync();
        return receipt;
    }

    private static async Task<IReadOnlyList<(string Kind, int Window)>> PlacesAsync(EngineHost host, Guid delivery)
    {
        await using NpgsqlConnection database = new(host.Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("SELECT kind, window_seconds FROM messaging.places WHERE delivery_id = @id ORDER BY kind, window_seconds", database);
        command.Parameters.AddWithValue("id", delivery);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        List<(string, int)> places = [];
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            places.Add((reader.GetString(0), reader.GetInt32(1)));
        }

        return places;
    }

    private Task<DispatchEngine> StartAsync(Action<CategoryBuilder> limits, Action<Dictionary<string, string?>>? configure = null)
    {
        return DispatchEngine.StartAsync(postgres, configure: configure, declare: Declare(limits));
    }
}