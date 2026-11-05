using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.DependencyInjection;

namespace Hermes.Sms.Cequens.Tests;

/// <summary>
/// The provider as <c>AddHermesCequensSms</c> registers it, the named client's primary handler
/// replaced by one reaching the gateway in the test process, and a secret source holding one
/// access token made at run time.
/// </summary>
internal sealed class CequensHarness : IAsyncDisposable
{
    internal const string Number = "+447700900123";

    internal const string Sender = "Notices";

    internal const string Text = "Your code is 482913";

    private readonly ServiceProvider services;

    private CequensHarness(GatewayFake gateway, SecretSourceFake secrets, ServiceProvider services)
    {
        Gateway = gateway;
        Secrets = secrets;
        this.services = services;
        Provider = services.GetRequiredService<ISmsProvider>();
    }

    internal GatewayFake Gateway { get; }

    internal SecretSourceFake Secrets { get; }

    internal ISmsProvider Provider { get; }

    internal IHttpClientFactory Clients => services.GetRequiredService<IHttpClientFactory>();

    /// <summary>
    /// A provider over the gateway, the services <paramref name="host"/> registers added before
    /// the package's own, its primary handler <paramref name="primary"/> where given, and
    /// <paramref name="client"/> applied to the named client last.
    /// </summary>
    internal static async Task<CequensHarness> StartAsync(Action<IServiceCollection>? host = null, Func<HttpMessageHandler>? primary = null, Action<HttpClient>? client = null)
    {
        GatewayFake gateway = await GatewayFake.StartAsync().ConfigureAwait(false);
        SecretSourceFake secrets = new SecretSourceFake().WithVersions(CequensToken.Name, (1, Token()));
        ServiceCollection collection = new();
        host?.Invoke(collection);
        collection.AddSingleton<ISecretSource>(secrets);
        collection.AddHermesCequensSms();
        IHttpClientBuilder named = collection.AddHttpClient(CequensClient.Name).ConfigurePrimaryHttpMessageHandler(primary ?? gateway.CreateHandler);
        if (client is not null)
        {
            named.ConfigureHttpClient(client);
        }

        return new CequensHarness(gateway, secrets, collection.BuildServiceProvider());
    }

    /// <summary>An access token made at run time: printable ASCII with no space, as a JWT is.</summary>
    internal static byte[] Token()
    {
        return Encoding.ASCII.GetBytes($"{Convert.ToHexString(RandomNumberGenerator.GetBytes(12))}.{Convert.ToHexString(RandomNumberGenerator.GetBytes(24))}.{Convert.ToHexString(RandomNumberGenerator.GetBytes(16))}");
    }

    /// <summary>An SMS to <see cref="Number"/> under <see cref="Sender"/>.</summary>
    internal static SmsMessage Message(string text = Text, SmsEncoding encoding = SmsEncoding.Gsm7, int segments = 1, Uri? reportAddress = null)
    {
        return new SmsMessage
        {
            ToNumber = Number,
            Sender = Sender,
            Text = text,
            DeliveryReference = "q1cTSz0yR9mB3Vn0aXo1Tw",
            Encoding = encoding,
            Segments = segments,
            ReportAddress = reportAddress,
        };
    }

    public async ValueTask DisposeAsync()
    {
        await services.DisposeAsync().ConfigureAwait(false);
        await Gateway.DisposeAsync().ConfigureAwait(false);
    }
}