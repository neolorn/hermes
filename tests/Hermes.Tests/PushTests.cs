using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

using Npgsql;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "integration")]
public sealed class PushTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Endpoint = "https://push.example.com/send/first";

    private const string OtherEndpoint = "https://push.example.com/send/second";

    private const string Person = "person-1";

    private const string Other = "person-2";

    [Fact]
    public async Task MSG_PUSH_003_AC1_ARegisteredSubscriptionReceivesThePersonsNextPushAndARemovedOneDoesNot()
    {
        await using DispatchEngine engine = await StartAsync();
        Pusher push = new(engine);
        Assert.Equal(HttpStatusCode.NoContent, await push.SubscribeAsync(Person, Endpoint));

        SendReceipt received = await push.PushedAsync(Person);
        using HttpResponseMessage removed = await push.SendAsync(HttpMethod.Delete, "/push/subscriptions", Person, $$"""{"endpoint":"{{Endpoint}}"}""");
        SendReceipt after = await push.PushedAsync(Person);

        Assert.Equal(DeliveryStatus.Sent, await SuppressionTests.StatusAsync(push.Host, received));
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(DeliveryStatus.NoSubscription, await SuppressionTests.StatusAsync(push.Host, after));
        Assert.Equal(Endpoint, Assert.Single(push.Provider.Received).Endpoint);
    }

    [Fact]
    public async Task MSG_SEND_003_AC3_ASendToAPersonWithASubscriptionAndNoChannelNamedIsSentOnEveryChannelPushIncluded()
    {
        await using DispatchEngine engine = await StartAsync();
        Pusher push = new(engine);
        Assert.Equal(HttpStatusCode.NoContent, await push.SubscribeAsync(Person, Endpoint));

        SendReceipt receipt = (await engine.Host.SendAsync(SendTests.Code(recipient: new Recipient.Person(Person)))).Value!;
        await engine.Host.DispatchAsync();

        SendRecord record = await engine.Host.ReadAsync(receipt.Send);
        Assert.Equal([Channel.Email, Channel.Sms, Channel.Push], receipt.Deliveries.Select(delivery => delivery.Channel));
        Assert.Equal(3, record.Deliveries.Count);
        Assert.All(record.Deliveries, delivery => Assert.Equal(DeliveryStatus.Sent, delivery.Status));
        Assert.Equal(Endpoint, Assert.Single(push.Provider.Received).Endpoint);
    }

    [Fact]
    public async Task MSG_OPS_004_AC1_WithHostedOffAPassRunOnDemandDeliversPushThroughTheShippedProvider()
    {
        using PushServiceHandler service = new();
        await using DispatchEngine engine = await StartAsync(
            configure: configuration => configuration["Hermes:Dispatch:Hosted"] = "false",
            services: collection => collection.PostConfigure<HttpClientFactoryOptions>("Hermes.Push", options => options.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = service)),
            shipped: true);
        Pusher push = new(engine);
        Assert.Equal(HttpStatusCode.NoContent, await push.SubscribeAsync(Person, Endpoint));
        SendReceipt receipt = (await engine.Host.SendAsync(Code(Person))).Value!;

        int before = service.Requests.Count;
        await engine.Host.DispatchAsync();

        Assert.Equal(DeliveryStatus.Sent, await SuppressionTests.StatusAsync(engine.Host, receipt));
        Assert.Equal(0, before);
        (Uri? address, string? encoding) = Assert.Single(service.Requests);
        Assert.Equal(new Uri(Endpoint), address);
        Assert.Equal("aes128gcm", encoding);
    }

    [Fact]
    public async Task MSG_API_004_AC1_ASubscriptionPostedByASignedInPersonReceivesThatPersonsPushDeliveries()
    {
        await using DispatchEngine engine = await StartAsync();
        Pusher push = new(engine);
        (string p256dh, string auth) = PushOutcomeTests.Subscriber();

        Assert.Equal(HttpStatusCode.NoContent, await push.SubscribeAsync(Person, Endpoint, p256dh, auth));
        SendReceipt receipt = await push.PushedAsync(Person);

        Assert.Equal(DeliveryStatus.Sent, await SuppressionTests.StatusAsync(push.Host, receipt));
        PushRequest.WebPush request = Assert.Single(push.Provider.Received);
        Assert.Equal((Endpoint, p256dh, auth, 1), (request.Endpoint, request.P256dh, request.Auth, request.KeyVersion));
        using JsonDocument payload = JsonDocument.Parse(request.Payload);
        Assert.Equal("Title en", payload.RootElement.GetProperty("title").GetString());
        Assert.Equal("Body en 123456", payload.RootElement.GetProperty("body").GetString());
    }

    [Fact]
    public async Task MSG_API_004_AC2_ASubscriptionDeletedReceivesNothingMoreAndADeleteOfAnotherPersonsLeavesItAsItIs()
    {
        await using DispatchEngine engine = await StartAsync();
        Pusher push = new(engine);
        await push.SubscribeAsync(Person, Endpoint);

        using HttpResponseMessage others = await push.SendAsync(HttpMethod.Delete, "/push/subscriptions", Other, $$"""{"endpoint":"{{Endpoint}}"}""");
        long kept = await push.SubscriptionsAsync();
        using HttpResponseMessage own = await push.SendAsync(HttpMethod.Delete, "/push/subscriptions", Person, $$"""{"endpoint":"{{Endpoint}}"}""");
        SendReceipt receipt = await push.PushedAsync(Person);

        Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.NoContent), (others.StatusCode, own.StatusCode));
        Assert.Equal(1L, kept);
        Assert.Equal(DeliveryStatus.NoSubscription, await SuppressionTests.StatusAsync(push.Host, receipt));
        Assert.Empty(push.Provider.Received);
    }

    [Fact]
    public async Task MSG_PUSH_003_AC2_ASubscriptionRegisteredAfterASendReceivesItsPushAndOneOfAnotherPersonNeverDoes()
    {
        await using DispatchEngine engine = await StartAsync();
        Pusher push = new(engine);
        SendReceipt receipt = (await push.Host.SendAsync(Code(Person))).Value!;

        await push.SubscribeAsync(Person, Endpoint);
        await push.SubscribeAsync(Other, OtherEndpoint);
        await push.Host.DispatchAsync();

        Assert.Equal(DeliveryStatus.Sent, await SuppressionTests.StatusAsync(push.Host, receipt));
        Assert.Equal(Endpoint, Assert.Single(push.Provider.Received).Endpoint);
    }

    [Fact]
    public async Task MSG_PUSH_003_AC3_TheEndpointAndKeysAreNotStoredInPlainTextAndAnEndpointMovedToAnotherPersonOpensOnlyForThatPerson()
    {
        await using DispatchEngine engine = await StartAsync();
        Pusher push = new(engine);
        (string p256dh, string auth) = PushOutcomeTests.Subscriber();
        await push.SubscribeAsync(Person, Endpoint, p256dh, auth);

        Assert.Equal(HttpStatusCode.NoContent, await push.SubscribeAsync(Other, Endpoint, p256dh, auth));
        SendReceipt first = await push.PushedAsync(Person);
        SendReceipt moved = await push.PushedAsync(Other);

        await using NpgsqlConnection database = new(push.Host.Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("SELECT person, endpoint, keys FROM messaging.subscriptions", database);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal(Other, reader.GetString(0));
        string stored = Convert.ToHexString((byte[])reader[1]) + Convert.ToHexString((byte[])reader[2]);
        Assert.DoesNotContain(Convert.ToHexString(Encoding.UTF8.GetBytes("push.example.com")), stored, StringComparison.Ordinal);
        Assert.DoesNotContain(Convert.ToHexString(Base64Url.DecodeFromChars(p256dh))[..32], stored, StringComparison.Ordinal);
        Assert.DoesNotContain(Convert.ToHexString(Base64Url.DecodeFromChars(auth)), stored, StringComparison.Ordinal);
        Assert.False(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal(DeliveryStatus.NoSubscription, await SuppressionTests.StatusAsync(push.Host, first));
        Assert.Equal(DeliveryStatus.Sent, await SuppressionTests.StatusAsync(push.Host, moved));
    }

    [Fact]
    public async Task Register_PastTheMostSubscriptions_RemovesTheLeastRecentlyUsedNeverTheOneJustRegistered()
    {
        await using DispatchEngine engine = await StartAsync(configuration => configuration["Hermes:Push:MaxSubscriptions"] = "2");
        Pusher push = new(engine);
        await push.SubscribeAsync(Person, "https://push.example.com/send/a");
        push.Host.Clock.Advance(TimeSpan.FromMinutes(1));
        await push.SubscribeAsync(Person, "https://push.example.com/send/b");
        push.Host.Clock.Advance(TimeSpan.FromMinutes(1));
        push.Provider.Answer = request => request.Endpoint.EndsWith("/a", StringComparison.Ordinal) ? new Attempt.Accepted(null) : new Attempt.Permanent(AttemptReason.BadRequest, 400);
        await push.PushedAsync(Person);
        push.Host.Clock.Advance(TimeSpan.FromMinutes(1));
        await push.SubscribeAsync(Person, "https://push.example.com/send/c");
        push.Provider.Answer = _ => new Attempt.Accepted(null);
        int before = push.Provider.Received.Count;

        await push.PushedAsync(Person);

        Assert.Equal(2, before);
        Assert.Equal(["https://push.example.com/send/a", "https://push.example.com/send/c"], push.Provider.Received.Skip(before).Select(request => request.Endpoint).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("https://other.example.com/send/first")]
    [InlineData("https://127.0.0.1/send/first")]
    [InlineData("https://push.example.com:8443/send/first")]
    [InlineData("http://push.example.com/send/first")]
    [InlineData("https://someone@push.example.com/send/first")]
    public async Task MSG_PUSH_004_AC1_AnEndpointAtAnotherHostAnIpAnotherPortOverHttpOrWithUserInformationIsRefusedAndStoredNowhere(string endpoint)
    {
        await using DispatchEngine engine = await StartAsync();
        Pusher push = new(engine);

        using HttpResponseMessage response = await push.PostAsync(Person, Body(endpoint, PushOutcomeTests.Subscriber(), await push.KeyAsync()));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("messaging.push.endpointnotallowed", (await PreferenceEndpointTests.RefusalAsync(response)).Code);
        Assert.Equal(0L, await push.SubscriptionsAsync());
    }

    [Fact]
    public async Task MSG_PUSH_004_AC2_ASubscriptionWhoseHostIsRemovedFromTheAllowedHostsReceivesNothingWithoutARestart()
    {
        await using DispatchEngine engine = await StartAsync();
        Pusher push = new(engine);
        await push.SubscribeAsync(Person, Endpoint);
        IConfigurationRoot root = (IConfigurationRoot)push.Host.Services.GetRequiredService<IConfiguration>();

        root["Hermes:Push:AllowedHosts:0"] = "other.example.com";
        root.Reload();
        SendReceipt receipt = await push.PushedAsync(Person);

        Assert.Equal(DeliveryStatus.NoSubscription, await SuppressionTests.StatusAsync(push.Host, receipt));
        Assert.Empty(push.Provider.Received);
    }

    [Fact]
    public async Task MSG_PUSH_004_KeysOutsideTheRuleAreRefusedAsMalformed()
    {
        await using DispatchEngine engine = await StartAsync();
        Pusher push = new(engine);

        using HttpResponseMessage response = await push.PostAsync(Person, Body(Endpoint, (Base64Url.EncodeToString(new byte[65]), Base64Url.EncodeToString(new byte[16])), await push.KeyAsync()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(("messaging.request.malformed", "keys"), await PreferenceEndpointTests.RefusalAsync(response, "member"));
        Assert.Equal(0L, await push.SubscriptionsAsync());
    }

    [Fact]
    public async Task MSG_PUSH_005_AC1_A410RemovesTheSubscriptionAndThePersonsNextPushDoesNotUseIt()
    {
        await using DispatchEngine engine = await StartAsync();
        Pusher push = new(engine);
        await push.SubscribeAsync(Person, Endpoint);
        push.Provider.Answer = _ => new Attempt.Gone(AttemptReason.Gone, 410);

        SendReceipt gone = await push.PushedAsync(Person);
        SendReceipt next = await push.PushedAsync(Person);

        Assert.Equal(DeliveryStatus.NoSubscription, await SuppressionTests.StatusAsync(push.Host, gone));
        Assert.Equal(DeliveryStatus.NoSubscription, await SuppressionTests.StatusAsync(push.Host, next));
        Assert.Single(push.Provider.Received);
        Assert.Equal(0L, await push.SubscriptionsAsync());
        CallRecord call = Assert.Single(Assert.Single(Assert.Single((await push.Host.ReadAsync(gone.Send)).Deliveries).Attempts).Calls);
        Assert.Equal((AttemptClass.Gone, (int?)410), (call.AnswerClass, call.Status));
    }

    [Fact]
    public async Task MSG_PUSH_005_AC2_A403LeavesTheDeliveryWaitingAnd21AuthFailuresFromOneServiceWithinAnHourRaiseTheAlertAnd20DoNot()
    {
        AlertSinkFake alerts = new();
        await using DispatchEngine engine = await StartAsync(services: services => services.AddSingleton<IAlertSink>(alerts));
        Pusher push = new(engine);
        await push.SubscribeAsync(Person, Endpoint);
        int calls = 0;
        push.Provider.Answer = _ => (++calls % 2) switch
        {
            1 => (Attempt)new Attempt.Transient(AttemptReason.AuthFailed, 403, null),
            _ => new Attempt.Transient(AttemptReason.AuthFailed, 401, null),
        };
        SendReceipt waiting = await push.PushedAsync(Person);

        for (int send = 1; send < 20; send++)
        {
            await push.PushedAsync(Person);
        }

        bool raisedAt20 = alerts.Calls.Any(call => call.Raised && call.Condition == "push-auth-failing");
        await push.PushedAsync(Person);

        Assert.Equal(DeliveryStatus.Waiting, await SuppressionTests.StatusAsync(push.Host, waiting));
        Assert.False(raisedAt20);
        AlertSinkFake.AlertCall raised = Assert.Single(alerts.Calls, call => call.Raised && call.Condition == "push-auth-failing");
        Assert.Equal(("other", "21"), (raised.Scope, raised.Details!["count"]));
    }

    [Fact]
    public async Task MSG_PUSH_005_AC3_TwoSubscriptionsAnswering201And503EndTheDeliverySentAfterOneAttempt()
    {
        await using DispatchEngine engine = await StartAsync();
        Pusher push = new(engine);
        await push.SubscribeAsync(Person, Endpoint);
        await push.SubscribeAsync(Person, OtherEndpoint);
        push.Provider.Answer = request => request.Endpoint == Endpoint ? new Attempt.Accepted(null) : new Attempt.Transient(AttemptReason.Temporary, 503, null);

        SendReceipt receipt = await push.PushedAsync(Person);
        push.Host.Clock.Advance(TimeSpan.FromHours(1));
        await push.Host.DispatchAsync();

        DeliveryRecord delivery = Assert.Single((await push.Host.ReadAsync(receipt.Send)).Deliveries);
        Assert.Equal(DeliveryStatus.Sent, delivery.Status);
        Assert.Equal(2, Assert.Single(delivery.Attempts).Calls.Count);
        Assert.Equal(2, push.Provider.Received.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MSG_PUSH_005_AC3_Answering503And410TheNextAttemptGoesToTheFirstAloneAndToNoneIfItMovedToAnotherPerson(bool moved)
    {
        await using DispatchEngine engine = await StartAsync();
        Pusher push = new(engine);
        (string, string) keys = PushOutcomeTests.Subscriber();
        await push.SubscribeAsync(Person, Endpoint, keys.Item1, keys.Item2);
        await push.SubscribeAsync(Person, OtherEndpoint);
        push.Provider.Answer = request => request.Endpoint == Endpoint ? new Attempt.Transient(AttemptReason.Temporary, 503, null) : new Attempt.Gone(AttemptReason.Gone, 410);
        SendReceipt receipt = await push.PushedAsync(Person);
        DeliveryStatus between = await SuppressionTests.StatusAsync(push.Host, receipt);
        if (moved)
        {
            await push.SubscribeAsync(Other, Endpoint, keys.Item1, keys.Item2);
        }

        await push.SubscribeAsync(Person, "https://push.example.com/send/third");
        push.Provider.Answer = _ => new Attempt.Accepted(null);
        push.Host.Clock.Advance(TimeSpan.FromHours(1));
        await push.Host.DispatchAsync();

        Assert.Equal(DeliveryStatus.Waiting, between);
        Assert.Equal(moved ? DeliveryStatus.NoSubscription : DeliveryStatus.Sent, await SuppressionTests.StatusAsync(push.Host, receipt));
        Assert.Equal(moved ? [] : [Endpoint], push.Provider.Received.Skip(2).Select(request => request.Endpoint));
    }

    [Fact]
    public async Task MSG_SEND_007_AC8_APushDeliveryWhosePassIsStoppedDuringTheFirstOfItsTwoCallsMakesNoSecondCallAndItsAttemptIsRecordedClaimlost()
    {
        await using DispatchEngine engine = await StartAsync();
        Pusher push = new(engine);
        await push.SubscribeAsync(Person, Endpoint);
        await push.SubscribeAsync(Person, OtherEndpoint);
        TaskCompletionSource calling = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        push.Provider.Answer = _ => new Attempt.Transient(AttemptReason.Temporary, 503, null);
        push.Provider.Before = async (_, _) =>
        {
            calling.TrySetResult();
            await release.Task;
        };
        SendReceipt receipt = (await push.Host.SendAsync(Code(Person))).Value!;
        using System.Threading.CancellationTokenSource stop = new();

        Task<int> pass = push.Host.DispatchAsync(stop.Token);
        await calling.Task;
        await stop.CancelAsync();
        release.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pass);
        DeliveryRecord stopped = Assert.Single((await push.Host.ReadAsync(receipt.Send)).Deliveries);
        int calls = push.Provider.Received.Count;
        push.Provider.Before = null;
        push.Provider.Answer = _ => new Attempt.Accepted(null);
        push.Host.Clock.Advance(TimeSpan.FromHours(1));
        await push.Host.DispatchAsync();

        Assert.Equal(1, calls);
        Assert.Equal(DeliveryStatus.Waiting, stopped.Status);
        AttemptRecord attempt = Assert.Single(stopped.Attempts);
        Assert.Equal(((AttemptClass?)AttemptClass.Transient, (AttemptReason?)AttemptReason.ClaimLost), (attempt.AnswerClass, attempt.Reason));
        Assert.Equal(DeliveryStatus.Sent, await SuppressionTests.StatusAsync(push.Host, receipt));
        Assert.Equal([Endpoint, OtherEndpoint], push.Provider.Received.Skip(1).Select(request => request.Endpoint).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task MSG_PUSH_006_AC1_ASubscriptionWhoseExpirationTimeHasPassedReceivesNothingAndIsGoneAfterTheSubscriptionJobsNextRun()
    {
        await using DispatchEngine engine = await StartAsync(configuration =>
        {
            configuration["Hermes:Jobs:Enabled"] = "true";
            configuration["Hermes:Jobs:SubscriptionsInterval"] = "00:01:00";
        });
        Pusher push = new(engine);
        long expires = EngineHost.Start.AddSeconds(30).ToUnixTimeMilliseconds();
        using HttpResponseMessage posted = await push.PostAsync(Person, Body(Endpoint, PushOutcomeTests.Subscriber(), await push.KeyAsync(), expires));
        push.Host.Clock.Advance(TimeSpan.FromSeconds(31));

        SendReceipt receipt = await push.PushedAsync(Person);
        long before = await push.SubscriptionsAsync();
        push.Host.Clock.Advance(TimeSpan.FromMinutes(1));
        await UntilAsync(async () => await push.SubscriptionsAsync() == 0);

        Assert.Equal(HttpStatusCode.NoContent, posted.StatusCode);
        Assert.Equal(DeliveryStatus.NoSubscription, await SuppressionTests.StatusAsync(push.Host, receipt));
        Assert.Equal(1L, before);
        Assert.Empty(push.Provider.Received);
    }

    [Fact]
    public async Task MSG_PUSH_007_AC1_AfterANewVersionIsMadeTheOneNewSubscriptionsUseAnOldSubscriptionIsSignedWithTheOldKeyAndANewOneWithTheNew()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        string oldKey;
        await using (DispatchEngine beforeEngine = await StartAsync(connection: connection, vapidWriteVersion: 1))
        {
            Pusher before = new(beforeEngine);
            oldKey = await before.KeyAsync();
            await before.SubscribeAsync(Person, Endpoint);
        }

        await using DispatchEngine afterEngine = await StartAsync(connection: connection, vapidWriteVersion: 2);
        Pusher after = new(afterEngine);
        string newKey = await after.KeyAsync();
        await after.SubscribeAsync(Person, OtherEndpoint);
        await after.PushedAsync(Person);

        Assert.NotEqual(oldKey, newKey);
        Assert.Equal([(Endpoint, 1), (OtherEndpoint, 2)], after.Provider.Received.Select(request => (request.Endpoint, request.KeyVersion)).Order());
    }

    [Fact]
    public async Task MSG_PUSH_007_AC2_ASubscriptionPostedWithTheOldKeyAfterTheSwitchRecordsTheOldVersion()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        string oldKey;
        await using (DispatchEngine beforeEngine = await StartAsync(connection: connection, vapidWriteVersion: 1))
        {
            Pusher before = new(beforeEngine);
            oldKey = await before.KeyAsync();
        }

        await using DispatchEngine afterEngine = await StartAsync(connection: connection, vapidWriteVersion: 2);
        Pusher after = new(afterEngine);
        using HttpResponseMessage response = await after.PostAsync(Person, Body(Endpoint, PushOutcomeTests.Subscriber(), oldKey));
        await after.PushedAsync(Person);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(1, Assert.Single(after.Provider.Received).KeyVersion);
    }

    [Fact]
    public async Task MSG_API_004_AC3_GetPushKeyAnswers200WithNoSignedInPerson()
    {
        await using DispatchEngine engine = await StartAsync();
        Pusher push = new(engine);

        using HttpResponseMessage response = await push.SendAsync(HttpMethod.Get, "/push/key");
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(65, Base64Url.DecodeFromChars(body.RootElement.GetProperty("publicKey").GetString()).Length);
    }

    [Fact]
    public async Task MSG_API_004_AC4_AKeyNoVersionHasOrARetiredVersionsIsRefused422AndStoredNowhere()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        await using DispatchEngine engine = await StartAsync(connection: connection, vapidWriteVersion: 2);
        Pusher push = new(engine);
        string retired = Base64Url.EncodeToString(KeyOf(1));
        await using (NpgsqlConnection database = new(connection))
        {
            await database.OpenAsync(TestContext.Current.CancellationToken);
            await using NpgsqlCommand command = new("UPDATE messaging.key_versions SET retired = true WHERE key = 'pushVapidKeys' AND version = 1", database);
            Assert.Equal(1, await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
        }

        using HttpResponseMessage unknown = await push.PostAsync(Person, Body(Endpoint, PushOutcomeTests.Subscriber(), Base64Url.EncodeToString(KeyOf(8))));
        using HttpResponseMessage old = await push.PostAsync(Person, Body(Endpoint, PushOutcomeTests.Subscriber(), retired));

        foreach (HttpResponseMessage response in new[] { unknown, old })
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Equal("messaging.push.keyunknown", (await PreferenceEndpointTests.RefusalAsync(response)).Code);
        }

        Assert.Equal(0L, await push.SubscriptionsAsync());
    }

    [Fact]
    public async Task MSG_API_004_AC4_ASubscriptionPostedWithTheKeyOfAVersionInUseThatThisProcessDoesNotHoldIsAnswered503()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        await using DispatchEngine holdsOneEngine = await StartAsync(connection: connection, vapidWriteVersion: 1, versions: 1);
        Pusher holdsOne = new(holdsOneEngine);
        await using DispatchEngine holdsTwoEngine = await StartAsync(connection: connection, vapidWriteVersion: 2);
        Pusher holdsTwo = new(holdsTwoEngine);

        using HttpResponseMessage response = await holdsOne.PostAsync(Person, Body(Endpoint, PushOutcomeTests.Subscriber(), await holdsTwo.KeyAsync()));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("messaging.request.unavailable", (await PreferenceEndpointTests.RefusalAsync(response)).Code);
        Assert.NotNull(response.Headers.RetryAfter);
        Assert.Equal(0L, await holdsOne.SubscriptionsAsync());
    }

    [Fact]
    public async Task MSG_PUSH_002_AC3_AKindWithATtlOf1DayAndAnExpiryOf10MinutesSendsATtlOfAtMost600()
    {
        await using DispatchEngine engine = await StartAsync(declare: declare =>
        {
            declare.Category(EngineHost.Category).Essential();
            declare.Kind(EngineHost.Kind, EngineHost.Category).Push(ttl: TimeSpan.FromDays(1)).ExpiresAfter(TimeSpan.FromMinutes(10));
        });
        Pusher push = new(engine);
        await push.SubscribeAsync(Person, Endpoint);

        await push.PushedAsync(Person);

        TimeSpan ttl = Assert.Single(push.Provider.Received).Ttl;
        Assert.InRange(ttl, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(600));
    }

    /// <summary>The VAPID public key of the scalar of 32 bytes of <paramref name="value"/>.</summary>
    private static byte[] KeyOf(byte value)
    {
        using System.Security.Cryptography.ECDsa key = System.Security.Cryptography.ECDsa.Create(new System.Security.Cryptography.ECParameters { Curve = System.Security.Cryptography.ECCurve.NamedCurves.nistP256, D = Secrets.Bytes(32, value) });
        System.Security.Cryptography.ECParameters parameters = key.ExportParameters(includePrivateParameters: false);
        return [0x04, .. parameters.Q.X!, .. parameters.Q.Y!];
    }

    private static Send Code(string person)
    {
        return SendTests.Code(channels: [Channel.Push], recipient: new Recipient.Person(person));
    }

    private static string Body(string endpoint, (string P256dh, string Auth) keys, string serverKey, long? expires = null)
    {
        string expiration = expires is { } at ? at.ToString(System.Globalization.CultureInfo.InvariantCulture) : "null";
        return $$"""{"endpoint":"{{endpoint}}","expirationTime":{{expiration}},"keys":{"p256dh":"{{keys.P256dh}}","auth":"{{keys.Auth}}"},"applicationServerKey":"{{serverKey}}"}""";
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

    private async Task<DispatchEngine> StartAsync(
        Action<Dictionary<string, string?>>? configure = null,
        Action<IServiceCollection>? services = null,
        Action<DeclarationBuilder>? declare = null,
        string? connection = null,
        int vapidWriteVersion = 1,
        int versions = 2,
        bool shipped = false)
    {
        string database = connection ?? await EngineHost.DatabaseAsync(postgres);
        SecretSourceFake secrets = Secrets.Held(database).With("pushVapidKeys", [.. new[] { Secrets.Bytes(32, 4), Secrets.Bytes(32, 7) }.Take(versions)]);
        return await DispatchEngine.StartAsync(
            postgres,
            configure: configuration =>
            {
                configuration["Hermes:Push:AllowedHosts:0"] = "push.example.com";
                configuration["Hermes:Secrets:PushVapidKeys:WriteVersion"] = vapidWriteVersion.ToString(System.Globalization.CultureInfo.InvariantCulture);
                configure?.Invoke(configuration);
            },
            declare: declare,
            services: collection =>
            {
                Endpoints.Identity(collection);
                collection.AddSingleton<ISecretSource>(secrets);
                if (!shipped)
                {
                    collection.AddSingleton<IPushProvider, RecordingPushProvider>();
                }

                services?.Invoke(collection);
            },
            connection: database,
            map: application => application.MapHermesPushSubscriptions(Endpoints.Prefix));
    }

    /// <summary>A started engine mapping the push group, with its push provider.</summary>
    private sealed class Pusher(DispatchEngine engine)
    {
        internal EngineHost Host => engine.Host;

        internal RecordingPushProvider Provider => (RecordingPushProvider)Host.Services.GetRequiredService<IPushProvider>();

        internal async Task<HttpResponseMessage> SendAsync(HttpMethod method, string route, string? person = null, string? json = null)
        {
            using HttpClient client = Host.Client();
            using StringContent? content = json is null ? null : Endpoints.Json(json);
            return await Endpoints.SendAsync(client, method, route, person, content);
        }

        internal async Task<string> KeyAsync()
        {
            using HttpResponseMessage response = await SendAsync(HttpMethod.Get, "/push/key");
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            return body.RootElement.GetProperty("publicKey").GetString()!;
        }

        internal async Task<HttpStatusCode> SubscribeAsync(string person, string endpoint, string? p256dh = null, string? auth = null)
        {
            (string P256dh, string Auth) keys = p256dh is null ? PushOutcomeTests.Subscriber() : (p256dh, auth!);
            using HttpResponseMessage response = await PostAsync(person, Body(endpoint, keys, await KeyAsync()));
            return response.StatusCode;
        }

        internal Task<HttpResponseMessage> PostAsync(string person, string body)
        {
            return SendAsync(HttpMethod.Post, "/push/subscriptions", person, body);
        }

        /// <summary>Sends a push to <paramref name="person"/> and runs a pass.</summary>
        internal async Task<SendReceipt> PushedAsync(string person)
        {
            SendReceipt receipt = (await Host.SendAsync(Code(person))).Value!;
            await Host.DispatchAsync();
            return receipt;
        }

        internal async Task<long> SubscriptionsAsync()
        {
            await using NpgsqlConnection database = new(Host.Connection);
            await database.OpenAsync(TestContext.Current.CancellationToken);
            await using NpgsqlCommand command = new("SELECT count(*) FROM messaging.subscriptions", database);
            return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
        }
    }
}