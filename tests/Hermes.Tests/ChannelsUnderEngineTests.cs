using System;
using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Operations;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "integration")]
public sealed class ChannelsUnderEngineTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Endpoint = "https://web.push.example.net/push/d4c1b7e09a5f4e3c8b2a6f1e0d9c8b7a";

    [Fact]
    public async Task MSG_LIB_003_AC3_AnEmailOnlyApplicationStartsWithNoSmsOrPushKeyAndNoSmsOrPushSecret()
    {
        string connection = await MigratedAsync();
        Dictionary<string, string?> configuration = TestHost.Languages();
        configuration.Remove("Hermes:Sms:Sender");
        configuration.Remove("Hermes:Sms:AllowedPrefixes:0");
        configuration.Remove("Hermes:Push:Subject");
        configuration["Hermes:PublicBaseAddress"] = "https://example.test/";
        SecretSourceFake secrets = Secrets.Held(connection).Without("pushVapidKeys");
        using IHost host = TestHost.Build(
            configuration,
            declare =>
            {
                declare.Category("news").Label("en", "News");
                declare.Kind("news.issued", "news").Email();
            },
            services => services.AddSingleton<ISecretSource>(secrets));

        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(secrets.Read, name => name is "pushVapidKeys" or "smsReportKey" or "smsWe");
    }

    [Fact]
    public async Task SendAsync_ShippedPushUnderTheEngine_SignsWithTheHeldVapidKeyAndTheConfiguredSubject()
    {
        RecordingHandler handler = new();
        using IHost host = await StartedAsync(handler.Handler, configure: null);

        Attempt attempt = await host.Services.GetRequiredService<IPushProvider>().SendAsync(Request(), TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new Attempt.Accepted(null), attempt);
        string authorization = Assert.Single(handler.Authorizations);
        Assert.EndsWith("k=" + Base64Url.EncodeToString(VapidPublicKey(Convert.FromHexString(Secrets.VapidScalar))), authorization, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_ShippedPushUnderTheEngine_TimesOutAfterTheEnginesDispatchTimeout()
    {
        using IHost host = await StartedAsync(() => new HangingHandler(), configuration => configuration["Hermes:Dispatch:Timeout"] = "00:00:01");
        Stopwatch watch = Stopwatch.StartNew();

        Attempt attempt = await host.Services.GetRequiredService<IPushProvider>().SendAsync(Request(), TestContext.Current.CancellationToken);

        watch.Stop();
        await host.StopAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new Attempt.Transient(AttemptReason.Timeout, null, null), attempt);
        Assert.InRange(watch.Elapsed, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));
    }

    private static PushRequest.WebPush Request()
    {
        using ECDiffieHellman receiver = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        ECParameters point = receiver.ExportParameters(false);
        return new PushRequest.WebPush(
            Endpoint,
            Base64Url.EncodeToString([0x04, .. point.Q.X!, .. point.Q.Y!]),
            Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16)),
            1,
            "{\"title\":\"Signed in\"}"u8.ToArray(),
            TimeSpan.FromMinutes(10),
            PushUrgency.High,
            null);
    }

    private static byte[] VapidPublicKey(byte[] scalar)
    {
        using ECDsa key = ECDsa.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, D = scalar });
        ECParameters parameters = key.ExportParameters(false);
        return [0x04, .. parameters.Q.X!, .. parameters.Q.Y!];
    }

    private async Task<IHost> StartedAsync(Func<HttpMessageHandler> primary, Action<Dictionary<string, string?>>? configure)
    {
        string connection = await MigratedAsync();
        Dictionary<string, string?> configuration = TestHost.Languages();
        configure?.Invoke(configuration);
        IHost host = TestHost.Build(
            configuration,
            declare =>
            {
                declare.Category("security").Essential();
                declare.Kind("sign.in", "security").Push();
            },
            services => services.AddSingleton<ISecretSource>(Secrets.Held(connection)),
            services => services.AddHttpClient("Hermes.Push").ConfigurePrimaryHttpMessageHandler(primary));
        await host.StartAsync(TestContext.Current.CancellationToken);
        return host;
    }

    private async Task<string> MigratedAsync()
    {
        string connection = await postgres.NewDatabaseAsync();
        Assert.Null(await SchemaMigration.ApplyAsync(connection, [], TestContext.Current.CancellationToken));
        return connection;
    }

    /// <summary>A push service that accepts every request and records its authorization.</summary>
    private sealed class RecordingHandler
    {
        internal ConcurrentQueue<string> Authorizations { get; } = new();

        internal HttpMessageHandler Handler()
        {
            return new Answering(this);
        }

        private sealed class Answering(RecordingHandler owner) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                owner.Authorizations.Enqueue(request.Headers.Authorization?.ToString() ?? string.Empty);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created));
            }
        }
    }

    /// <summary>A push service that never answers.</summary>
    private sealed class HangingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            throw new OperationCanceledException(cancellationToken);
        }
    }
}