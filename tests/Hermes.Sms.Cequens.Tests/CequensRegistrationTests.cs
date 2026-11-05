using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Xunit;

namespace Hermes.Sms.Cequens.Tests;

[Trait("Category", "unit")]
public sealed class CequensRegistrationTests
{
    [Fact]
    public async Task MSG_SMS_007_AC7_UnderDebugLoggingNoLineHoldsTheNumberTheTextTheTokenOrTheAnswersText()
    {
        using LogCapture logs = new();
        await using CequensHarness harness = await CequensHarness.StartAsync(host: services => services
            .AddLogging(logging => logging.AddProvider(logs).SetMinimumLevel(LogLevel.Debug))
            .AddHttpClient("Control").ConfigurePrimaryHttpMessageHandler(() => new FailingHandler(HttpRequestError.ConnectionError)));
        byte[] token = CequensHarness.Token();
        harness.Secrets.WithVersions(CequensToken.Name, (1, token));
        Uri report = new("https://notices.example.com/notifications/sms/report/f0Jx2mQa9", UriKind.Absolute);

        await harness.Provider.SendAsync(CequensHarness.Message(reportAddress: report), TestContext.Current.CancellationToken);
        harness.Gateway.Status = 400;
        harness.Gateway.Body = GatewayAnswers.Refused(-12, "Invalid Sender Name");
        await harness.Provider.SendAsync(CequensHarness.Message(), TestContext.Current.CancellationToken);
        harness.Gateway.Status = 401;
        harness.Gateway.Body = GatewayAnswers.Unauthorized;
        await harness.Provider.BalanceAsync(TestContext.Current.CancellationToken);

        string[] held = ["447700900123", CequensHarness.Text, Encoding.ASCII.GetString(token), "Invalid Sender Name", "Request failed", "UnAuthorized", "Request handled", "apis.cequens.com", "f0Jx2mQa9"];
        Assert.DoesNotContain(logs.Lines, line => held.Any(value => line.Contains(value, StringComparison.Ordinal)));

        // A client of the host's own, with the factory's logging, shows the capture sees what it writes.
        using HttpClient control = harness.Clients.CreateClient("Control");
        await Assert.ThrowsAsync<HttpRequestException>(() => control.PostAsync(CequensClient.Messages, null, TestContext.Current.CancellationToken));
        Assert.Contains(logs.Lines, line => line.Contains("apis.cequens.com", StringComparison.Ordinal));
    }

    [Fact]
    public void AddHermesCequensSms_ProviderRegisteredBefore_IsReplaced()
    {
        ServiceCollection services = new();
        services.AddSingleton<ISecretSource>(new SecretSourceFake());
        services.AddSingleton<ISmsProvider>(new EarlierProvider());

        services.AddHermesCequensSms();

        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.IsType<CequensSmsProvider>(provider.GetRequiredService<ISmsProvider>());
        Assert.Single(provider.GetServices<ISmsProvider>());
    }

    [Fact]
    public void AddHermesCequensSms_CalledTwice_RegistersOneProviderAndOneStartCheck()
    {
        ServiceCollection services = new();

        services.AddHermesCequensSms();
        services.AddHermesCequensSms();

        Assert.Single(services, service => service.ServiceType == typeof(ISmsProvider));
        Assert.Single(services, service => service.ImplementationType == typeof(CequensStartup));
    }

    [Fact]
    public void AddHermesCequensSms_NullServices_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CequensSmsServiceCollectionExtensions.AddHermesCequensSms(null!));
    }

    /// <summary>A provider a host registered before the package's.</summary>
    private sealed class EarlierProvider : ISmsProvider
    {
        public ValueTask<Attempt> SendAsync(SmsMessage message, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<Attempt>(new Attempt.Accepted(null));
        }

        public bool IsValidSender(string sender)
        {
            return true;
        }

        public ValueTask<BalanceReading> BalanceAsync(CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<BalanceReading>(new BalanceReading.NotSupported());
        }

        public ValueTask<SmsReportReading> ReadReportAsync(SmsReportRequest request, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(new SmsReportReading { Reads = false, Response = new SmsReportResponse { Status = 200, Body = ReadOnlyMemory<byte>.Empty } });
        }
    }
}