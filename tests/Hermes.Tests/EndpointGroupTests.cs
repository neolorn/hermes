using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Hermes.Tests;

/// <summary>Each endpoint group of <c>05</c> mapped alone (<c>04</c> MSG-LIB-004; <c>00</c> MSG-OVR-001).</summary>
[Trait("Category", "integration")]
public sealed class EndpointGroupTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Person = "person-1";

    [Fact]
    public async Task MSG_LIB_004_AC1_ThePreferencesGroupMappedAloneWorks()
    {
        await using DispatchEngine engine = await DispatchEngine.StartAsync(
            postgres,
            declare: SuppressionTests.Declare,
            services: Endpoints.Identity,
            map: application => application.MapHermesPreferences(Endpoints.Prefix));
        using HttpClient client = engine.Host.Client();

        using HttpResponseMessage response = await Endpoints.SendAsync(client, HttpMethod.Get, "/preferences", Person);
        using HttpResponseMessage other = await Endpoints.SendAsync(client, HttpMethod.Get, "/push/key");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
    }

    [Fact]
    public async Task MSG_LIB_004_AC1_ThePushSubscriptionGroupMappedAloneWorks()
    {
        await using DispatchEngine engine = await DispatchEngine.StartAsync(
            postgres,
            configure: configuration => configuration["Hermes:Push:AllowedHosts:0"] = "push.example.com",
            services: services =>
            {
                Endpoints.Identity(services);
                services.AddSingleton<IPushProvider, RecordingPushProvider>();
            },
            map: application => application.MapHermesPushSubscriptions(Endpoints.Prefix));
        using HttpClient client = engine.Host.Client();

        using HttpResponseMessage key = await Endpoints.SendAsync(client, HttpMethod.Get, "/push/key");
        (string p256dh, string auth) = PushOutcomeTests.Subscriber();
        using System.Text.Json.JsonDocument read = System.Text.Json.JsonDocument.Parse(await key.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        string publicKey = read.RootElement.GetProperty("publicKey").GetString()!;
        using HttpResponseMessage posted = await Endpoints.SendAsync(
            client,
            HttpMethod.Post,
            "/push/subscriptions",
            Person,
            Endpoints.Json($$"""{"endpoint":"https://push.example.com/send/alone","keys":{"p256dh":"{{p256dh}}","auth":"{{auth}}"},"applicationServerKey":"{{publicKey}}"}"""));
        using HttpResponseMessage other = await Endpoints.SendAsync(client, HttpMethod.Get, "/preferences", Person);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.NoContent), (key.StatusCode, posted.StatusCode));
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
    }

    [Fact]
    public async Task MSG_LIB_004_AC1_TheSmsReportGroupMappedAloneWorks()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        await using DispatchEngine engine = await DispatchEngine.StartAsync(
            postgres,
            connection: connection,
            services: services => services.AddSingleton<ISecretSource>(Secrets.Held(connection).With("smsReportKey", Secrets.Bytes(16, 5))),
            map: SmsReportTests.Map);
        using HttpClient client = engine.Host.Client();

        using FormUrlEncodedContent body = new([new("pid", "wx-alone"), new("status", "delivered")]);
        using HttpRequestMessage request = new(HttpMethod.Post, Endpoints.Prefix + SmsReportTests.Route()) { Content = body };
        request.Headers.Add(SmsReportTests.SourceHeader, "127.0.0.1");
        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        using HttpResponseMessage other = await Endpoints.SendAsync(client, HttpMethod.Get, "/push/key");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
    }

    [Fact]
    public async Task MSG_OVR_001_AC2_TheUnsubscribeGroupMappedAloneInAnApplicationThatDoesNotDispatchWorksForALetterAnotherApplicationSent()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        await using DispatchEngine sender = await DispatchEngine.StartAsync(
            postgres,
            configure: UnsubscribeLinkTests.Configure,
            declare: UnsubscribeLinkTests.Declare,
            templates: UnsubscribeLinkTests.Templates,
            connection: connection);
        SendReceipt receipt = (await sender.Host.SendAsync(UnsubscribeLinkTests.LetterTo("reader@example.com"))).Value!;
        string link = (await sender.Host.OpenAsync(receipt.Deliveries[0].Delivery.Value, "unsubscribe"))!;
        await using EngineHost endpoints = await EngineHost.StartAsync(
            connection,
            declare: UnsubscribeLinkTests.Declare,
            templates: UnsubscribeLinkTests.Templates,
            configure: UnsubscribeLinkTests.Configure,
            map: application => application.MapHermesUnsubscribe(Endpoints.Prefix));
        using HttpClient client = endpoints.Client();

        using FormUrlEncodedContent body = new([new("List-Unsubscribe", "One-Click")]);
        using HttpResponseMessage response = await Endpoints.SendAsync(client, HttpMethod.Post, link[UnsubscribeLinkTests.BaseAddress.Length..], content: body);
        await sender.Host.DispatchAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(DeliveryStatus.Suppressed, await SuppressionTests.StatusAsync(sender.Host, receipt));
    }

    [Fact]
    public async Task MSG_LIB_004_GetPushKeyAnswersWithNoSignedInPersonUnderAGroupThatRequiresAuthorization()
    {
        await using DispatchEngine engine = await DispatchEngine.StartAsync(
            postgres,
            configure: configuration => configuration["Hermes:Push:AllowedHosts:0"] = "push.example.com",
            services: services =>
            {
                Endpoints.Identity(services);
                services.AddSingleton<IPushProvider, RecordingPushProvider>();
                services.AddAuthentication();
                services.AddAuthorization();
            },
            map: application =>
            {
                application.UseAuthentication();
                application.UseAuthorization();
                application.MapHermesPushSubscriptions(Endpoints.Prefix).RequireAuthorization(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
            });
        using HttpClient client = engine.Host.Client();

        using HttpResponseMessage key = await Endpoints.SendAsync(client, HttpMethod.Get, "/push/key");

        Assert.Equal(HttpStatusCode.OK, key.StatusCode);
    }
}