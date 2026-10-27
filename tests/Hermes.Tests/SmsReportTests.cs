using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "integration")]
public sealed class SmsReportTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    internal const string SourceHeader = "X-Source";

    internal static readonly string Key = Base64Url.EncodeToString(Secrets.Bytes(16, 5));

    private const string Number = "+201000000001";

    private const string Here = "127.0.0.1";

    private const string Elsewhere = "127.0.0.2";

    [Fact]
    public async Task MSG_SMS_004_AC1_AWrongKeyAndAGuessedDeliveryReferenceChangeNothingAndAreCountedAndAnUnknownProviderReferenceIsHeldUncounted()
    {
        await using DispatchEngine engine = await StartAsync();
        using HttpClient client = engine.Host.Client();
        SendReceipt receipt = await SentAsync(engine);

        using HttpResponseMessage wrong = await ReportAsync(client, Fields(engine.Sms.Received[0].DeliveryReference, "delivered"), key: Base64Url.EncodeToString(Secrets.Bytes(16, 6)));
        using HttpResponseMessage guessed = await ReportAsync(client, Fields("guessed-reference", "delivered"));
        long counted = await RejectedAsync(engine.Host);
        using HttpResponseMessage held = await ReportAsync(client, [new("pid", "wx-unknown"), new("status", "delivered")]);

        Assert.Equal(HttpStatusCode.NotFound, wrong.StatusCode);
        Assert.Empty(await wrong.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.OK, guessed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, held.StatusCode);
        Assert.Equal(DeliveryStatus.Sent, await SuppressionTests.StatusAsync(engine.Host, receipt));
        Assert.Equal(2L, counted);
        Assert.Equal(2L, await RejectedAsync(engine.Host));
        Assert.Equal(1L, await HeldAsync(engine.Host));
        Assert.Equal(2, engine.Sms.Reports.Count);
    }

    [Fact]
    public async Task MSG_SMS_004_AC1_AHeldReportIsDroppedByTheWatchJobOnceItLapsesLoggedAndUncounted()
    {
        using LogCapture logs = new();
        await using DispatchEngine engine = await StartAsync(configuration => configuration["Hermes:Jobs:Enabled"] = "true", logs: logs);
        using HttpClient client = engine.Host.Client();
        using HttpResponseMessage held = await ReportAsync(client, [new("pid", "wx-lapsed"), new("status", "delivered")]);

        await Watching.RunAsync(engine.Host);
        (long Rejected, long Held) kept = await CountsAsync(engine.Host);
        await LapseHeldAsync(engine.Host);
        await Watching.RunAsync(engine.Host);

        Assert.Equal(HttpStatusCode.OK, held.StatusCode);
        Assert.Equal((0L, 1L), kept);
        Assert.Equal((0L, 0L), await CountsAsync(engine.Host));
        Assert.Single(logs.Entries, entry => entry.Message.Contains("held past Hermes:Sms:ReportHold", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MSG_SMS_004_AC2_AReportOfDeliveryMovesASentDeliveryToDeliveredAFinalFailureToFailedAndAnIntermediateStateChangesNothingUncounted()
    {
        await using DispatchEngine engine = await StartAsync();
        using HttpClient client = engine.Host.Client();
        SendReceipt delivered = await SentAsync(engine);
        SendReceipt failed = await SentAsync(engine, "person-2");
        SendReceipt intermediate = await SentAsync(engine, "person-3");

        using HttpResponseMessage first = await ReportAsync(client, Fields(engine.Sms.Received[0].DeliveryReference, "delivered"));
        using HttpResponseMessage second = await ReportAsync(client, Fields(engine.Sms.Received[1].DeliveryReference, "failed"));
        using HttpResponseMessage third = await Endpoints.SendAsync(client, HttpMethod.Get, Route() + "?ref=" + engine.Sms.Received[2].DeliveryReference + "&status=notfinal");

        Assert.All(new[] { first, second, third }, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.Equal(DeliveryStatus.Delivered, await SuppressionTests.StatusAsync(engine.Host, delivered));
        DeliveryRecord ended = Assert.Single((await engine.Host.ReadAsync(failed.Send)).Deliveries);
        Assert.Equal((DeliveryStatus.Failed, AttemptReason.ReportedFailed), (ended.Status, ended.Reason!.Value));
        Assert.Equal(DeliveryStatus.Sent, await SuppressionTests.StatusAsync(engine.Host, intermediate));
        Assert.Equal(0L, await RejectedAsync(engine.Host));
    }

    [Fact]
    public async Task MSG_SMS_004_AC3_AReportThatArrivesWhileItsDeliveryIsInflightIsAppliedOnceTheAttemptsAnswerIsRecorded()
    {
        await using DispatchEngine engine = await StartAsync();
        using HttpClient client = engine.Host.Client();
        List<HttpStatusCode> answered = [];
        engine.Sms.Before = async (message, _) =>
        {
            using HttpResponseMessage response = await ReportAsync(client, Fields(message.DeliveryReference, "delivered"));
            answered.Add(response.StatusCode);
        };
        SendReceipt receipt = (await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]))).Value!;

        await engine.Host.DispatchAsync();

        Assert.Equal([HttpStatusCode.OK], answered);
        Assert.Equal(DeliveryStatus.Delivered, await SuppressionTests.StatusAsync(engine.Host, receipt));
        Assert.Equal(0L, await HeldAsync(engine.Host));
    }

    [Fact]
    public async Task MSG_SMS_004_AC3_AReportOfDeliveryForADeliveryWaitingAfterATimedOutAttemptEndsItDeliveredWithNoFurtherAttempt()
    {
        await using DispatchEngine engine = await StartAsync();
        using HttpClient client = engine.Host.Client();
        engine.Sms.Answer = _ => new Attempt.Transient(AttemptReason.Timeout, null, null);
        SendReceipt receipt = (await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]))).Value!;
        await engine.Host.DispatchAsync();
        Assert.Equal(DeliveryStatus.Waiting, await SuppressionTests.StatusAsync(engine.Host, receipt));

        using HttpResponseMessage response = await ReportAsync(client, Fields(engine.Sms.Received[0].DeliveryReference, "delivered"));
        engine.Host.Clock.Advance(TimeSpan.FromHours(1));
        await engine.Host.DispatchAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        DeliveryRecord delivery = Assert.Single((await engine.Host.ReadAsync(receipt.Send)).Deliveries);
        Assert.Equal(DeliveryStatus.Delivered, delivery.Status);
        Assert.Single(delivery.Attempts);
        Assert.Single(engine.Sms.Received);
    }

    [Fact]
    public async Task MSG_SEND_007_AC7_ADeliveredReportOfAClaimlostAttemptArrivingWhileItsDeliveryWaitsEndsItDelivered()
    {
        await using DispatchEngine engine = await StartAsync();
        using HttpClient client = engine.Host.Client();
        SendReceipt receipt = (await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]))).Value!;
        await DispatchTests.LoseAsync(engine.Host, receipt.Deliveries[0].Delivery.Value);
        await engine.Host.DispatchAsync();
        DeliveryRecord waiting = Assert.Single((await engine.Host.ReadAsync(receipt.Send)).Deliveries);

        using HttpResponseMessage response = await ReportAsync(client, Fields("lost-reference", "delivered"));
        engine.Host.Clock.Advance(TimeSpan.FromHours(1));
        await engine.Host.DispatchAsync();

        Assert.Equal((DeliveryStatus.Waiting, AttemptReason.ClaimLost), (waiting.Status, waiting.Reason!.Value));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        DeliveryRecord delivery = Assert.Single((await engine.Host.ReadAsync(receipt.Send)).Deliveries);
        Assert.Equal(DeliveryStatus.Delivered, delivery.Status);
        Assert.Single(delivery.Attempts);
        Assert.Empty(engine.Sms.Received);
    }

    [Fact]
    public async Task MSG_SMS_004_AC4_ALateFailureReportOfAFirstAttemptDoesNotMoveADeliveryWhoseSecondAttemptWasSent()
    {
        await using DispatchEngine engine = await StartAsync();
        using HttpClient client = engine.Host.Client();
        int calls = 0;
        engine.Sms.Answer = _ => ++calls == 1 ? new Attempt.Transient(AttemptReason.Temporary, 503, null) : new Attempt.Accepted(null);
        SendReceipt receipt = (await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]))).Value!;
        await engine.Host.DispatchAsync();
        engine.Host.Clock.Advance(TimeSpan.FromHours(1));
        await engine.Host.DispatchAsync();

        using HttpResponseMessage response = await ReportAsync(client, Fields(engine.Sms.Received[0].DeliveryReference, "failed"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, engine.Sms.Received.Count);
        Assert.Equal(DeliveryStatus.Sent, await SuppressionTests.StatusAsync(engine.Host, receipt));
        Assert.Equal(0L, await RejectedAsync(engine.Host));
    }

    [Fact]
    public async Task MSG_SMS_004_AC5_OverTheWrongKeyRateFromOneSourceARequestIsAnswered429WhileReportsWithTheRightKeyFromThatSourceAreNot()
    {
        await AssertWrongKeyRateAsync();
    }

    [Fact]
    public async Task MSG_API_006_AC3_OverTheWrongKeyRateFromOneSourceARequestIsAnswered429AndAReportWithTheRightKeyFromThatSourceIsNot()
    {
        await AssertWrongKeyRateAsync();
    }

    [Fact]
    public async Task MSG_SMS_004_AC6_WithTheReportBaseAddressSetTheProviderReceivesEachSmsWithItsReportAddressAndWithoutItNone()
    {
        await using DispatchEngine addressed = await StartAsync(configuration => configuration["Hermes:Sms:ReportBaseAddress"] = "https://example.com/notifications/");
        await SentAsync(addressed);
        await using DispatchEngine plain = await StartAsync();
        await SentAsync(plain);

        Assert.Equal(new Uri("https://example.com/notifications/sms/report/" + Key), Assert.Single(addressed.Sms.Received).ReportAddress);
        Assert.Null(Assert.Single(plain.Sms.Received).ReportAddress);
    }

    [Fact]
    public async Task Report_WhoseKeyMatches_RecordsInTheKeyTableWhenItsVersionLastMatched()
    {
        await using DispatchEngine engine = await StartAsync();
        using HttpClient client = engine.Host.Client();
        engine.Host.Clock.Advance(TimeSpan.FromMinutes(3));

        using HttpResponseMessage response = await ReportAsync(client, [new("nonsense", "1")]);

        await using NpgsqlConnection database = new(engine.Host.Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("SELECT last_made_at FROM messaging.key_versions WHERE key = 'smsReportKey' AND version = 1", database);
        Assert.Equal(engine.Host.Clock.GetUtcNow().UtcDateTime, (DateTime)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!);
    }

    [Fact]
    public async Task MSG_SMS_004_AC7_ADeliveredReportOfAFirstAttemptKeptWhileTheSecondAttemptsCheck2EndsTheDeliveryExpiredEndsItDelivered()
    {
        await using DispatchEngine engine = await StartAsync(declare: declare =>
        {
            declare.Category(EngineHost.Category).Essential();
            declare.Kind(EngineHost.Kind, EngineHost.Category).Sms().ExpiresAfter(TimeSpan.FromMinutes(5));
        });
        using HttpClient client = engine.Host.Client();
        engine.Sms.Answer = message => message.ToNumber == Number ? new Attempt.Transient(AttemptReason.Temporary, 503, null) : new Attempt.Accepted(null);
        SendReceipt expiring = (await engine.Host.SendAsync(SendTests.Code(phone: Number, channels: [Channel.Sms]))).Value!;
        await engine.Host.DispatchAsync();
        string first = engine.Sms.Received[0].DeliveryReference;
        engine.Host.Clock.Advance(TimeSpan.FromSeconds(10));
        SendReceipt other = (await engine.Host.SendAsync(SendTests.Code(phone: "+201000000002", recipient: new Recipient.Person("person-2"), channels: [Channel.Sms]))).Value!;
        List<HttpStatusCode> answered = [];
        engine.Sms.Before = async (message, _) =>
        {
            if (message.ToNumber != Number)
            {
                using HttpResponseMessage response = await ReportAsync(client, Fields(first, "delivered"));
                answered.Add(response.StatusCode);
            }
        };
        engine.Host.Clock.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal(2, await engine.Host.DispatchAsync());

        Assert.Equal([HttpStatusCode.OK], answered);
        Assert.Equal(DeliveryStatus.Sent, await SuppressionTests.StatusAsync(engine.Host, other));
        DeliveryRecord delivery = Assert.Single((await engine.Host.ReadAsync(expiring.Send)).Deliveries);
        Assert.Equal(DeliveryStatus.Delivered, delivery.Status);
        Assert.Equal(1, engine.Sms.Received.Count(message => message.ToNumber == Number));
    }

    [Fact]
    public async Task MSG_SMS_005_AC1_AfterAFailureReportAFurtherDeliveryToTheNumberFitsUnderTheLimitTheFailedOneHadUsedAndTheDeploymentsPlaceStaysTaken()
    {
        await using DispatchEngine engine = await StartAsync(declare: LimitTests.Declare(category => category.Limit(Channel.Sms, 1, TimeSpan.FromDays(1)).DeploymentLimit(Channel.Sms, 2, TimeSpan.FromHours(1))));
        using HttpClient client = engine.Host.Client();
        SendReceipt failed = (await engine.Host.SendAsync(LimitTests.ReminderTo(Number))).Value!;
        await engine.Host.DispatchAsync();

        using HttpResponseMessage response = await ReportAsync(client, Fields(engine.Sms.Received[0].DeliveryReference, "failed"));
        SendReceipt again = (await engine.Host.SendAsync(LimitTests.ReminderTo(Number))).Value!;
        await engine.Host.DispatchAsync();
        SendReceipt capped = (await engine.Host.SendAsync(LimitTests.ReminderTo("+201000000002"))).Value!;
        await engine.Host.DispatchAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(DeliveryStatus.Failed, await SuppressionTests.StatusAsync(engine.Host, failed));
        Assert.Equal(DeliveryStatus.Sent, await SuppressionTests.StatusAsync(engine.Host, again));
        Assert.Equal(DeliveryStatus.Limited, await SuppressionTests.StatusAsync(engine.Host, capped));
    }

    [Fact]
    public async Task MSG_API_006_AC1_AReportOfDeliveryForASentSmsMovesItToDeliveredAndIsAnsweredAsTheProviderExpects()
    {
        await using DispatchEngine engine = await StartAsync();
        using HttpClient client = engine.Host.Client();
        SendReceipt receipt = await SentAsync(engine);

        using HttpResponseMessage response = await ReportAsync(client, Fields(engine.Sms.Received[0].DeliveryReference, "delivered"));

        await AssertExpectedAsync(response);
        Assert.Equal(DeliveryStatus.Delivered, await SuppressionTests.StatusAsync(engine.Host, receipt));
    }

    [Fact]
    public async Task MSG_API_006_AC2_AForgedReportWithTheRightKeyIsAnsweredAsARealOneIsAndChangesNothing()
    {
        await using DispatchEngine engine = await StartAsync();
        using HttpClient client = engine.Host.Client();
        SendReceipt receipt = await SentAsync(engine);

        using HttpResponseMessage forged = await ReportAsync(client, Fields("forged-reference", "failed"));
        using HttpResponseMessage unread = await ReportAsync(client, [new("nonsense", "1")]);

        await AssertExpectedAsync(forged);
        await AssertExpectedAsync(unread);
        Assert.Equal(DeliveryStatus.Sent, await SuppressionTests.StatusAsync(engine.Host, receipt));
        Assert.Equal(2L, await RejectedAsync(engine.Host));
    }

    [Fact]
    public async Task MSG_API_006_AC4_ABodyOver16KiBIsAnswered413AndPassedToNoProvider()
    {
        await using DispatchEngine engine = await StartAsync();
        using HttpClient client = engine.Host.Client();

        using HttpResponseMessage response = await ReportAsync(client, [new("pad", new string('a', 16 * 1024))]);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("messaging.request.toolarge", (await PreferenceEndpointTests.RefusalAsync(response)).Code);
        Assert.Empty(engine.Sms.Reports);
    }

    [Fact]
    public async Task Report_NamingAProviderReferenceNoDeliveryHoldsYet_IsHeldAndAppliedWhenTheAnswerRecordsThatReference()
    {
        await using DispatchEngine engine = await StartAsync();
        using HttpClient client = engine.Host.Client();
        engine.Sms.Answer = _ => new Attempt.Accepted("wx-1");
        engine.Sms.Before = async (_, _) =>
        {
            using HttpResponseMessage response = await ReportAsync(client, [new("pid", "wx-1"), new("status", "delivered")]);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        };
        SendReceipt receipt = (await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]))).Value!;

        await engine.Host.DispatchAsync();

        Assert.Equal(DeliveryStatus.Delivered, await SuppressionTests.StatusAsync(engine.Host, receipt));
        Assert.Equal(0L, await HeldAsync(engine.Host));
        Assert.Equal(0L, await RejectedAsync(engine.Host));
    }

    [Fact]
    public async Task Report_HeldPastTheHoldWhenTheAnswerRecordsItsReference_IsNotApplied()
    {
        await using DispatchEngine engine = await StartAsync();
        using HttpClient client = engine.Host.Client();
        engine.Sms.Answer = _ => new Attempt.Accepted("wx-1");
        using HttpResponseMessage held = await ReportAsync(client, [new("pid", "wx-1"), new("status", "delivered")]);
        engine.Host.Clock.Advance(TimeSpan.FromMinutes(10));
        SendReceipt receipt = (await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]))).Value!;

        await engine.Host.DispatchAsync();

        Assert.Equal(HttpStatusCode.OK, held.StatusCode);
        Assert.Equal(DeliveryStatus.Sent, await SuppressionTests.StatusAsync(engine.Host, receipt));
        Assert.Equal((0L, 1L), await CountsAsync(engine.Host));
    }

    [Fact]
    public async Task Report_OverTheHoldCap_IsDroppedAtOnceAndCountedAsRejected()
    {
        await using DispatchEngine engine = await StartAsync(configuration => configuration["Hermes:Sms:ReportHoldMax"] = "10");
        using HttpClient client = engine.Host.Client();

        for (int report = 0; report <= 10; report++)
        {
            using HttpResponseMessage response = await ReportAsync(client, [new("pid", "wx-" + report), new("status", "delivered")]);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        Assert.Equal(10L, await HeldAsync(engine.Host));
        Assert.Equal(1L, await RejectedAsync(engine.Host));
    }

    [Fact]
    public async Task Report_RejectedPastTheThresholdWithinTheWindow_RaisesSmsReportRejected()
    {
        AlertSinkFake alerts = new();
        await using DispatchEngine engine = await StartAsync(
            configuration => configuration["Hermes:Sms:ReportRejectThreshold"] = "2",
            services: services => services.AddSingleton<IAlertSink>(alerts));
        using HttpClient client = engine.Host.Client();
        string wrong = Base64Url.EncodeToString(Secrets.Bytes(16, 6));

        for (int report = 0; report < 2; report++)
        {
            using HttpResponseMessage response = await ReportAsync(client, [], key: wrong);
        }

        bool raisedAtTwo = alerts.Calls.Any(call => call.Raised && call.Condition == "sms-report-rejected");
        engine.Host.Clock.Advance(TimeSpan.FromMinutes(1));
        using HttpResponseMessage third = await ReportAsync(client, [new("nonsense", "1")]);

        Assert.False(raisedAtTwo);
        AlertSinkFake.AlertCall raised = Assert.Single(alerts.Calls, call => call.Raised && call.Condition == "sms-report-rejected");
        Assert.Equal("3", raised.Details!["count"]);
        Assert.Equal(3L, await RejectedAsync(engine.Host));
    }

    /// <summary>The report route with <paramref name="key"/>, the held key unless given.</summary>
    internal static string Route(string? key = null)
    {
        return "/sms/report/" + (key ?? Key);
    }

    /// <summary>Maps the report group under the prefix, after a step that takes a request's source address from its <c>X-Source</c> header, as a host's forwarded-headers setup would.</summary>
    internal static void Map(WebApplication application)
    {
        application.Use((context, next) =>
        {
            if (context.Request.Headers.TryGetValue(SourceHeader, out Microsoft.Extensions.Primitives.StringValues source))
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse(source.ToString());
            }

            return next(context);
        });
        application.MapHermesSmsReports(Endpoints.Prefix);
    }

    private static List<KeyValuePair<string, string>> Fields(string reference, string status)
    {
        return [new("ref", reference), new("status", status)];
    }

    private static async Task<HttpResponseMessage> ReportAsync(HttpClient client, IEnumerable<KeyValuePair<string, string>> fields, string source = Here, string? key = null)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, Endpoints.Prefix + Route(key)) { Content = new FormUrlEncodedContent(fields) };
        request.Headers.Add(SourceHeader, source);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task AssertExpectedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("OK", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private static async Task<SendReceipt> SentAsync(DispatchEngine engine, string person = "person-1")
    {
        SendReceipt receipt = (await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms], recipient: new Recipient.Person(person)))).Value!;
        await engine.Host.DispatchAsync();
        Assert.Equal(DeliveryStatus.Sent, await SuppressionTests.StatusAsync(engine.Host, receipt));
        return receipt;
    }

    /// <summary>Moves the held reports' lapse to the host's now, as <c>Hermes:Sms:ReportHold</c> passing would.</summary>
    private static async Task LapseHeldAsync(EngineHost host)
    {
        await using NpgsqlConnection database = new(host.Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("UPDATE messaging.held_reports SET lapses_at = @now", database);
        command.Parameters.AddWithValue("now", host.Clock.GetUtcNow());
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<long> RejectedAsync(EngineHost host)
    {
        return (await CountsAsync(host)).Rejected;
    }

    private static async Task<long> HeldAsync(EngineHost host)
    {
        return (await CountsAsync(host)).Held;
    }

    private static async Task<(long Rejected, long Held)> CountsAsync(EngineHost host)
    {
        const string Counts = "SELECT (SELECT coalesce(sum(count), 0)::bigint FROM messaging.rejected_reports), (SELECT count(*) FROM messaging.held_reports)";
        await using NpgsqlConnection database = new(host.Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new(Counts, database);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        return (reader.GetInt64(0), reader.GetInt64(1));
    }

    private async Task AssertWrongKeyRateAsync()
    {
        await using DispatchEngine engine = await StartAsync(configuration => configuration["Hermes:Sms:WrongKeyRate"] = "2");
        using HttpClient client = engine.Host.Client();
        SendReceipt receipt = await SentAsync(engine);
        string wrong = Base64Url.EncodeToString(Secrets.Bytes(16, 6));
        List<HttpStatusCode> answers = [];

        for (int request = 0; request < 3; request++)
        {
            using HttpResponseMessage response = await ReportAsync(client, [], key: wrong);
            answers.Add(response.StatusCode);
            if (request == 2)
            {
                Assert.Equal("messaging.request.ratelimited", (await PreferenceEndpointTests.RefusalAsync(response)).Code);
                Assert.NotNull(response.Headers.RetryAfter);
            }
        }

        using HttpResponseMessage right = await ReportAsync(client, Fields(engine.Sms.Received[0].DeliveryReference, "delivered"));
        using HttpResponseMessage elsewhere = await ReportAsync(client, [], source: Elsewhere, key: wrong);
        engine.Host.Clock.Advance(TimeSpan.FromMinutes(1));
        using HttpResponseMessage later = await ReportAsync(client, [], key: wrong);

        Assert.Equal([HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.TooManyRequests], answers);
        Assert.Equal(HttpStatusCode.OK, right.StatusCode);
        Assert.Equal(DeliveryStatus.Delivered, await SuppressionTests.StatusAsync(engine.Host, receipt));
        Assert.Equal(HttpStatusCode.NotFound, elsewhere.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, later.StatusCode);
    }

    private async Task<DispatchEngine> StartAsync(
        Action<Dictionary<string, string?>>? configure = null,
        Action<DeclarationBuilder>? declare = null,
        Action<IServiceCollection>? services = null,
        LogCapture? logs = null)
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        return await DispatchEngine.StartAsync(
            postgres,
            configure: configure,
            declare: declare,
            connection: connection,
            logs: logs,
            services: collection =>
            {
                collection.AddSingleton<ISecretSource>(Secrets.Held(connection).With("smsReportKey", Secrets.Bytes(16, 5)));
                services?.Invoke(collection);
            },
            map: Map);
    }
}