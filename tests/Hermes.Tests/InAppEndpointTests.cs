using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.AspNetCore.Builder;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "integration")]
public sealed class InAppEndpointTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Other = "person-8";

    [Fact]
    public async Task MSG_API_008_AC1_ASignedInPersonsGetWithAcceptLanguageArThenEnAnswersTheirEntriesNewestFirstInArabicAndNoneOfAnotherPersons()
    {
        await using DispatchEngine engine = await StartAsync();
        await SendAsync(engine, InApp.Person, "A-1");
        await SendAsync(engine, Other, "B-1");
        await SendAsync(engine, InApp.Person, "A-2");
        using HttpClient client = engine.Host.Client();
        using HttpRequestMessage request = new(HttpMethod.Get, Endpoints.Prefix + "/inapp");
        request.Headers.Add(RecipientIdentityFake.Header, InApp.Person);
        request.Headers.Add("Accept-Language", "ar, en;q=0.5");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        JsonElement[] entries = [.. body.RootElement.GetProperty("entries").EnumerateArray()];
        Assert.Equal(["Order A-2 ar ar", "Order A-1 ar ar"], entries.Select(entry => entry.GetProperty("title").GetString()));
        Assert.All(entries, entry => Assert.Equal("ar", entry.GetProperty("language").GetString()));
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("next").ValueKind);
        JsonElement first = entries[0];
        Assert.Matches("^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[0-9a-f]{4}-[0-9a-f]{12}$", first.GetProperty("id").GetString());
        Assert.Equal((InApp.Kind, InApp.Category, InApp.Target, "Body ar rtl"), (first.GetProperty("kind").GetString(), first.GetProperty("category").GetString(), first.GetProperty("target").GetString(), first.GetProperty("body").GetString()));
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$", first.GetProperty("writtenAt").GetString());
        Assert.Equal(JsonValueKind.Null, first.GetProperty("readAt").ValueKind);
        Assert.Equal("A-2", first.GetProperty("targetValues").GetProperty("orderId").GetString());
    }

    [Fact]
    public async Task MSG_API_008_AC2_MarkingAnotherPersonsEntryReadAnswers404AndChangesNothingAndTheirOwn204AndTheUnreadCountThenOneFewer()
    {
        await using DispatchEngine engine = await StartAsync();
        Guid mine = await SendAsync(engine, InApp.Person, "A-1");
        await SendAsync(engine, InApp.Person, "A-2");
        Guid theirs = await SendAsync(engine, Other, "B-1");
        using HttpClient client = engine.Host.Client();

        int before = await UnreadAsync(client, InApp.Person);
        using HttpResponseMessage other = await Endpoints.SendAsync(client, HttpMethod.Post, $"/inapp/{theirs:D}/read", InApp.Person, Endpoints.Json("{}"));
        using HttpResponseMessage unknown = await Endpoints.SendAsync(client, HttpMethod.Post, "/inapp/not-an-id/read", InApp.Person, Endpoints.Json("{}"));
        using HttpResponseMessage own = await Endpoints.SendAsync(client, HttpMethod.Post, $"/inapp/{mine:D}/read", InApp.Person, Endpoints.Json("{}"));
        using HttpResponseMessage again = await Endpoints.SendAsync(client, HttpMethod.Post, $"/inapp/{mine:D}/read", InApp.Person, Endpoints.Json("{ }"));

        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        Assert.Equal("messaging.inapp.unknown", (await PreferenceEndpointTests.RefusalAsync(other)).Code);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(1, await UnreadAsync(client, Other));
        Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.NoContent), (own.StatusCode, again.StatusCode));
        Assert.Equal((2, 1), (before, await UnreadAsync(client, InApp.Person)));
    }

    [Fact]
    public async Task MSG_API_008_AC3_APostOfInappReadSentAsAFormIsAnswered415AndMarksNothing()
    {
        await using DispatchEngine engine = await StartAsync();
        await SendAsync(engine, InApp.Person, "A-1");
        using HttpClient client = engine.Host.Client();

        using FormUrlEncodedContent form = new([new("all", "true")]);
        using HttpResponseMessage formed = await Endpoints.SendAsync(client, HttpMethod.Post, "/inapp/read", InApp.Person, form);
        int after = await UnreadAsync(client, InApp.Person);
        using HttpResponseMessage json = await Endpoints.SendAsync(client, HttpMethod.Post, "/inapp/read", InApp.Person, Endpoints.Json("{}"));

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, formed.StatusCode);
        Assert.Equal("messaging.request.mediatype", (await PreferenceEndpointTests.RefusalAsync(formed)).Code);
        Assert.Equal(1, after);
        Assert.Equal(HttpStatusCode.NoContent, json.StatusCode);
        Assert.Equal(0, await UnreadAsync(client, InApp.Person));
    }

    [Theory]
    [InlineData("GET", "/inapp")]
    [InlineData("GET", "/inapp/unread")]
    [InlineData("POST", "/inapp/read")]
    [InlineData("POST", "/inapp/0190f0a0-0000-7000-8000-000000000000/read")]
    public async Task MSG_API_002_AC1_AnInAppRouteWithNoSignedInPersonAnswers401(string method, string route)
    {
        await using DispatchEngine engine = await StartAsync();
        using HttpClient client = engine.Host.Client();

        using HttpResponseMessage response = await Endpoints.SendAsync(client, new HttpMethod(method), route, content: method == "POST" ? Endpoints.Json("{}") : null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("?size=0", "messaging.request.malformed")]
    [InlineData("?size=101", "messaging.request.malformed")]
    [InlineData("?size=ten", "messaging.request.malformed")]
    [InlineData("?cursor=nothing", "messaging.inapp.cursorinvalid")]
    public async Task MSG_API_008_ASizeOutsideItsRuleOrACursorThatDoesNotReadAnswers400(string query, string code)
    {
        await using DispatchEngine engine = await StartAsync();
        using HttpClient client = engine.Host.Client();

        using HttpResponseMessage response = await Endpoints.SendAsync(client, HttpMethod.Get, "/inapp" + query, InApp.Person);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(code, (await PreferenceEndpointTests.RefusalAsync(response)).Code);
    }

    [Fact]
    public async Task MSG_API_008_ThePagesCursorReadsTheNextPage()
    {
        await using DispatchEngine engine = await StartAsync();
        await SendAsync(engine, InApp.Person, "A-1");
        await SendAsync(engine, InApp.Person, "A-2");
        using HttpClient client = engine.Host.Client();

        using HttpResponseMessage first = await Endpoints.SendAsync(client, HttpMethod.Get, "/inapp?size=1", InApp.Person);
        using JsonDocument page = JsonDocument.Parse(await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        string next = page.RootElement.GetProperty("next").GetString()!;
        using HttpResponseMessage second = await Endpoints.SendAsync(client, HttpMethod.Get, "/inapp?size=1&cursor=" + Uri.EscapeDataString(next), InApp.Person);
        using JsonDocument last = JsonDocument.Parse(await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal("Order A-2 en en", page.RootElement.GetProperty("entries")[0].GetProperty("title").GetString());
        Assert.Equal("Order A-1 en en", Assert.Single(last.RootElement.GetProperty("entries").EnumerateArray()).GetProperty("title").GetString());
        Assert.Equal(JsonValueKind.Null, last.RootElement.GetProperty("next").ValueKind);
    }

    private async Task<DispatchEngine> StartAsync()
    {
        return await DispatchEngine.StartAsync(
            postgres,
            declare: InApp.Declare,
            templates: InApp.Templates,
            services: Endpoints.Identity,
            map: application => application.MapHermesInApp(Endpoints.Prefix));
    }

    /// <summary>Sends and dispatches an in-app delivery to <paramref name="person"/>, a second after the last, answering its entry.</summary>
    private static async Task<Guid> SendAsync(DispatchEngine engine, string person, string order)
    {
        engine.Host.Clock.Advance(TimeSpan.FromSeconds(1));
        Guid delivery = (await engine.Host.SendAsync(InApp.Send(person: person, order: order))).Value!.Deliveries[0].Delivery.Value;
        Assert.Equal(1, await engine.Host.DispatchAsync());
        return Guid.Parse((await engine.Host.ColumnAsync<string>(delivery, "provider_reference"))!);
    }

    private static async Task<int> UnreadAsync(HttpClient client, string person)
    {
        using HttpResponseMessage response = await Endpoints.SendAsync(client, HttpMethod.Get, "/inapp/unread", person);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return body.RootElement.GetProperty("unread").GetInt32();
    }
}