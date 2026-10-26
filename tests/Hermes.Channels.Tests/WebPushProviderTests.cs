using System;
using System.Buffers.Text;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Xunit;

namespace Hermes.Channels.Tests;

[Trait("Category", "unit")]
public sealed class WebPushProviderTests
{
    public static TheoryData<int, Attempt> Answers { get; } = new()
    {
        { 200, new Attempt.Accepted(null) },
        { 201, new Attempt.Accepted(null) },
        { 202, new Attempt.Accepted(null) },
        { 404, new Attempt.Gone(AttemptReason.Gone, 404) },
        { 410, new Attempt.Gone(AttemptReason.Gone, 410) },
        { 400, new Attempt.Permanent(AttemptReason.BadRequest, 400) },
        { 401, new Attempt.Transient(AttemptReason.AuthFailed, 401, null) },
        { 403, new Attempt.Transient(AttemptReason.AuthFailed, 403, null) },
        { 413, new Attempt.Permanent(AttemptReason.MessageRefused, 413) },
        { 500, new Attempt.Transient(AttemptReason.Temporary, 500, null) },
        { 503, new Attempt.Transient(AttemptReason.Temporary, 503, null) },
        { 204, new Attempt.Permanent(AttemptReason.BadRequest, 204) },
        { 301, new Attempt.Permanent(AttemptReason.BadRequest, 301) },
        { 302, new Attempt.Permanent(AttemptReason.BadRequest, 302) },
        { 418, new Attempt.Permanent(AttemptReason.BadRequest, 418) },
    };

    [Fact]
    public async Task MSG_PUSH_001_AC1_RequestCarriesItsEncodingTtlUrgencyAndVapidAuthorization()
    {
        await using PushHarness harness = await PushHarness.StartAsync();
        long before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        Attempt attempt = await harness.Provider.SendAsync(PushHarness.Request(urgency: PushUrgency.High), TestContext.Current.CancellationToken);

        long after = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Assert.Equal(new Attempt.Accepted(null), attempt);
        PushServiceRequest request = Assert.Single(harness.Service.Requests);
        Assert.Equal("POST", request.Method);
        Assert.Equal(PushHarness.Endpoint, request.Address);
        Assert.Equal("aes128gcm", request.Headers["Content-Encoding"]);
        Assert.Equal("600", request.Headers["TTL"]);
        Assert.Equal("high", request.Headers["Urgency"]);

        (JsonElement payload, string key) = Token(request, harness.PublicKeys[1]);
        Assert.Equal(harness.PublicKeys[1], key);
        Assert.Equal("https://web.push.example.net", payload.GetProperty("aud").GetString());
        Assert.Equal(PushHarness.Subject, payload.GetProperty("sub").GetString());
        long expiry = payload.GetProperty("exp").GetInt64();
        Assert.InRange(expiry, before + (12 * 3600) - 60, after + (12 * 3600));
    }

    [Theory]
    [InlineData(PushUrgency.VeryLow, "very-low")]
    [InlineData(PushUrgency.Low, "low")]
    [InlineData(PushUrgency.High, "high")]
    public async Task MSG_PUSH_001_AC1_UrgencyOtherThanNormalIsSent(PushUrgency urgency, string header)
    {
        await using PushHarness harness = await PushHarness.StartAsync();

        await harness.Provider.SendAsync(PushHarness.Request(urgency: urgency), TestContext.Current.CancellationToken);

        Assert.Equal(header, Assert.Single(harness.Service.Requests).Headers["Urgency"]);
    }

    [Fact]
    public async Task MSG_PUSH_001_AC1_NormalUrgencySendsNoUrgency()
    {
        await using PushHarness harness = await PushHarness.StartAsync();

        await harness.Provider.SendAsync(PushHarness.Request(urgency: PushUrgency.Normal), TestContext.Current.CancellationToken);

        Assert.False(Assert.Single(harness.Service.Requests).Headers.ContainsKey("Urgency"));
    }

    [Fact]
    public async Task MSG_PUSH_001_AC2_MessageDecryptsUnderTheTestsOwnSection3ToItsPlaintext()
    {
        await using PushHarness harness = await PushHarness.StartAsync();

        await harness.Provider.SendAsync(PushHarness.Request(payload: Vectors.Plaintext), TestContext.Current.CancellationToken);

        byte[] body = Assert.Single(harness.Service.Requests).Body;
        byte[] plaintext = Rfc8291.Decrypt(body, Base64Url.DecodeFromChars(Vectors.ReceiverPrivateKey), Base64Url.DecodeFromChars(Vectors.ReceiverPublicKey), Base64Url.DecodeFromChars(Vectors.AuthenticationSecret));
        Assert.Equal(Vectors.Plaintext, Encoding.UTF8.GetString(plaintext));
        Assert.Equal(4096u, System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(body.AsSpan(16, 4)));
    }

    [Fact]
    public async Task MSG_PUSH_001_AC3_RateLimitedAnswerReturnsAtOnce()
    {
        await using PushHarness harness = await PushHarness.StartAsync(timeout: TimeSpan.FromSeconds(10));
        harness.Service.Status = 429;
        harness.Service.RetryAfter = "3600";
        Stopwatch elapsed = Stopwatch.StartNew();

        Attempt attempt = await harness.Provider.SendAsync(PushHarness.Request(), TestContext.Current.CancellationToken);

        Assert.Equal(new Attempt.Transient(AttemptReason.RateLimited, 429, TimeSpan.FromHours(1)), attempt);
        Assert.Single(harness.Service.Requests);
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task MSG_CHAN_001_AC4_PushRateLimitedUnderAHostsRetryingHandlerIsAnsweredAtOnce()
    {
        await using PushHarness harness = await PushHarness.StartAsync(host: services => services.ConfigureHttpClientDefaults(client => client.AddHttpMessageHandler(() => new RetryingHandler())));
        harness.Service.Status = 429;

        Attempt attempt = await harness.Provider.SendAsync(PushHarness.Request(), TestContext.Current.CancellationToken);

        Assert.Equal(new Attempt.Transient(AttemptReason.RateLimited, 429, null), attempt);
        Assert.Single(harness.Service.Requests);
    }

    [Fact]
    public async Task MSG_CHAN_001_AC5_PushRequestLogsNothingOfItsEndpoint()
    {
        using LogCapture logs = new();
        await using PushHarness harness = await PushHarness.StartAsync(host: services => services
            .AddLogging(logging => logging.AddProvider(logs).SetMinimumLevel(LogLevel.Information))
            .AddHttpClient("Control").ConfigurePrimaryHttpMessageHandler(() => new RefusingHandler()));
        string path = new Uri(PushHarness.Endpoint).AbsolutePath;

        await harness.Provider.SendAsync(PushHarness.Request(), TestContext.Current.CancellationToken);

        Assert.DoesNotContain(logs.Lines, line => line.Contains(path, StringComparison.Ordinal) || line.Contains("web.push.example.net", StringComparison.Ordinal));

        // A client of the host's own, with the factory's logging, shows the capture sees what it writes.
        using HttpClient control = harness.Clients.CreateClient("Control");
        await Assert.ThrowsAsync<HttpRequestException>(() => control.PostAsync(new Uri(PushHarness.Endpoint), null, TestContext.Current.CancellationToken));
        Assert.Contains(logs.Lines, line => line.Contains(path, StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Answers))]
    public async Task SendAsync_PushServiceAnswer_IsClassedAsMsgPush005Gives(int status, Attempt expected)
    {
        await using PushHarness harness = await PushHarness.StartAsync();
        harness.Service.Status = status;
        harness.Service.Body = "a reason the service gives";

        Attempt attempt = await harness.Provider.SendAsync(PushHarness.Request(), TestContext.Current.CancellationToken);

        Assert.Equal(expected, attempt);
    }

    [Fact]
    public async Task SendAsync_RetryAfterAsADate_IsTheTimeLeftByTheClock()
    {
        await using PushHarness harness = await PushHarness.StartAsync();
        harness.Service.Status = 429;
        harness.Service.RetryAfter = harness.Clock.GetUtcNow().AddMinutes(5).ToString("R", System.Globalization.CultureInfo.InvariantCulture);

        Attempt attempt = await harness.Provider.SendAsync(PushHarness.Request(), TestContext.Current.CancellationToken);

        Assert.Equal(new Attempt.Transient(AttemptReason.RateLimited, 429, TimeSpan.FromMinutes(5)), attempt);
    }

    [Fact]
    public async Task SendAsync_ServiceDoesNotAnswerWithinTheTimeout_IsTransientTimeout()
    {
        await using PushHarness harness = await PushHarness.StartAsync(timeout: TimeSpan.FromSeconds(1));
        harness.Service.Hangs = true;

        Attempt attempt = await harness.Provider.SendAsync(PushHarness.Request(), TestContext.Current.CancellationToken);

        Assert.Equal(new Attempt.Transient(AttemptReason.Timeout, null, null), attempt);
    }

    [Fact]
    public async Task SendAsync_ConnectionRefused_IsTransientConnectionFailed()
    {
        await using PushHarness harness = await PushHarness.StartAsync(primary: () => new RefusingHandler());

        Attempt attempt = await harness.Provider.SendAsync(PushHarness.Request(), TestContext.Current.CancellationToken);

        Assert.Equal(new Attempt.Transient(AttemptReason.ConnectionFailed, null, null), attempt);
    }

    [Fact]
    public async Task SendAsync_KeyVersionNotHeld_IsTransientKeyUnavailableAndCallsNothing()
    {
        await using PushHarness harness = await PushHarness.StartAsync();

        Attempt attempt = await harness.Provider.SendAsync(PushHarness.Request(keyVersion: 3), TestContext.Current.CancellationToken);

        Assert.Equal(new Attempt.Transient(AttemptReason.KeyUnavailable, null, null), attempt);
        Assert.Empty(harness.Service.Requests);
    }

    [Fact]
    public async Task SendAsync_KeyVersion_SignsWithThatVersionsKey()
    {
        await using PushHarness harness = await PushHarness.StartAsync();

        await harness.Provider.SendAsync(PushHarness.Request(keyVersion: 2), TestContext.Current.CancellationToken);

        (_, string key) = Token(Assert.Single(harness.Service.Requests), harness.PublicKeys[2]);
        Assert.Equal(harness.PublicKeys[2], key);
    }

    [Fact]
    public async Task SendAsync_Topic_IsCarried()
    {
        await using PushHarness harness = await PushHarness.StartAsync();

        await harness.Provider.SendAsync(PushHarness.Request(topic: "Q2hhbmdlZE9yZGVyU3RhdHVzMTIzNDU2"), TestContext.Current.CancellationToken);

        Assert.Equal("Q2hhbmdlZE9yZGVyU3RhdHVzMTIzNDU2", Assert.Single(harness.Service.Requests).Headers["Topic"]);
    }

    [Fact]
    public async Task SendAsync_WithinAnHourOfTheToken_ReusesItForTheSameOriginAndVersion()
    {
        await using PushHarness harness = await PushHarness.StartAsync();

        await harness.Provider.SendAsync(PushHarness.Request(), TestContext.Current.CancellationToken);
        harness.Clock.Advance(TimeSpan.FromMinutes(59));
        await harness.Provider.SendAsync(PushHarness.Request(), TestContext.Current.CancellationToken);
        await harness.Provider.SendAsync(PushHarness.Request(endpoint: "https://other.push.example.net/push/1"), TestContext.Current.CancellationToken);
        await harness.Provider.SendAsync(PushHarness.Request(keyVersion: 2), TestContext.Current.CancellationToken);

        string[] tokens = [.. harness.Service.Requests.Select(request => request.Headers["Authorization"])];
        Assert.Equal(tokens[0], tokens[1]);
        Assert.NotEqual(tokens[0], tokens[2]);
        Assert.NotEqual(tokens[0], tokens[3]);
    }

    [Fact]
    public async Task SendAsync_AnHourAfterTheToken_ReplacesIt()
    {
        await using PushHarness harness = await PushHarness.StartAsync();

        await harness.Provider.SendAsync(PushHarness.Request(), TestContext.Current.CancellationToken);
        harness.Clock.Advance(TimeSpan.FromHours(1));
        await harness.Provider.SendAsync(PushHarness.Request(), TestContext.Current.CancellationToken);

        string[] tokens = [.. harness.Service.Requests.Select(request => request.Headers["Authorization"])];
        Assert.NotEqual(tokens[0], tokens[1]);
    }

    [Fact]
    public async Task Register_ErrorAnswer_ReachesTheClientWithNoContent()
    {
        await using PushHarness harness = await PushHarness.StartAsync();
        harness.Service.Status = 400;
        harness.Service.Body = "a reason the service gives";
        using HttpClient client = harness.Clients.CreateClient(PushClient.Name);

        using HttpResponseMessage response = await client.PostAsync(new Uri(PushHarness.Endpoint), null, TestContext.Current.CancellationToken);

        Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Register_SuccessAnswer_KeepsItsContent()
    {
        await using PushHarness harness = await PushHarness.StartAsync();
        harness.Service.Body = "accepted";
        using HttpClient client = harness.Clients.CreateClient(PushClient.Name);

        using HttpResponseMessage response = await client.PostAsync(new Uri(PushHarness.Endpoint), null, TestContext.Current.CancellationToken);

        Assert.Equal("accepted", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Register_UnderAHostsDefaults_FollowsNoRedirectAndCarriesTheLibrarysHandlerAlone()
    {
        ServiceCollection services = new();
        services.ConfigureHttpClientDefaults(client => client
            .AddHttpMessageHandler(() => new RetryingHandler())
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = true }));
        services.AddSingleton(new CallTimeout(() => TimeSpan.FromSeconds(20)));
        PushClient.Register(services);
        using ServiceProvider provider = services.BuildServiceProvider();

        HttpMessageHandler? handler = provider.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(PushClient.Name);
        System.Collections.Generic.List<HttpMessageHandler> chain = [];
        for (; handler is not null; handler = (handler as DelegatingHandler)?.InnerHandler)
        {
            chain.Add(handler);
        }

        Assert.DoesNotContain(chain, link => link is RetryingHandler);
        Assert.Single(chain, link => link is EmptyErrorContent);
        SocketsHttpHandler primary = Assert.IsType<SocketsHttpHandler>(chain[^1]);
        Assert.False(primary.AllowAutoRedirect);
    }

    [Fact]
    public async Task Register_Timeout_IsTheCallTimeoutInForce()
    {
        TimeSpan bound = TimeSpan.FromSeconds(7);
        await using PushHarness harness = await PushHarness.StartAsync(timeout: bound);

        using HttpClient client = harness.Clients.CreateClient(PushClient.Name);

        Assert.Equal(bound, client.Timeout);
    }

    /// <summary>The verified payload of the request's VAPID token, and the key its authorization names.</summary>
    private static (JsonElement Payload, string Key) Token(PushServiceRequest request, string publicKey)
    {
        string authorization = request.Headers["Authorization"];
        Assert.StartsWith("vapid ", authorization, StringComparison.Ordinal);
        string[] parameters = [.. authorization["vapid ".Length..].Split(',').Select(parameter => parameter.Trim())];
        string token = Assert.Single(parameters, parameter => parameter.StartsWith("t=", StringComparison.Ordinal))[2..];
        string key = Assert.Single(parameters, parameter => parameter.StartsWith("k=", StringComparison.Ordinal))[2..];

        string[] parts = token.Split('.');
        Assert.Equal(3, parts.Length);
        using JsonDocument header = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[0]));
        Assert.Equal("ES256", header.RootElement.GetProperty("alg").GetString());

        byte[] point = Base64Url.DecodeFromChars(publicKey);
        using ECDsa verifier = ECDsa.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, Q = new ECPoint { X = point[1..33], Y = point[33..] } });
        Assert.True(verifier.VerifyData(Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), Base64Url.DecodeFromChars(parts[2]), HashAlgorithmName.SHA256));

        using JsonDocument payload = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[1]));
        return (payload.RootElement.Clone(), key);
    }
}