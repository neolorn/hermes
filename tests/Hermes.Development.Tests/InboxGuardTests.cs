using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

using Xunit;

namespace Hermes.Development.Tests;

/// <summary>Who the inbox's group answers, the headers of its answers, and how it serves an email's HTML (<c>02</c> MSG-DEV-005).</summary>
[Trait("Category", "unit")]
public sealed class InboxGuardTests
{
    private const string PagePolicy = "default-src 'none'; script-src 'self'; style-src 'self'; img-src 'self' data: http: https:; connect-src 'self'; frame-src 'self'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'";

    private const string HtmlPolicy = "default-src 'none'; img-src data: http: https:; style-src 'unsafe-inline' http: https:; font-src data: http: https:; base-uri 'none'; form-action 'none'; frame-ancestors 'self'; sandbox allow-popups allow-popups-to-escape-sandbox";

    public static TheoryData<string, string> Strangers => new()
    {
        { InboxSite.RemoteHeader, "192.0.2.10" },
        { InboxSite.RemoteHeader, "2001:db8::10" },
        { InboxSite.RemoteHeader, "::ffff:192.0.2.10" },
        { "Host", "attacker.example" },
        { "Host", "attacker.example:5000" },
        { "Host", "localhost.attacker.example" },
        { "Host", "127.0.0.1.attacker.example" },
    };

    public static TheoryData<string, string> Locals => new()
    {
        { InboxSite.RemoteHeader, "127.0.0.1" },
        { InboxSite.RemoteHeader, "::1" },
        { InboxSite.RemoteHeader, "::ffff:127.0.0.1" },
        { "Host", "localhost" },
        { "Host", "LOCALHOST:5000" },
        { "Host", "127.0.0.1:1234" },
        { "Host", "[::1]:8080" },
    };

    [Theory]
    [MemberData(nameof(Strangers))]
    public async Task MSG_DEV_005_AC1_ARequestFromAnotherAddressOrUnderAnotherHostIsAnswered403OnEveryRoute(string header, string value)
    {
        using ScratchFolder folder = new();
        await using InboxSite site = await InboxSite.StartAsync(folder.Path);
        string id = await site.EmailAsync(Captured.Email());

        foreach ((HttpMethod method, string route) in GroupRoutes.Every(id))
        {
            using HttpResponseMessage answer = await site.SendAsync(method, route, (header, value), ("X-Hermes-Inbox", "clear"));
            Assert.True(answer.StatusCode == HttpStatusCode.Forbidden, $"{method} {route}: {(int)answer.StatusCode}");
            JsonObject body = JsonNode.Parse(await answer.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!.AsObject();
            Assert.Equal("messaging.inbox.notlocal", body["code"]!.GetValue<string>());
            Assert.Equal(["no-store", "nosniff", "no-referrer"], [answer.Headers.CacheControl!.ToString(), Header(answer, "X-Content-Type-Options"), Header(answer, "Referrer-Policy")]);
        }

        Assert.Single(Captured.In(folder.Path));
    }

    [Theory]
    [MemberData(nameof(Locals))]
    public async Task MSG_DEV_005_AC1_ARequestFromTheMachineUnderALoopbackNameIsAnswered(string header, string value)
    {
        using ScratchFolder folder = new();
        await using InboxSite site = await InboxSite.StartAsync(folder.Path);
        string id = await site.EmailAsync(Captured.Email());

        using HttpResponseMessage answer = await site.SendAsync(HttpMethod.Get, "/messages/" + id, (header, value));

        Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Strangers))]
    public async Task MSG_DEV_005_AC1_WithAllowRemoteOnARequestFromAnotherMachineIsAnsweredAsFromTheMachine(string header, string value)
    {
        using ScratchFolder folder = new();
        await using InboxSite site = await InboxSite.StartAsync(folder.Path, allowRemote: true);
        string id = await site.EmailAsync(Captured.Email());

        foreach ((HttpMethod method, string route) in GroupRoutes.Every(id).Where(route => route.Method == HttpMethod.Get))
        {
            using HttpResponseMessage answer = await site.SendAsync(method, route, (header, value));
            Assert.True(answer.StatusCode == HttpStatusCode.OK, $"{method} {route}: {(int)answer.StatusCode}");
        }

        using HttpResponseMessage cleared = await site.SendAsync(HttpMethod.Delete, "/messages", (header, value), ("X-Hermes-Inbox", "clear"));
        Assert.Equal(HttpStatusCode.NoContent, cleared.StatusCode);
    }

    [Fact]
    public async Task MSG_DEV_005_AC2_NoAnswerCarriesAnAccessControlHeaderEvenToARequestFromAnotherOrigin()
    {
        using ScratchFolder folder = new();
        await using InboxSite site = await InboxSite.StartAsync(folder.Path);
        string id = await site.EmailAsync(Captured.Email());

        List<HttpResponseMessage> answers = [];
        foreach ((HttpMethod method, string route) in GroupRoutes.Every(id))
        {
            answers.Add(await site.SendAsync(method, route, ("Origin", "https://attacker.example")));
        }

        answers.Add(await site.SendAsync(HttpMethod.Options, "/messages", ("Origin", "https://attacker.example"), ("Access-Control-Request-Method", "DELETE"), ("Access-Control-Request-Headers", "X-Hermes-Inbox")));
        try
        {
            Assert.All(answers, answer => Assert.DoesNotContain(answer.Headers.Concat(answer.Content.Headers), header => header.Key.StartsWith("Access-Control-", StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            answers.ForEach(answer => answer.Dispose());
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("yes")]
    [InlineData("Clear")]
    [InlineData("clear, clear")]
    public async Task MSG_DEV_005_AC2_ClearWithoutItsHeaderIsAnswered400AndClearsNothing(string? value)
    {
        using ScratchFolder folder = new();
        await using InboxSite site = await InboxSite.StartAsync(folder.Path);
        await site.EmailAsync(Captured.Email());

        using HttpResponseMessage answer = value is null
            ? await site.SendAsync(HttpMethod.Delete, "/messages")
            : await site.SendAsync(HttpMethod.Delete, "/messages", ("X-Hermes-Inbox", value));

        Assert.Equal(HttpStatusCode.BadRequest, answer.StatusCode);
        JsonObject body = JsonNode.Parse(await answer.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!.AsObject();
        Assert.Equal(("messaging.request.malformed", "X-Hermes-Inbox"), (body["code"]!.GetValue<string>(), body["details"]!["member"]!.GetValue<string>()));
        Assert.Single(Captured.In(folder.Path));
        Assert.Single(Directory.EnumerateFiles(folder.Path, "*.eml"));
    }

    [Theory]
    [InlineData("/", "text/html")]
    [InlineData("/assets/inbox.js", "text/javascript")]
    [InlineData("/assets/inbox.css", "text/css")]
    public async Task MSG_DEV_005_AC3_ThePageItsScriptAndItsStyleCarryThePagesPolicyAndHeaders(string route, string type)
    {
        using ScratchFolder folder = new();
        await using InboxSite site = await InboxSite.StartAsync(folder.Path);

        using HttpResponseMessage answer = await site.SendAsync(HttpMethod.Get, route);

        Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
        Assert.Equal(type, answer.Content.Headers.ContentType!.MediaType);
        Assert.Equal("utf-8", answer.Content.Headers.ContentType.CharSet);
        Assert.Equal(PagePolicy, Header(answer, "Content-Security-Policy"));
        Assert.Equal(["no-store", "nosniff", "no-referrer"], [answer.Headers.CacheControl!.ToString(), Header(answer, "X-Content-Type-Options"), Header(answer, "Referrer-Policy")]);
    }

    [Fact]
    public async Task MSG_DEV_005_AC3_EveryOtherAnswerCarriesTheThreeHeaders()
    {
        using ScratchFolder folder = new();
        await using InboxSite site = await InboxSite.StartAsync(folder.Path);
        string id = await site.EmailAsync(Captured.Email());

        foreach ((HttpMethod method, string route) in GroupRoutes.Every(id).Skip(3).Append((HttpMethod.Get, "/messages/0123456789abcdef0123456789abcdef")).Append((HttpMethod.Get, "/messages?limit=0")))
        {
            using HttpResponseMessage answer = await site.SendAsync(method, route);
            Assert.Equal(["no-store", "nosniff", "no-referrer"], [answer.Headers.CacheControl!.ToString(), Header(answer, "X-Content-Type-Options"), Header(answer, "Referrer-Policy")]);
        }
    }

    [Fact]
    public async Task MSG_DEV_005_AC3_AnEmailsHtmlCarriesItsPolicyAndHeaders()
    {
        using ScratchFolder folder = new();
        await using InboxSite site = await InboxSite.StartAsync(folder.Path);
        string id = await site.EmailAsync(Captured.Email());

        using HttpResponseMessage answer = await site.SendAsync(HttpMethod.Get, $"/messages/{id}/html");

        Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
        Assert.Equal(("text/html", "utf-8"), (answer.Content.Headers.ContentType!.MediaType, answer.Content.Headers.ContentType.CharSet));
        Assert.Equal(HtmlPolicy, Header(answer, "Content-Security-Policy"));
        Assert.Equal(["no-store", "nosniff", "no-referrer"], [answer.Headers.CacheControl!.ToString(), Header(answer, "X-Content-Type-Options"), Header(answer, "Referrer-Policy")]);
    }

    [Fact]
    public async Task MSG_DEV_005_AC4_AnEmailsHtmlHoldingAScriptIsServedSandboxedWithItsBaseAddedAndItsEmlUnchanged()
    {
        const string html = """<html><HEAD lang="en"><title>Receipt</title></HEAD><body><script>document.title = 'ran';</script><a href="https://bakery.example/receipts/7">Receipt</a></body></html>""";
        using ScratchFolder folder = new();
        await using InboxSite site = await InboxSite.StartAsync(folder.Path);
        string id = await site.EmailAsync(Captured.Email() with { Html = html });
        string eml = Directory.EnumerateFiles(folder.Path, "*.eml").Single();
        string record = Directory.EnumerateFiles(folder.Path, "*.json").Single();
        byte[] emlBefore = await File.ReadAllBytesAsync(eml, TestContext.Current.CancellationToken);
        byte[] recordBefore = await File.ReadAllBytesAsync(record, TestContext.Current.CancellationToken);

        using HttpResponseMessage answer = await site.SendAsync(HttpMethod.Get, $"/messages/{id}/html");

        string served = await answer.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(html.Replace("<HEAD lang=\"en\">", "<HEAD lang=\"en\"><base target=\"_blank\">", StringComparison.Ordinal), served);
        string policy = Header(answer, "Content-Security-Policy");
        Assert.DoesNotContain("script-src", policy, StringComparison.Ordinal);
        Assert.StartsWith("default-src 'none';", policy, StringComparison.Ordinal);
        Assert.EndsWith("sandbox allow-popups allow-popups-to-escape-sandbox", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("allow-scripts", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("allow-same-origin", policy, StringComparison.Ordinal);
        Assert.Equal(emlBefore, await File.ReadAllBytesAsync(eml, TestContext.Current.CancellationToken));
        Assert.Equal(recordBefore, await File.ReadAllBytesAsync(record, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("<p>No head</p>", "<base target=\"_blank\"><p>No head</p>")]
    [InlineData("<!DOCTYPE html><header>Top</header>", "<!DOCTYPE html><base target=\"_blank\"><header>Top</header>")]
    [InlineData("<html><head><title>T</title></head></html>", "<html><head><base target=\"_blank\"><title>T</title></head></html>")]
    public async Task MSG_DEV_005_AC4_TheBaseIsPutAtTheStartOfTheHeadOrOfTheDocument(string html, string served)
    {
        using ScratchFolder folder = new();
        await using InboxSite site = await InboxSite.StartAsync(folder.Path);
        string id = await site.EmailAsync(Captured.Email() with { Html = html });

        using HttpResponseMessage answer = await site.SendAsync(HttpMethod.Get, $"/messages/{id}/html");

        Assert.Equal(served, await answer.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private static string Header(HttpResponseMessage answer, string name)
    {
        return answer.Headers.TryGetValues(name, out IEnumerable<string>? values) || answer.Content.Headers.TryGetValues(name, out values)
            ? string.Join(", ", values)
            : string.Empty;
    }
}