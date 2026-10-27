using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Sending;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "integration")]
public sealed class PreferenceEndpointTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Person = "person-1";

    private const string OffersSms = "/preferences/" + SuppressionTests.Offers + "/sms";

    [Fact]
    public async Task MSG_API_003_AC1_APutTurningACategorysSmsOffIsReadBackByGetAndStopsTheirSms()
    {
        await using DispatchEngine engine = await StartAsync();
        using HttpClient client = engine.Host.Client();

        using HttpResponseMessage put = await Endpoints.SendAsync(client, HttpMethod.Put, OffersSms, Person, Endpoints.Json("""{ "on": false }"""));
        using JsonDocument read = await GetAsync(client, Person);
        SendReceipt receipt = (await engine.Host.SendAsync(SuppressionTests.OfferTo("+201000000001"))).Value!;
        await engine.Host.DispatchAsync();

        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);
        JsonElement categories = read.RootElement.GetProperty("categories");
        Assert.Equal([EngineHost.Category, SuppressionTests.Offers], categories.EnumerateArray().Select(category => category.GetProperty("name").GetString()));
        Assert.Equal([true, false], categories.EnumerateArray().Select(category => category.GetProperty("essential").GetBoolean()));
        Assert.Equal(
            [("email", true), ("sms", true), ("push", true)],
            categories[0].GetProperty("channels").EnumerateArray().Select(channel => (channel.GetProperty("channel").GetString(), channel.GetProperty("on").GetBoolean())));
        Assert.Equal([("sms", false)], categories[1].GetProperty("channels").EnumerateArray().Select(channel => (channel.GetProperty("channel").GetString(), channel.GetProperty("on").GetBoolean())));
        Assert.Equal(DeliveryStatus.OptedOut, await SuppressionTests.StatusAsync(engine.Host, receipt));
        Assert.Empty(engine.Sms.Received);
    }

    [Fact]
    public async Task MSG_API_001_AC1_WithTheApplicationsJsonOptionsSetToAnotherNamingPolicyTheEndpointsReadAndAnswerCamelCase()
    {
        await using DispatchEngine engine = await StartAsync(services => services.ConfigureHttpJsonOptions(json =>
        {
            json.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.KebabCaseUpper;
            json.SerializerOptions.PropertyNameCaseInsensitive = true;
        }));
        using HttpClient client = engine.Host.Client();

        using HttpResponseMessage put = await Endpoints.SendAsync(client, HttpMethod.Put, OffersSms, Person, Endpoints.Json("""{ "on": false }"""));
        using HttpResponseMessage upper = await Endpoints.SendAsync(client, HttpMethod.Put, OffersSms, Person, Endpoints.Json("""{ "ON": true }"""));
        using JsonDocument read = await GetAsync(client, Person);

        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, upper.StatusCode);
        Assert.Equal(("messaging.request.malformed", "on"), await RefusalAsync(upper, "member"));
        Assert.False(read.RootElement.GetProperty("categories")[1].GetProperty("channels")[0].GetProperty("on").GetBoolean());
    }

    [Fact]
    public async Task MSG_API_001_AC2_AMalformedBodyIs400AFormBodyToAJsonRoute415AndA17KiBBody413EachWithItsCode()
    {
        await using DispatchEngine engine = await StartAsync();
        using HttpClient client = engine.Host.Client();

        using HttpResponseMessage malformed = await Endpoints.SendAsync(client, HttpMethod.Put, OffersSms, Person, Endpoints.Json("""{ "on": "no" }"""));
        using HttpResponseMessage unread = await Endpoints.SendAsync(client, HttpMethod.Put, OffersSms, Person, Endpoints.Json("""{ "on": """));
        using FormUrlEncodedContent form = new([new("on", "false")]);
        using HttpResponseMessage formed = await Endpoints.SendAsync(client, HttpMethod.Put, OffersSms, Person, form);
        using HttpResponseMessage large = await Endpoints.SendAsync(client, HttpMethod.Put, OffersSms, Person, Endpoints.Json($$"""{ "on": false, "pad": "{{new string('a', 17 * 1024)}}" }"""));

        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal(("messaging.request.malformed", "on"), await RefusalAsync(malformed, "member"));
        Assert.Equal(HttpStatusCode.BadRequest, unread.StatusCode);
        Assert.Equal("messaging.request.malformed", (await RefusalAsync(unread)).Code);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, formed.StatusCode);
        Assert.Equal("messaging.request.mediatype", (await RefusalAsync(formed)).Code);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, large.StatusCode);
        Assert.Equal("messaging.request.toolarge", (await RefusalAsync(large)).Code);
        Assert.Equal(0L, await PreferenceRowsAsync(engine.Host));
    }

    [Fact]
    public async Task MSG_API_002_AC1_ARequestWithNoSignedInPersonToAPreferencesRouteIs401()
    {
        await using DispatchEngine engine = await StartAsync();
        using HttpClient client = engine.Host.Client();

        using HttpResponseMessage get = await Endpoints.SendAsync(client, HttpMethod.Get, "/preferences");
        using HttpResponseMessage put = await Endpoints.SendAsync(client, HttpMethod.Put, OffersSms, content: Endpoints.Json("""{ "on": false }"""));

        Assert.Equal(HttpStatusCode.Unauthorized, get.StatusCode);
        Assert.Equal("messaging.request.unauthenticated", (await RefusalAsync(get)).Code);
        Assert.Equal(HttpStatusCode.Unauthorized, put.StatusCode);
        Assert.Equal("messaging.request.unauthenticated", (await RefusalAsync(put)).Code);
        Assert.Equal(0L, await PreferenceRowsAsync(engine.Host));
    }

    [Fact]
    public async Task MSG_API_002_AC2_NoPreferencesRouteReadsOrChangesAnotherPersonsPreferences()
    {
        await using DispatchEngine engine = await StartAsync();
        using HttpClient client = engine.Host.Client();

        using HttpResponseMessage put = await Endpoints.SendAsync(client, HttpMethod.Put, OffersSms, Person, Endpoints.Json("""{ "on": false }"""));
        using JsonDocument other = await GetAsync(client, "person-2");
        SendReceipt receipt = (await engine.Host.SendAsync(SuppressionTests.OfferTo("+201000000002") with { Recipient = new Recipient.Person("person-2") })).Value!;
        await engine.Host.DispatchAsync();

        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);
        Assert.True(other.RootElement.GetProperty("categories")[1].GetProperty("channels")[0].GetProperty("on").GetBoolean());
        Assert.Equal(DeliveryStatus.Sent, await SuppressionTests.StatusAsync(engine.Host, receipt));
    }

    [Theory]
    [InlineData("/preferences/" + EngineHost.Category + "/sms", HttpStatusCode.UnprocessableEntity, "messaging.preference.essential")]
    [InlineData("/preferences/unknown/sms", HttpStatusCode.NotFound, "messaging.preference.unknown")]
    [InlineData("/preferences/" + SuppressionTests.Offers + "/email", HttpStatusCode.NotFound, "messaging.preference.unknown")]
    [InlineData("/preferences/" + SuppressionTests.Offers + "/fax", HttpStatusCode.NotFound, "messaging.preference.unknown")]
    public async Task Put_AnEssentialOrUnknownCategoryOrChannel_IsRefusedWithItsCodeAndChangesNothing(string route, HttpStatusCode status, string code)
    {
        await using DispatchEngine engine = await StartAsync();
        using HttpClient client = engine.Host.Client();

        using HttpResponseMessage put = await Endpoints.SendAsync(client, HttpMethod.Put, route, Person, Endpoints.Json("""{ "on": false }"""));

        Assert.Equal(status, put.StatusCode);
        Assert.Equal(code, (await RefusalAsync(put)).Code);
        Assert.Equal(0L, await PreferenceRowsAsync(engine.Host));
    }

    [Fact]
    public async Task Put_ThePersonsLocksNotHadWithinTheDispatchTimeout_Is503WithRetryAfterOfTheDispatchInterval()
    {
        await using DispatchEngine engine = await StartAsync(configure: configuration =>
        {
            configuration["Hermes:Dispatch:Timeout"] = "00:00:01";
            configuration["Hermes:Dispatch:Interval"] = "00:00:03";
        });
        using HttpClient client = engine.Host.Client();
        KeyedHashes hashes = engine.Host.Services.GetRequiredService<KeyedHashes>();
        await using NpgsqlConnection holder = new(engine.Host.Connection);
        await holder.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlTransaction transaction = await holder.BeginTransactionAsync(TestContext.Current.CancellationToken);
        foreach (long key in AdvisoryLocks.Person(hashes, Person))
        {
            await using NpgsqlCommand command = new("SELECT pg_advisory_xact_lock(@key)", holder, transaction);
            command.Parameters.AddWithValue("key", key);
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        using HttpResponseMessage put = await Endpoints.SendAsync(client, HttpMethod.Put, OffersSms, Person, Endpoints.Json("""{ "on": false }"""));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, put.StatusCode);
        Assert.Equal("messaging.request.unavailable", (await RefusalAsync(put)).Code);
        Assert.Equal(TimeSpan.FromSeconds(3), put.Headers.RetryAfter?.Delta);
    }

    [Fact]
    public async Task MSG_LIB_004_AC2_AGroupMappedUnderNotificationsAnswersThereAndNowhereElse()
    {
        await using DispatchEngine engine = await StartAsync();
        using HttpClient client = engine.Host.Client();

        using HttpResponseMessage there = await Endpoints.SendAsync(client, HttpMethod.Get, "/preferences", Person);
        using HttpResponseMessage elsewhere = await Endpoints.SendAsync(client, HttpMethod.Get, "/preferences", Person, prefix: string.Empty);

        Assert.Equal(HttpStatusCode.OK, there.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, elsewhere.StatusCode);
    }

    internal static async Task<(string Code, string? Detail)> RefusalAsync(HttpResponseMessage response, string? detail = null)
    {
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        JsonElement details = body.RootElement.GetProperty("details");
        return (body.RootElement.GetProperty("code").GetString()!, detail is null ? null : details.GetProperty(detail).GetString());
    }

    private Task<DispatchEngine> StartAsync(Action<IServiceCollection>? services = null, Action<System.Collections.Generic.Dictionary<string, string?>>? configure = null)
    {
        return DispatchEngine.StartAsync(
            postgres,
            configure: configure,
            declare: SuppressionTests.Declare,
            services: collection =>
            {
                Endpoints.Identity(collection);
                services?.Invoke(collection);
            },
            map: application => application.MapHermesPreferences(Endpoints.Prefix));
    }

    private static async Task<JsonDocument> GetAsync(HttpClient client, string person)
    {
        using HttpResponseMessage response = await Endpoints.SendAsync(client, HttpMethod.Get, "/preferences", person);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private static async Task<long> PreferenceRowsAsync(EngineHost host)
    {
        await using NpgsqlConnection database = new(host.Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("SELECT count(*) FROM messaging.preferences", database);
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }
}