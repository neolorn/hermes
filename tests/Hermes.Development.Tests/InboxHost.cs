using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Testing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Xunit;

namespace Hermes.Development.Tests;

/// <summary>Where a host registers its own providers against the inbox's call.</summary>
public enum HostOrder
{
    /// <summary>The host's providers before <c>AddHermesDevelopmentInbox</c>.</summary>
    ProvidersBefore,

    /// <summary>The host's providers after <c>AddHermesDevelopmentInbox</c>.</summary>
    ProvidersAfter,
}

/// <summary>
/// A web host on the loopback address that sends through the engine, maps the push
/// subscription group and the inbox's group under <c>/dev/inbox</c>, and registers its own
/// providers of the three channels around <c>AddHermesDevelopmentInbox</c>.
/// </summary>
internal sealed class InboxHost : IAsyncDisposable
{
    internal const string Prefix = "/dev/inbox";

    private InboxHost(WebApplication app, HostProviders providers, LogSink logs)
    {
        App = app;
        Providers = providers;
        Logs = logs;
        Client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
    }

    internal WebApplication App { get; }

    internal HostProviders Providers { get; }

    internal LogSink Logs { get; }

    internal HttpClient Client { get; }

    /// <summary>Starts a host whose environment is <paramref name="environment"/> on a database of its own, its inbox in <paramref name="folder"/>.</summary>
    internal static async Task<InboxHost> StartAsync(
        PostgresFixture postgres,
        string folder,
        HostOrder order = HostOrder.ProvidersBefore,
        string? environment = "Development",
        Action<Dictionary<string, string?>>? configure = null,
        Action<IServiceCollection>? services = null)
    {
        string connection = await postgres.NewDatabaseAsync();
        await MessagingTestDatabase.MigrateAsync(connection, TestContext.Current.CancellationToken);
        Dictionary<string, string?> configuration = new(StringComparer.Ordinal)
        {
            ["Hermes:Languages:0"] = "en",
            ["Hermes:Languages:1"] = "ar",
            ["Hermes:DefaultLanguage"] = "en",
            ["Hermes:Email:From:Address"] = "orders@bakery.example",
            ["Hermes:Email:From:DisplayName"] = "Bakery",
            ["Hermes:Sms:Sender"] = "Bakery",
            ["Hermes:Sms:AllowedPrefixes:0"] = "+20",
            ["Hermes:DevelopmentInbox:Folder"] = folder,
        };
        configure?.Invoke(configuration);

        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(configuration);
        LogSink logs = new();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(logs);

        FixedSecretSource secrets = new();
        secrets.SetSecret("connection", new SecretVersions { Versions = [new SecretVersion { Version = 1, Value = Encoding.UTF8.GetBytes(connection) }] });
        HostProviders providers = new();
        if (order == HostOrder.ProvidersBefore)
        {
            Register(builder.Services, providers);
        }

        builder.AddHermesDevelopmentInbox();
        if (order == HostOrder.ProvidersAfter)
        {
            Register(builder.Services, providers);
        }

        builder.Services.AddHermes(builder.Configuration, declare =>
        {
            declare.Category("orders").Essential();
            declare.Kind("order.ready", "orders").Email().Sms().Push();
        });
        builder.Services.AddSingleton<ISecretSource>(secrets);
        builder.Services.AddSingleton<ITemplateSource>(new TemplateSourceFake());
        builder.Services.AddSingleton<IRecipientIdentity>(new IdentityFake("person-1"));
        services?.Invoke(builder.Services);

        WebApplication app = builder.Build();
        app.MapHermesPushSubscriptions("/notifications");
        app.MapHermesDevelopmentInbox(Prefix);
        try
        {
            await app.StartAsync(TestContext.Current.CancellationToken);
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }

        return new InboxHost(app, providers, logs);
    }

    /// <summary>Subscribes the signed-in person to push from an endpoint of FCM, as a browser does, and answers the endpoint.</summary>
    internal async Task<string> SubscribeAsync()
    {
        JsonObject key = (await Client.GetFromJsonAsync<JsonObject>("/notifications/push/key", TestContext.Current.CancellationToken))!;
        using ECDiffieHellman browser = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        ECParameters point = browser.ExportParameters(includePrivateParameters: false);
        byte[] p256dh = [0x04, .. point.Q.X!, .. point.Q.Y!];
        string endpoint = "https://fcm.googleapis.com/fcm/send/" + Guid.NewGuid().ToString("N");
        JsonObject body = new()
        {
            ["endpoint"] = endpoint,
            ["expirationTime"] = null,
            ["keys"] = new JsonObject { ["p256dh"] = Base64Url.EncodeToString(p256dh), ["auth"] = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16)) },
            ["applicationServerKey"] = key["publicKey"]!.GetValue<string>(),
        };
        using HttpResponseMessage answer = await Client.PostAsJsonAsync("/notifications/push/subscriptions", body, TestContext.Current.CancellationToken);
        Assert.True(answer.IsSuccessStatusCode, await answer.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return endpoint;
    }

    /// <summary>Sends the kind <c>order.ready</c> on <paramref name="channels"/> to <c>person-1</c>, in <paramref name="language"/>.</summary>
    internal async Task<SendReceipt> SendAsync(IReadOnlyList<Channel> channels, string? language = null)
    {
        AsyncServiceScope scope = App.Services.CreateAsyncScope();
        await using (scope)
        {
            Result<SendReceipt> sent = await scope.ServiceProvider.GetRequiredService<IMessages>().SendAsync(
                new Send
                {
                    Kind = "order.ready",
                    Recipient = new Recipient.Person("person-1"),
                    Email = "someone@example.com",
                    Phone = "+201000000001",
                    Language = language,
                    Values = new Dictionary<string, SendValue>(StringComparer.Ordinal) { ["code"] = new SendValue.PlainText("45K1") },
                    Channels = channels,
                },
                TestContext.Current.CancellationToken);
            Assert.True(sent.IsSuccess, sent.Failure?.Code);
            return sent.Value!;
        }
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await App.StopAsync(TestContext.Current.CancellationToken);
        await App.DisposeAsync();
        Logs.Check();
    }

    /// <summary>Registers the host's own providers of the three channels, each by its contract.</summary>
    internal static void Register(IServiceCollection services, HostProviders providers)
    {
        services.AddSingleton<IEmailProvider>(providers.Email);
        services.AddSingleton<ISmsProvider>(providers.Sms);
        services.AddSingleton<IPushProvider>(providers.Push);
    }
}