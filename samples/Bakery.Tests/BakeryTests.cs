using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Development;
using Hermes.Testing;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Bakery.Tests;

/// <summary>The bakery's site, through its routes and the library's endpoint groups, its notices read from the in-memory providers.</summary>
public sealed class BakeryTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task PlaceOrder_SignedInCustomer_IsToldByEmailAndSms()
    {
        await using BakeryFactory bakery = await BakeryFactory.StartAsync(postgres);
        using HttpClient customer = await bakery.SignInAsync("c-1");

        using HttpResponseMessage ordered = await customer.PostAsJsonAsync("/orders", new { item = "a rye loaf" }, TestContext.Current.CancellationToken);
        int delivered = await bakery.DispatchAsync();

        Assert.Equal((HttpStatusCode.NoContent, 2), (ordered.StatusCode, delivered));
        EmailMessage email = Assert.Single(bakery.Services.GetRequiredService<InMemoryEmailProvider>().Received);
        Assert.Equal(
            ("amal@example.com", "Your order of a rye loaf is in", "Thank you. We have your order of a rye loaf and will have it ready to collect today."),
            (email.ToAddress, email.Subject, email.Text));
        SmsMessage sms = Assert.Single(bakery.Services.GetRequiredService<InMemorySmsProvider>().Received);
        Assert.Equal(("+201000000001", "Bakery", "Your order of a rye loaf is in."), (sms.ToNumber, sms.Sender, sms.Text));
    }

    [Fact]
    public async Task PlaceOrder_InTheTests_GoesThroughTheInMemoryProvidersAndLeavesTheDevelopmentInboxEmpty()
    {
        await using BakeryFactory bakery = await BakeryFactory.StartAsync(postgres);
        using HttpClient customer = await bakery.SignInAsync("c-1");

        using HttpResponseMessage ordered = await customer.PostAsJsonAsync("/orders", new { item = "a rye loaf" }, TestContext.Current.CancellationToken);
        int delivered = await bakery.DispatchAsync();

        Assert.Equal((HttpStatusCode.NoContent, 2), (ordered.StatusCode, delivered));
        Assert.Single(bakery.Services.GetRequiredService<InMemoryEmailProvider>().Received);
        Assert.Empty(await new DevelopmentInbox(bakery.InboxFolder).ListAsync(new InboxFilter(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DevelopmentInbox_AskedFromOutsideTheMachine_Is403()
    {
        await using BakeryFactory bakery = await BakeryFactory.StartAsync(postgres);
        using HttpClient client = bakery.CreateClient();

        using HttpResponseMessage answer = await client.GetAsync(Route("/dev/inbox/messages"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, answer.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await answer.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("messaging.inbox.notlocal", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task PlaceOrder_NoOneSignedIn_Is401AndSendsNothing()
    {
        await using BakeryFactory bakery = await BakeryFactory.StartAsync(postgres);
        using HttpClient client = bakery.CreateClient();

        using HttpResponseMessage ordered = await client.PostAsJsonAsync("/orders", new { item = "a rye loaf" }, TestContext.Current.CancellationToken);

        Assert.Equal((HttpStatusCode.Unauthorized, 0), (ordered.StatusCode, await bakery.DispatchAsync()));
    }

    [Fact]
    public async Task SendOffer_ACustomer_Is403()
    {
        await using BakeryFactory bakery = await BakeryFactory.StartAsync(postgres);
        using HttpClient customer = await bakery.SignInAsync("c-1");

        using HttpResponseMessage sent = await customer.PostAsJsonAsync("/offers", new { text = "Two rolls for one" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, sent.StatusCode);
    }

    [Fact]
    public async Task SendOffer_ToACustomerWithAPushSubscriptionAndOneWhoTurnedItsEmailOff_GoesByEmailToTheOneAndByPushToTheOther()
    {
        await using BakeryFactory bakery = await BakeryFactory.StartAsync(postgres);
        using HttpClient amal = await bakery.SignInAsync("c-1");
        using HttpClient omar = await bakery.SignInAsync("c-2");
        using HttpClient staff = await bakery.SignInAsync("s-1");
        string endpoint = "https://push.example.com/send/" + Guid.NewGuid().ToString("N");

        using HttpResponseMessage subscribed = await SubscribeAsync(amal, endpoint);
        using HttpResponseMessage turnedOff = await omar.PutAsJsonAsync("/notifications/preferences/offers/email", new { on = false }, TestContext.Current.CancellationToken);
        using HttpResponseMessage essential = await omar.PutAsJsonAsync("/notifications/preferences/orders/email", new { on = false }, TestContext.Current.CancellationToken);
        using JsonDocument preferences = JsonDocument.Parse(await omar.GetStringAsync(Route("/notifications/preferences"), TestContext.Current.CancellationToken));
        using HttpResponseMessage sent = await staff.PostAsJsonAsync("/offers", new { text = "Two rolls for one" }, TestContext.Current.CancellationToken);
        await bakery.DispatchAsync();

        Assert.Equal(
            (HttpStatusCode.NoContent, HttpStatusCode.NoContent, HttpStatusCode.UnprocessableEntity, HttpStatusCode.NoContent),
            (subscribed.StatusCode, turnedOff.StatusCode, essential.StatusCode, sent.StatusCode));
        Assert.False(Channel(preferences, "offers", "email").GetProperty("on").GetBoolean());
        EmailMessage email = Assert.Single(bakery.Services.GetRequiredService<InMemoryEmailProvider>().Received);
        Assert.Equal(("amal@example.com", "This week at the bakery"), (email.ToAddress, email.Subject));
        Assert.StartsWith("Two rolls for one", email.Text, StringComparison.Ordinal);
        PushRequest.WebPush push = Assert.IsType<PushRequest.WebPush>(Assert.Single(bakery.Services.GetRequiredService<InMemoryPushProvider>().Received));
        using JsonDocument payload = JsonDocument.Parse(push.Payload);
        Assert.Equal(endpoint, push.Endpoint);
        Assert.Contains("Two rolls for one", payload.RootElement.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendOffer_AfterTheCustomerUnsubscribedThroughTheEmailsLink_SendsThemNoMoreEmail()
    {
        await using BakeryFactory bakery = await BakeryFactory.StartAsync(postgres);
        using HttpClient staff = await bakery.SignInAsync("s-1");
        using HttpClient anyone = bakery.CreateClient();
        InMemoryEmailProvider emails = bakery.Services.GetRequiredService<InMemoryEmailProvider>();

        using HttpResponseMessage first = await staff.PostAsJsonAsync("/offers", new { text = "Two rolls for one" }, TestContext.Current.CancellationToken);
        await bakery.DispatchAsync();
        Uri link = emails.Received.Single(email => email.ToAddress == "amal@example.com").Unsubscribe!;
        using FormUrlEncodedContent oneClick = new([new KeyValuePair<string, string>("List-Unsubscribe", "One-Click")]);
        using HttpResponseMessage unsubscribed = await anyone.PostAsync(Route(link.PathAndQuery), oneClick, TestContext.Current.CancellationToken);
        using HttpResponseMessage second = await staff.PostAsJsonAsync("/offers", new { text = "A free coffee with any cake" }, TestContext.Current.CancellationToken);
        await bakery.DispatchAsync();

        Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.OK, HttpStatusCode.NoContent), (first.StatusCode, unsubscribed.StatusCode, second.StatusCode));
        Assert.Equal("https://bakery.example.com", link.GetLeftPart(UriPartial.Authority));
        Assert.Equal(
            [("amal@example.com", "Two rolls for one"), ("omar@example.net", "A free coffee with any cake"), ("omar@example.net", "Two rolls for one")],
            emails.Received.Select(email => (email.ToAddress, email.Text.Split('\n')[0])).Order());
    }

    [Fact]
    public async Task SmsReport_AWrongKey_Is404()
    {
        await using BakeryFactory bakery = await BakeryFactory.StartAsync(postgres);
        using HttpClient provider = bakery.CreateClient();

        using HttpResponseMessage report = await provider.GetAsync(Route("/notifications/sms/report/" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16))), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, report.StatusCode);
    }

    /// <summary>Subscribes as a browser does: with the key <c>GET /notifications/push/key</c> answers, and keys of its own.</summary>
    private static async Task<HttpResponseMessage> SubscribeAsync(HttpClient client, string endpoint)
    {
        using JsonDocument key = JsonDocument.Parse(await client.GetStringAsync(Route("/notifications/push/key"), TestContext.Current.CancellationToken));
        using ECDiffieHellman browser = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        ECParameters point = browser.ExportParameters(includePrivateParameters: false);
        byte[] publicKey = [0x04, .. point.Q.X!, .. point.Q.Y!];
        string body = JsonSerializer.Serialize(new
        {
            endpoint,
            expirationTime = (long?)null,
            keys = new { p256dh = Base64Url.EncodeToString(publicKey), auth = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16)) },
            applicationServerKey = key.RootElement.GetProperty("publicKey").GetString(),
        });
        using StringContent content = new(body, Encoding.UTF8, "application/json");
        return await client.PostAsync(Route("/notifications/push/subscriptions"), content, TestContext.Current.CancellationToken);
    }

    private static Uri Route(string path)
    {
        return new Uri(path, UriKind.Relative);
    }

    private static JsonElement Channel(JsonDocument preferences, string category, string channel)
    {
        JsonElement found = preferences.RootElement.GetProperty("categories").EnumerateArray().Single(item => item.GetProperty("name").GetString() == category);
        return found.GetProperty("channels").EnumerateArray().Single(item => item.GetProperty("channel").GetString() == channel);
    }
}