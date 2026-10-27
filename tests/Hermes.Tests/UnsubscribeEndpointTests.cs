using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "integration")]
public sealed class UnsubscribeEndpointTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Address = "someone@example.com";

    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

    [Fact]
    public async Task MSG_API_005_AC1_TheOneClickPostSuppressesTheDestinationAndAnswersTheSameWithNoCookieAndNoRedirectWhetherOrNotAlreadySuppressed()
    {
        await using DispatchEngine engine = await StartAsync();
        using HttpClient client = engine.Host.Client();
        (SendReceipt receipt, string route) = await LetterAsync(engine.Host, Address);

        using HttpResponseMessage first = await Endpoints.SendAsync(client, HttpMethod.Post, route, content: OneClick());
        using MultipartFormDataContent multipart = new() { { new StringContent("One-Click"), "List-Unsubscribe" } };
        using HttpResponseMessage again = await Endpoints.SendAsync(client, HttpMethod.Post, route, content: multipart);
        await engine.Host.DispatchAsync();

        foreach (HttpResponseMessage response in new[] { first, again })
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
            Assert.False(response.Headers.Contains("Set-Cookie"));
            Assert.Null(response.Headers.Location);
        }

        Assert.Equal(DeliveryStatus.Suppressed, await SuppressionTests.StatusAsync(engine.Host, receipt));
        Assert.Empty(engine.Email.Received);
        Assert.Equal(1L, await SuppressionRowsAsync(engine.Host));
    }

    [Fact]
    public async Task MSG_API_005_AC2_AGetSuppressesNothingAndPostingItsFormSuppressesAndAnswersTheUnsubscribedPage()
    {
        await using DispatchEngine engine = await StartAsync();
        using HttpClient client = engine.Host.Client();
        (SendReceipt sent, string route) = await LetterAsync(engine.Host, Address);

        using HttpResponseMessage asked = await Endpoints.SendAsync(client, HttpMethod.Get, route);
        string asks = await asked.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        await engine.Host.DispatchAsync();
        using FormUrlEncodedContent form = new([new("page", "unsubscribe")]);
        using HttpResponseMessage posted = await Endpoints.SendAsync(client, HttpMethod.Post, route, content: form);
        string done = await posted.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        (SendReceipt later, _) = await LetterAsync(engine.Host, Address);
        await engine.Host.DispatchAsync();

        Assert.Equal(HttpStatusCode.OK, asked.StatusCode);
        Assert.Equal("text/html", asked.Content.Headers.ContentType?.MediaType);
        Assert.Contains("action=\"" + Endpoints.Prefix + route + "\"", asks, StringComparison.Ordinal);
        Assert.Contains("name=\"page\"", asks, StringComparison.Ordinal);
        Assert.Contains("News", asks, StringComparison.Ordinal);
        Assert.Equal(DeliveryStatus.Sent, await SuppressionTests.StatusAsync(engine.Host, sent));
        Assert.Equal(HttpStatusCode.OK, posted.StatusCode);
        Assert.Contains("no longer receive News messages", done, StringComparison.Ordinal);
        Assert.Equal(DeliveryStatus.Suppressed, await SuppressionTests.StatusAsync(engine.Host, later));
    }

    [Fact]
    public async Task MSG_API_005_AC2_TheUnsubscribedPageIsInTheLanguageTheTokenRecords()
    {
        await using DispatchEngine engine = await StartAsync();
        using HttpClient client = engine.Host.Client();
        (_, string route) = await LetterAsync(engine.Host, Address, language: "ar");

        using HttpResponseMessage asked = await Endpoints.SendAsync(client, HttpMethod.Get, route);
        string asks = await asked.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("lang=\"ar\"", asks, StringComparison.Ordinal);
        Assert.Contains("dir=\"rtl\"", asks, StringComparison.Ordinal);
        Assert.Contains("الأخبار", asks, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MSG_API_005_AC3_ATokenAlteredInAnyCharacterIs404AndSuppressesNothingSaveAFormatKeptForALaterReleaseWhichIs503AndRaisesNoAlert()
    {
        AlertSinkFake alerts = new();
        await using DispatchEngine engine = await StartAsync(services: services => services.AddSingleton<IAlertSink>(alerts));
        using HttpClient client = engine.Host.Client();
        (_, string route) = await LetterAsync(engine.Host, Address);
        string token = route["/unsubscribe/".Length..];

        int later = 0;
        for (int at = 0; at < token.Length; at++)
        {
            char changed = Alphabet[(Alphabet.IndexOf(token[at], StringComparison.Ordinal) + 1) % Alphabet.Length];
            string altered = string.Concat(token.AsSpan(0, at), changed.ToString(), token.AsSpan(at + 1));
            using HttpResponseMessage response = await OneClickAsync(client, "/unsubscribe/" + altered);
            if (IsLaterFormat(altered))
            {
                later++;
                Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
                Assert.Equal("messaging.request.unavailable", (await PreferenceEndpointTests.RefusalAsync(response)).Code);
                Assert.NotNull(response.Headers.RetryAfter);
            }
            else
            {
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                Assert.Equal("messaging.unsubscribe.invalid", (await PreferenceEndpointTests.RefusalAsync(response)).Code);
            }
        }

        Assert.True(later > 0);
        Assert.Equal(0L, await SuppressionRowsAsync(engine.Host));
        Assert.DoesNotContain(alerts.Calls, call => call.Raised && call.Condition == "key-unavailable");
    }

    [Fact]
    public async Task MSG_API_005_AC3_ATokenPastItsLifetimeIs404AndSuppressesNothing()
    {
        await using DispatchEngine engine = await StartAsync(configure: configuration => configuration["Hermes:Unsubscribe:TokenLifetime"] = "180.00:00:00");
        using HttpClient client = engine.Host.Client();
        (_, string route) = await LetterAsync(engine.Host, Address);

        engine.Host.Clock.Advance(TimeSpan.FromDays(181));
        using HttpResponseMessage response = await Endpoints.SendAsync(client, HttpMethod.Post, route, content: OneClick());
        using HttpResponseMessage page = await Endpoints.SendAsync(client, HttpMethod.Get, route);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, page.StatusCode);
        Assert.Contains("<html", await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Equal(0L, await SuppressionRowsAsync(engine.Host));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MSG_API_005_AC3_ATokenNamingACategoryNoLongerDeclaredOrNowEssentialIs404AndSuppressesNothing(bool essential)
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        string route;
        await using (DispatchEngine maker = await StartAsync(connection: connection))
        {
            (_, route) = await LetterAsync(maker.Host, Address);
        }

        await using EngineHost host = await EngineHost.StartAsync(
            connection,
            declare =>
            {
                EngineHost.Essential(declare);
                if (essential)
                {
                    declare.Category(UnsubscribeLinkTests.News).Essential();
                    declare.Kind(UnsubscribeLinkTests.Letter, UnsubscribeLinkTests.News).Email();
                }
            },
            map: application => application.MapHermesUnsubscribe(Endpoints.Prefix));
        using HttpClient client = host.Client();

        using HttpResponseMessage response = await Endpoints.SendAsync(client, HttpMethod.Post, route, content: OneClick());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0L, await SuppressionRowsAsync(host));
    }

    [Fact]
    public async Task MSG_API_005_AC4_ATokenMadeUnderOlderVersionsStillVerifiesAfterNewOnesAreAddedAndItsSuppressionAppliesToLaterSends()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        string route;
        await using (DispatchEngine maker = await StartAsync(connection: connection))
        {
            (_, route) = await LetterAsync(maker.Host, Address);
        }

        SecretSourceFake newer = Secrets.Held(connection)
            .With("tokenKeys", Secrets.Bytes(32, 3), Secrets.Bytes(32, 7))
            .With("hashKeys", Secrets.Bytes(32, 2), Secrets.Bytes(32, 8));
        await using DispatchEngine engine = await StartAsync(
            connection: connection,
            configure: configuration =>
            {
                configuration["Hermes:Secrets:TokenKeys:WriteVersion"] = "2";
                configuration["Hermes:Secrets:HashKeys:WriteVersion"] = "2";
            },
            services: services => services.AddSingleton<ISecretSource>(newer));
        using HttpClient client = engine.Host.Client();

        using HttpResponseMessage response = await Endpoints.SendAsync(client, HttpMethod.Post, route, content: OneClick());
        (SendReceipt later, _) = await LetterAsync(engine.Host, Address);
        await engine.Host.DispatchAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(DeliveryStatus.Suppressed, await SuppressionTests.StatusAsync(engine.Host, later));
    }

    [Fact]
    public async Task MSG_API_005_AC5_EachPageCarriesTheFourHeadersAndAPostBodyOver16KiBIs413()
    {
        await using DispatchEngine engine = await StartAsync();
        using HttpClient client = engine.Host.Client();
        (_, string route) = await LetterAsync(engine.Host, Address);

        using HttpResponseMessage large = await Endpoints.SendAsync(client, HttpMethod.Post, route, content: new FormUrlEncodedContent([new("List-Unsubscribe", "One-Click"), new("pad", new string('a', 16 * 1024))]));
        using HttpResponseMessage asks = await Endpoints.SendAsync(client, HttpMethod.Get, route);
        using HttpResponseMessage invalid = await Endpoints.SendAsync(client, HttpMethod.Get, route + "A");
        using HttpResponseMessage done = await Endpoints.SendAsync(client, HttpMethod.Post, route, content: new FormUrlEncodedContent([new("page", "unsubscribe")]));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, large.StatusCode);
        Assert.Equal("messaging.request.toolarge", (await PreferenceEndpointTests.RefusalAsync(large)).Code);
        foreach (HttpResponseMessage page in new[] { asks, invalid, done })
        {
            Assert.Equal("text/html", page.Content.Headers.ContentType?.MediaType);
            Assert.Equal("no-store", page.Headers.CacheControl?.ToString());
            Assert.Equal("no-referrer", Assert.Single(page.Headers.GetValues("Referrer-Policy")));
            Assert.Equal("nosniff", Assert.Single(page.Headers.GetValues("X-Content-Type-Options")));
            Assert.Equal(
                "default-src 'none'; style-src 'unsafe-inline'; form-action 'self'; base-uri 'none'; frame-ancestors 'none'",
                Assert.Single(page.Headers.GetValues("Content-Security-Policy")));
        }

        Assert.Equal(HttpStatusCode.NotFound, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.OK, done.StatusCode);
    }

    [Fact]
    public async Task MSG_API_005_AC6_ATokenWhoseHashKeysVersionIsMarkedRetiredButStillHeldSuppressesUnderThatVersionAndClearsItsMark()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        string route;
        await using (DispatchEngine maker = await StartAsync(connection: connection))
        {
            (_, route) = await LetterAsync(maker.Host, Address);
        }

        SecretSourceFake newer = Secrets.Held(connection).With("hashKeys", Secrets.Bytes(32, 2), Secrets.Bytes(32, 8));
        await using DispatchEngine engine = await StartAsync(
            connection: connection,
            configure: configuration =>
            {
                configuration["Hermes:Secrets:HashKeys:WriteVersion"] = "2";
                configuration["Hermes:Unsubscribe:TokenLifetime"] = "1000.00:00:00";
            },
            services: services => services.AddSingleton<ISecretSource>(newer));
        using HttpClient client = engine.Host.Client();
        await engine.Host.RunAsync(database => new NpgsqlCommand("UPDATE messaging.key_versions SET retired = true WHERE key = 'hashKeys' AND version = 1", database));
        engine.Host.Clock.Advance(TimeSpan.FromDays(1));

        using HttpResponseMessage response = await Endpoints.SendAsync(client, HttpMethod.Post, route, content: OneClick());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using NpgsqlConnection database = new(connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand suppression = new("SELECT hash_version FROM messaging.suppressions", database);
        Assert.Equal(1, (int)(await suppression.ExecuteScalarAsync(TestContext.Current.CancellationToken))!);
        await using NpgsqlCommand version = new("SELECT retired, last_made_at FROM messaging.key_versions WHERE key = 'hashKeys' AND version = 1", database);
        await using NpgsqlDataReader reader = await version.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.False(reader.GetBoolean(0));
        Assert.Equal(engine.Host.Clock.GetUtcNow(), new DateTimeOffset(reader.GetDateTime(1), TimeSpan.Zero));
    }

    [Fact]
    public async Task Post_ATokenNamingAVersionInUseButNotHeld_Is503AndRaisesKeyUnavailable_AndOneNamingAVersionNotInUseIs404()
    {
        AlertSinkFake alerts = new();
        await using DispatchEngine engine = await StartAsync(services: services => services.AddSingleton<IAlertSink>(alerts));
        using HttpClient client = engine.Host.Client();
        (_, string route) = await LetterAsync(engine.Host, Address);
        byte[] bytes = Base64Url.DecodeFromChars(route.AsSpan("/unsubscribe/".Length));
        bytes[4] = 2;
        string unheld = "/unsubscribe/" + Base64Url.EncodeToString(bytes);

        using HttpResponseMessage unknown = await Endpoints.SendAsync(client, HttpMethod.Post, unheld, content: OneClick());
        await engine.Host.RunAsync(database => new NpgsqlCommand(
            "INSERT INTO messaging.key_versions (key, version, check_value, first_made_at, last_made_at, retired, value_count) VALUES ('tokenKeys', 2, '\\x00', now(), now(), false, 0)",
            database));
        using HttpResponseMessage unavailable = await Endpoints.SendAsync(client, HttpMethod.Post, unheld, content: OneClick());
        using HttpResponseMessage page = await Endpoints.SendAsync(client, HttpMethod.Get, unheld);

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        Assert.Equal("messaging.request.unavailable", (await PreferenceEndpointTests.RefusalAsync(unavailable)).Code);
        Assert.NotNull(unavailable.Headers.RetryAfter);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, page.StatusCode);
        AlertSinkFake.AlertCall raised = Assert.Single(alerts.Calls, call => call.Raised && call.Condition == "key-unavailable");
        Assert.Equal("tokenKeys", raised.Scope);
        Assert.Equal(0L, await SuppressionRowsAsync(engine.Host));
    }

    [Fact]
    public async Task MSG_LIB_004_AC3_TheOneClickPostSucceedsInAnApplicationWhoseFallbackPolicyRequiresASignedInPersonAndWhichRegistersAntiforgery()
    {
        await using DispatchEngine engine = await StartAsync(
            services: services =>
            {
                services.AddAuthentication();
                services.AddAuthorization(authorization => authorization.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
                services.AddAntiforgery();
            },
            map: application =>
            {
                application.UseAuthentication();
                application.UseAuthorization();
                application.UseAntiforgery();
                application.MapHermesUnsubscribe(Endpoints.Prefix);
            });
        using HttpClient client = engine.Host.Client();
        (SendReceipt receipt, string route) = await LetterAsync(engine.Host, Address);

        using HttpResponseMessage response = await Endpoints.SendAsync(client, HttpMethod.Post, route, content: OneClick());
        await engine.Host.DispatchAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(DeliveryStatus.Suppressed, await SuppressionTests.StatusAsync(engine.Host, receipt));
    }

    /// <summary>Records a letter to <paramref name="address"/> and answers it with its unsubscribe route under the prefix.</summary>
    private static async Task<(SendReceipt Receipt, string Route)> LetterAsync(EngineHost host, string address, string language = "en")
    {
        SendReceipt receipt = (await host.SendAsync(UnsubscribeLinkTests.LetterTo(address) with { Language = language })).Value!;
        string link = (await host.OpenAsync(receipt.Deliveries[0].Delivery.Value, "unsubscribe"))!;
        return (receipt, link[UnsubscribeLinkTests.BaseAddress.Length..]);
    }

    private static async Task<HttpResponseMessage> OneClickAsync(HttpClient client, string route)
    {
        using FormUrlEncodedContent body = OneClick();
        return await Endpoints.SendAsync(client, HttpMethod.Post, route, content: body);
    }

    private static FormUrlEncodedContent OneClick()
    {
        return new FormUrlEncodedContent([new("List-Unsubscribe", "One-Click")]);
    }

    private static bool IsLaterFormat(string token)
    {
        byte[] bytes;
        try
        {
            bytes = Base64Url.DecodeFromChars(token);
        }
        catch (FormatException)
        {
            return false;
        }

        return bytes.Length > 0 && bytes[0] is >= 0x02 and <= 0x0F && Base64Url.EncodeToString(bytes) == token;
    }

    private Task<DispatchEngine> StartAsync(
        Action<Dictionary<string, string?>>? configure = null,
        Action<IServiceCollection>? services = null,
        string? connection = null,
        Action<WebApplication>? map = null)
    {
        return DispatchEngine.StartAsync(
            postgres,
            configure: configuration =>
            {
                UnsubscribeLinkTests.Configure(configuration);
                configure?.Invoke(configuration);
            },
            declare: UnsubscribeLinkTests.Declare,
            templates: UnsubscribeLinkTests.Templates,
            services: services,
            connection: connection,
            map: map ?? (application => application.MapHermesUnsubscribe(Endpoints.Prefix)));
    }

    private static async Task<long> SuppressionRowsAsync(EngineHost host)
    {
        await using NpgsqlConnection database = new(host.Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("SELECT count(*) FROM messaging.suppressions", database);
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }
}