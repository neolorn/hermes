using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

using Hermes.Core;

using Xunit;

namespace Hermes.Development.Tests;

/// <summary>The inbox's JSON API (<c>05</c> MSG-API-007).</summary>
[Trait("Category", "unit")]
public sealed class InboxApiTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 29, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task MSG_API_007_AC1_AnEmailAnSmsAndAPushAreEachAnsweredWithTheirMembersAndListedNewestFirst()
    {
        using ScratchFolder folder = new();
        ManualClock clock = new(Start);
        await using InboxSite site = await InboxSite.StartAsync(folder.Path, clock: clock);
        string email = await site.EmailAsync(Captured.Email());
        clock.Advance(TimeSpan.FromSeconds(1));
        string sms = await site.SmsAsync(Captured.Sms() with { Text = "رمز الدخول 45K1", Encoding = SmsEncoding.Ucs2 });
        clock.Advance(TimeSpan.FromSeconds(1));
        string push = await site.PushAsync(Captured.Push("https://fcm.googleapis.com/fcm/send/abc"));

        JsonObject list = await JsonAsync(site, "/messages");
        JsonArray messages = list["messages"]!.AsArray();
        Assert.Equal([push, sms, email], messages.Select(message => message!["id"]!.GetValue<string>()));
        Assert.Equal(
            ["app", "at", "category", "channel", "id", "kind", "language", "person", "summary", "to"],
            messages[0]!.AsObject().Select(member => member.Key).Order(StringComparer.Ordinal));
        Assert.Equal(("sms", "2026-10-29T09:00:01.000Z"), (messages[1]!["channel"]!.GetValue<string>(), messages[1]!["at"]!.GetValue<string>()));

        JsonObject emailBody = await JsonAsync(site, "/messages/" + email);
        Assert.Equal(("email", "someone@example.com", "Your receipt"), (emailBody["channel"]!.GetValue<string>(), emailBody["to"]!.GetValue<string>(), emailBody["summary"]!.GetValue<string>()));
        Assert.Equal("orders@bakery.example", emailBody["email"]!["from"]!["address"]!.GetValue<string>());
        Assert.Equal(["https://bakery.example/receipts/7"], emailBody["links"]!.AsArray().Select(link => link!.GetValue<string>()));
        Assert.Null(emailBody["sms"]);
        Assert.Null(emailBody["push"]);
        Assert.Null(emailBody["kind"]);
        Assert.Contains(emailBody["email"]!["headers"]!.AsArray(), header => header!["name"]!.GetValue<string>() == "Subject" && header["value"]!.GetValue<string>() == "Your receipt");

        JsonObject smsBody = await JsonAsync(site, "/messages/" + sms);
        Assert.Equal(("رمز الدخول 45K1", "ucs2", 1), (smsBody["sms"]!["text"]!.GetValue<string>(), smsBody["sms"]!["encoding"]!.GetValue<string>(), smsBody["sms"]!["segments"]!.GetValue<int>()));

        JsonObject pushBody = await JsonAsync(site, "/messages/" + push);
        Assert.Equal(("fcm", "high", 3600), (pushBody["push"]!["service"]!.GetValue<string>(), pushBody["push"]!["urgency"]!.GetValue<string>(), pushBody["push"]!["ttl"]!.GetValue<int>()));
        Assert.Equal("Your order", pushBody["push"]!["notification"]!["title"]!.GetValue<string>());
        Assert.Null(pushBody["to"]);
    }

    [Fact]
    public async Task MSG_API_007_AC1_AMessageIsAnsweredAsTheSameJsonItsRecordHolds()
    {
        using ScratchFolder folder = new();
        await using InboxSite site = await InboxSite.StartAsync(folder.Path);
        string id = await site.EmailAsync(Captured.Email());

        using HttpResponseMessage answer = await site.SendAsync(HttpMethod.Get, "/messages/" + id);

        string record = await File.ReadAllTextAsync(Directory.EnumerateFiles(folder.Path, "*.json").Single(), TestContext.Current.CancellationToken);
        Assert.Equal(JsonNode.Parse(record)!.ToJsonString(), JsonNode.Parse(await answer.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!.ToJsonString());
        Assert.Equal("application/json", answer.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task MSG_API_007_AC1_TheListIsFilteredByEachQueryParameterAndBoundedByLimit()
    {
        using ScratchFolder folder = new();
        ManualClock clock = new(Start);
        await using InboxSite site = await InboxSite.StartAsync(folder.Path, clock: clock);
        string first = await site.EmailAsync(Captured.Email());
        clock.Advance(TimeSpan.FromSeconds(1));
        string other = await site.EmailAsync(Captured.Email() with { ToAddress = "other@example.com" });
        clock.Advance(TimeSpan.FromSeconds(1));
        string sms = await site.SmsAsync(Captured.Sms());

        Assert.Equal([first], await IdsAsync(site, "/messages?to=SomeOne@Example.com"));
        Assert.Equal([sms], await IdsAsync(site, "/messages?to=%2B201000000001"));
        Assert.Equal([sms], await IdsAsync(site, "/messages?channel=sms"));
        Assert.Equal([other, first], await IdsAsync(site, "/messages?channel=email&app=" + Uri.EscapeDataString(site.App.Environment.ApplicationName)));
        Assert.Empty(await IdsAsync(site, "/messages?kind=order.ready"));
        Assert.Empty(await IdsAsync(site, "/messages?person=person-1"));
        Assert.Equal([sms], await IdsAsync(site, "/messages?after=" + Uri.EscapeDataString("2026-10-29T11:00:01.000+02:00")));
        Assert.Equal([sms, other], await IdsAsync(site, "/messages?limit=2"));
    }

    [Theory]
    [InlineData("/messages?to=someone", "to")]
    [InlineData("/messages?to=%2B0123", "to")]
    [InlineData("/messages?to=", "to")]
    [InlineData("/messages?person=", "person")]
    [InlineData("/messages?channel=fax", "channel")]
    [InlineData("/messages?channel=Email", "channel")]
    [InlineData("/messages?app=", "app")]
    [InlineData("/messages?kind=", "kind")]
    [InlineData("/messages?after=2026-10-29T09:00:00", "after")]
    [InlineData("/messages?after=yesterday", "after")]
    [InlineData("/messages?limit=0", "limit")]
    [InlineData("/messages?limit=1001", "limit")]
    [InlineData("/messages?limit=ten", "limit")]
    [InlineData("/messages?channel=sms&channel=push", "channel")]
    [InlineData("/messages/latest?wait=31", "wait")]
    [InlineData("/messages/latest?wait=-1", "wait")]
    [InlineData("/messages/latest?wait=1.5", "wait")]
    [InlineData("/messages/latest?after=2026-10-29", "after")]
    public async Task MSG_API_007_AC1_AQueryParameterOutsideItsRuleIsAnswered400NamingIt(string route, string member)
    {
        using ScratchFolder folder = new();
        await using InboxSite site = await InboxSite.StartAsync(folder.Path);

        using HttpResponseMessage answer = await site.SendAsync(HttpMethod.Get, route);

        Assert.Equal(HttpStatusCode.BadRequest, answer.StatusCode);
        JsonObject body = JsonNode.Parse(await answer.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!.AsObject();
        Assert.Equal(("messaging.request.malformed", member), (body["code"]!.GetValue<string>(), body["details"]!["member"]!.GetValue<string>()));
    }

    [Fact]
    public async Task MSG_API_007_AC1_AParameterOfTheOtherRouteIsIgnored()
    {
        using ScratchFolder folder = new();
        await using InboxSite site = await InboxSite.StartAsync(folder.Path);
        string id = await site.SmsAsync(Captured.Sms());

        Assert.Equal([id], await IdsAsync(site, "/messages?wait=ten"));
        Assert.Equal(id, (await JsonAsync(site, "/messages/latest?limit=none"))["id"]!.GetValue<string>());
    }

    [Fact]
    public async Task MSG_API_007_AC2_LatestWithWaitAnswersTheMessageAsSoonAsItIsCaptured()
    {
        using ScratchFolder folder = new();
        await using InboxSite site = await InboxSite.StartAsync(folder.Path);
        await site.EmailAsync(Captured.Email() with { ToAddress = "earlier@example.com" });
        Stopwatch waited = Stopwatch.StartNew();

        Task<HttpResponseMessage> asked = site.SendAsync(HttpMethod.Get, "/messages/latest?to=someone@example.com&wait=10");
        await Task.Delay(500, TestContext.Current.CancellationToken);
        Assert.False(asked.IsCompleted);
        string id = await site.EmailAsync(Captured.Email());
        using HttpResponseMessage answer = await asked;

        Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
        Assert.Equal(id, JsonNode.Parse(await answer.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!["id"]!.GetValue<string>());
        Assert.True(waited.Elapsed < TimeSpan.FromSeconds(5), $"The answer came after {waited.Elapsed}.");
    }

    [Fact]
    public async Task MSG_API_007_AC2_LatestWithWaitAndNothingSentAnswers404OnceTheWaitHasPassed()
    {
        using ScratchFolder folder = new();
        await using InboxSite site = await InboxSite.StartAsync(folder.Path);
        await site.EmailAsync(Captured.Email() with { ToAddress = "earlier@example.com" });
        Stopwatch waited = Stopwatch.StartNew();

        using HttpResponseMessage answer = await site.SendAsync(HttpMethod.Get, "/messages/latest?to=someone@example.com&wait=10");

        Assert.Equal(HttpStatusCode.NotFound, answer.StatusCode);
        Assert.Equal("messaging.inbox.notfound", JsonNode.Parse(await answer.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!["code"]!.GetValue<string>());
        Assert.True(waited.Elapsed >= TimeSpan.FromSeconds(10), $"The answer came after {waited.Elapsed}.");
    }

    [Fact]
    public async Task MSG_API_007_AC2_LatestWithoutWaitAnswersTheNewestMatchOr404AtOnce()
    {
        using ScratchFolder folder = new();
        ManualClock clock = new(Start);
        await using InboxSite site = await InboxSite.StartAsync(folder.Path, clock: clock);
        await site.EmailAsync(Captured.Email());
        clock.Advance(TimeSpan.FromSeconds(1));
        string newest = await site.EmailAsync(Captured.Email());

        Assert.Equal(newest, (await JsonAsync(site, "/messages/latest?to=someone@example.com"))["id"]!.GetValue<string>());
        using HttpResponseMessage none = await site.SendAsync(HttpMethod.Get, "/messages/latest?channel=push");
        Assert.Equal(HttpStatusCode.NotFound, none.StatusCode);
    }

    [Fact]
    public async Task MSG_API_007_AC1_AMessageTheInboxDoesNotHoldIsAnswered404()
    {
        using ScratchFolder folder = new();
        await using InboxSite site = await InboxSite.StartAsync(folder.Path);
        string sms = await site.SmsAsync(Captured.Sms());
        string email = await site.EmailAsync(Captured.Email() with { Html = null });

        foreach (string route in new[] { "/messages/0123456789abcdef0123456789abcdef", "/messages/not-an-id", $"/messages/{sms}/eml", $"/messages/{sms}/html", $"/messages/{email}/html", "/messages/0123456789abcdef0123456789abcdef/eml" })
        {
            using HttpResponseMessage answer = await site.SendAsync(HttpMethod.Get, route);
            Assert.Equal(HttpStatusCode.NotFound, answer.StatusCode);
            Assert.Equal("messaging.inbox.notfound", JsonNode.Parse(await answer.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!["code"]!.GetValue<string>());
        }
    }

    [Fact]
    public async Task MSG_API_007_AC1_AnEmailsEmlIsAnsweredAsAnAttachmentNamedByItsId()
    {
        using ScratchFolder folder = new();
        await using InboxSite site = await InboxSite.StartAsync(folder.Path);
        string id = await site.EmailAsync(Captured.Email());

        using HttpResponseMessage answer = await site.SendAsync(HttpMethod.Get, $"/messages/{id}/eml");

        Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
        Assert.Equal("message/rfc822", answer.Content.Headers.ContentType!.MediaType);
        Assert.Equal(("attachment", id + ".eml"), (answer.Content.Headers.ContentDisposition!.DispositionType, answer.Content.Headers.ContentDisposition.FileName));
        Assert.Equal(
            await File.ReadAllBytesAsync(Directory.EnumerateFiles(folder.Path, "*.eml").Single(), TestContext.Current.CancellationToken),
            await answer.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MSG_API_007_AC1_ClearWithItsHeaderDeletesEveryRecordAndEml()
    {
        using ScratchFolder folder = new();
        await using InboxSite site = await InboxSite.StartAsync(folder.Path);
        await site.EmailAsync(Captured.Email());
        await site.SmsAsync(Captured.Sms());

        using HttpResponseMessage answer = await site.SendAsync(HttpMethod.Delete, "/messages", ("X-Hermes-Inbox", "clear"));

        Assert.Equal(HttpStatusCode.NoContent, answer.StatusCode);
        Assert.Empty(Directory.EnumerateFiles(folder.Path));
        Assert.Empty(await IdsAsync(site, "/messages"));
    }

    [Fact]
    public async Task MSG_API_007_AC3_OutsideDevelopmentTheGroupAnswersNoRoute()
    {
        using ScratchFolder folder = new();
        await using InboxSite site = await InboxSite.StartAsync(folder.Path, environment: "Production");

        foreach ((HttpMethod method, string route) in GroupRoutes.Every("0123456789abcdef0123456789abcdef"))
        {
            using HttpResponseMessage answer = await site.SendAsync(method, route, ("X-Hermes-Inbox", "clear"));
            Assert.Equal(HttpStatusCode.NotFound, answer.StatusCode);
        }

        Assert.False(Directory.Exists(folder.Path));
    }

    private static async Task<JsonObject> JsonAsync(InboxSite site, string route)
    {
        using HttpResponseMessage answer = await site.SendAsync(HttpMethod.Get, route);
        string text = await answer.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(answer.StatusCode == HttpStatusCode.OK, $"{route}: {(int)answer.StatusCode} {text}");
        return JsonNode.Parse(text)!.AsObject();
    }

    private static async Task<string[]> IdsAsync(InboxSite site, string route)
    {
        return [.. (await JsonAsync(site, route))["messages"]!.AsArray().Select(message => message!["id"]!.GetValue<string>())];
    }
}

/// <summary>Every route of the inbox's group, a message's routes for <c>id</c>.</summary>
internal static class GroupRoutes
{
    internal static (HttpMethod Method, string Route)[] Every(string id)
    {
        return
        [
            (HttpMethod.Get, "/"),
            (HttpMethod.Get, "/assets/inbox.js"),
            (HttpMethod.Get, "/assets/inbox.css"),
            (HttpMethod.Get, "/messages"),
            (HttpMethod.Get, "/messages/latest"),
            (HttpMethod.Get, "/messages/" + id),
            (HttpMethod.Get, $"/messages/{id}/eml"),
            (HttpMethod.Get, $"/messages/{id}/html"),
            (HttpMethod.Delete, "/messages"),
        ];
    }
}