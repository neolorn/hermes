using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Hermes.Tests;

/// <summary>Each endpoint group of <c>05</c> mapped alone (<c>04</c> MSG-LIB-004; <c>00</c> MSG-OVR-001).</summary>
[Trait("Category", "integration")]
public sealed partial class EndpointGroupTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
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
    public async Task MSG_THR_002_AC1_NoRouteOfTheFourGroupsRecordsASend()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        await using DispatchEngine engine = await DispatchEngine.StartAsync(
            postgres,
            configure: configuration =>
            {
                UnsubscribeLinkTests.Configure(configuration);
                configuration["Hermes:Push:AllowedHosts:0"] = "push.example.com";
            },
            declare: UnsubscribeLinkTests.Declare,
            templates: UnsubscribeLinkTests.Templates,
            services: services =>
            {
                Endpoints.Identity(services);
                services.AddSingleton<IPushProvider, RecordingPushProvider>();
            },
            connection: connection,
            map: application =>
            {
                SmsReportTests.Map(application);
                application.MapHermesPreferences(Endpoints.Prefix);
                application.MapHermesUnsubscribe(Endpoints.Prefix);
                application.MapHermesPushSubscriptions(Endpoints.Prefix);
            },
            secrets: Secrets.Held(connection).With("smsReportKey", Secrets.Bytes(16, 5)));
        SendReceipt letter = (await engine.Host.SendAsync(UnsubscribeLinkTests.LetterTo("reader@example.com"))).Value!;
        string unsubscribe = (await engine.Host.OpenAsync(letter.Deliveries[0].Delivery.Value, "unsubscribe"))![UnsubscribeLinkTests.BaseAddress.Length..];
        (long sends, long deliveries, long keys) recorded = await engine.Host.RecordedAsync();
        using HttpClient client = engine.Host.Client();

        using FormUrlEncodedContent oneClick = new([new("List-Unsubscribe", "One-Click")]);
        using HttpResponseMessage unsubscribed = await Endpoints.SendAsync(client, HttpMethod.Post, unsubscribe, content: oneClick);
        List<string> called = [];
        foreach (RouteEndpoint endpoint in engine.Host.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>())
        {
            foreach (string method in endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [])
            {
                string route = Parameter().Replace(endpoint.RoutePattern.RawText!, "x");
                using HttpResponseMessage response = await CallAsync(client, method, "/" + route.TrimStart('/'));
                called.Add($"{method} {endpoint.RoutePattern.RawText}");
            }
        }

        Assert.Equal(HttpStatusCode.OK, unsubscribed.StatusCode);
        Assert.Contains(called, route => route.StartsWith("POST", StringComparison.Ordinal) && route.Contains("/unsubscribe/", StringComparison.Ordinal));
        Assert.Contains(called, route => route.StartsWith("PUT", StringComparison.Ordinal) && route.Contains("/preferences", StringComparison.Ordinal));
        Assert.Contains(called, route => route.StartsWith("POST", StringComparison.Ordinal) && route.Contains("/push/subscriptions", StringComparison.Ordinal));
        Assert.Contains(called, route => route.StartsWith("POST", StringComparison.Ordinal) && route.Contains("/sms/", StringComparison.Ordinal));
        Assert.Equal(recorded, await engine.Host.RecordedAsync());
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

    /// <summary>Calls <paramref name="route"/> as the person, with an empty JSON object as the body of any method but <c>GET</c>.</summary>
    private static async Task<HttpResponseMessage> CallAsync(HttpClient client, string method, string route)
    {
        if (method == "GET")
        {
            return await Endpoints.SendAsync(client, HttpMethod.Get, route, Person, prefix: string.Empty);
        }

        using StringContent body = Endpoints.Json("{}");
        return await Endpoints.SendAsync(client, new HttpMethod(method), route, Person, body, prefix: string.Empty);
    }

    /// <summary>A route parameter, which the test fills with a value of its own.</summary>
    [GeneratedRegex(@"\{[^}]*\}")]
    private static partial Regex Parameter();
}