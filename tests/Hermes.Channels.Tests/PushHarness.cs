using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.DependencyInjection;

namespace Hermes.Channels.Tests;

/// <summary>
/// The shipped push provider over the named client <c>Hermes.Push</c>, its primary handler
/// replaced by one reaching a push service in the test process, with two VAPID key
/// versions made at run time.
/// </summary>
internal sealed class PushHarness : IAsyncDisposable
{
    internal const string Subject = "mailto:operations@example.com";

    internal const string Endpoint = "https://web.push.example.net/push/d4c1b7e09a5f4e3c8b2a6f1e0d9c8b7a";

    private readonly ServiceProvider services;

    private PushHarness(PushServiceFake service, ServiceProvider services, ManualClock clock, PushKeys keys)
    {
        Service = service;
        this.services = services;
        Clock = clock;
        Provider = new WebPushProvider(services.GetRequiredService<IHttpClientFactory>(), () => Subject, keys, clock);
    }

    internal PushServiceFake Service { get; }

    internal ManualClock Clock { get; }

    internal WebPushProvider Provider { get; }

    /// <summary>The public keys of the versions held, base64url, by version.</summary>
    internal Dictionary<int, string> PublicKeys { get; } = [];

    internal IHttpClientFactory Clients => services.GetRequiredService<IHttpClientFactory>();

    /// <summary>
    /// A provider whose calls time out after <paramref name="timeout"/>, the services
    /// <paramref name="host"/> registers added before the client's own, and its primary
    /// handler <paramref name="primary"/> where given, the push service's otherwise.
    /// </summary>
    internal static async Task<PushHarness> StartAsync(Action<IServiceCollection>? host = null, TimeSpan? timeout = null, Func<HttpMessageHandler>? primary = null)
    {
        PushServiceFake service = await PushServiceFake.StartAsync().ConfigureAwait(false);
        ServiceCollection collection = new();
        host?.Invoke(collection);
        TimeSpan bound = timeout ?? TimeSpan.FromSeconds(20);
        collection.AddSingleton(new CallTimeout(() => bound));
        PushClient.Register(collection);
        collection.AddHttpClient(PushClient.Name).ConfigurePrimaryHttpMessageHandler(primary ?? service.CreateHandler);

        ManualClock clock = new(new DateTimeOffset(2026, 10, 26, 9, 0, 0, TimeSpan.Zero));
        PushKeys keys = new(clock);
        PushHarness harness = new(service, collection.BuildServiceProvider(), clock, keys);
        Dictionary<int, byte[]> versions = new() { [1] = Scalar(), [2] = Scalar() };
        foreach ((int version, byte[] scalar) in versions)
        {
            harness.PublicKeys[version] = Base64Url.EncodeToString(VapidKey.PublicPoint(scalar));
        }

        keys.Hold(versions);
        return harness;
    }

    /// <summary>A request to <see cref="Endpoint"/>, encrypted to RFC 8291 section 5's receiver.</summary>
    internal static PushRequest.WebPush Request(
        int keyVersion = 1,
        PushUrgency urgency = PushUrgency.Normal,
        string? topic = null,
        string endpoint = Endpoint,
        string payload = """{"title":"Order shipped","body":"Your order is on its way."}""")
    {
        return new PushRequest.WebPush(
            endpoint,
            Vectors.ReceiverPublicKey,
            Vectors.AuthenticationSecret,
            keyVersion,
            System.Text.Encoding.UTF8.GetBytes(payload),
            TimeSpan.FromMinutes(10),
            urgency,
            topic);
    }

    public async ValueTask DisposeAsync()
    {
        await services.DisposeAsync().ConfigureAwait(false);
        await Service.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>A P-256 private key made at run time, as its 32-byte scalar.</summary>
    internal static byte[] Scalar()
    {
        using ECDsa pair = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return pair.ExportParameters(includePrivateParameters: true).D!;
    }
}