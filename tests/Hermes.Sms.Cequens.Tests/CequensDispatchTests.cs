using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Testing;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Xunit;

namespace Hermes.Sms.Cequens.Tests;

/// <summary>The provider under the engine's dispatcher, which bounds each call by <c>Dispatch:Timeout</c> through its cancellation (M-043).</summary>
[Trait("Category", "integration")]
public sealed class CequensDispatchTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Person = "person-cq-7";

    private const string Code = "482913";

    [Fact]
    public async Task MSG_SMS_007_AC2_NoAnswerWithinTheCallsBoundIsTransientTimeoutThroughTheDispatcher()
    {
        using LogCapture logs = new();
        await using GatewayFake gateway = await GatewayFake.StartAsync();
        gateway.Hangs = true;
        byte[] token = CequensHarness.Token();
        using IHost host = await StartAsync(gateway, token, logs);

        SendId send = await SendAsync(host);
        int taken = await host.Services.GetRequiredService<TestDispatcher>().RunUntilIdleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, taken);
        Assert.Single(gateway.Requests);
        DeliveryRecord delivery = Assert.Single((await ReadAsync(host, send)).Deliveries);
        Assert.Equal(DeliveryStatus.Waiting, delivery.Status);
        AttemptRecord attempt = Assert.Single(delivery.Attempts);
        Assert.Equal((AttemptClass.Transient, AttemptReason.Timeout), (attempt.AnswerClass, attempt.Reason));

        Assert.NotEmpty(logs.Lines);
        string[] used = [CequensHarness.Number, "447700900123", "Your code is " + Code, Code, Person, Encoding.ASCII.GetString(token)];
        Assert.DoesNotContain(logs.Lines, line => used.Any(value => line.Contains(value, StringComparison.Ordinal)));
    }

    private async Task<IHost> StartAsync(GatewayFake gateway, byte[] token, LogCapture logs)
    {
        string connection = await postgres.NewDatabaseAsync();
        await MessagingTestDatabase.MigrateAsync(connection, TestContext.Current.CancellationToken);
        Dictionary<string, string?> configuration = new(StringComparer.Ordinal)
        {
            ["Hermes:Languages:0"] = "en",
            ["Hermes:DefaultLanguage"] = "en",
            ["Hermes:Sms:Sender"] = CequensHarness.Sender,
            ["Hermes:Sms:AllowedPrefixes:0"] = "+44",
            ["Hermes:Dispatch:Timeout"] = "00:00:01",
        };
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(configuration);
        builder.Logging.AddProvider(logs).SetMinimumLevel(LogLevel.Debug);
        builder.Services.AddHermesTesting(secrets =>
        {
            secrets.SetSecret("connection", new SecretVersions { Versions = [new SecretVersion { Version = 1, Value = Encoding.UTF8.GetBytes(connection) }] });
            secrets.SetSecret(CequensToken.Name, new SecretVersions { Versions = [new SecretVersion { Version = 1, Value = token }] });
        });
        builder.Services.AddHermes(builder.Configuration, declare =>
        {
            declare.Category("security").Essential();
            declare.Kind("sign.in", "security").Sms();
        });
        builder.Services.AddHermesCequensSms();
        builder.Services.AddHttpClient(CequensClient.Name).ConfigurePrimaryHttpMessageHandler(gateway.CreateHandler);
        builder.Services.AddSingleton<ITemplateSource>(new TemplateSourceFake());
        builder.Services.AddSingleton(provider => new TestDispatcher(provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<TimeProvider>()));
        IHost host = builder.Build();
        try
        {
            await host.StartAsync(TestContext.Current.CancellationToken);
        }
        catch
        {
            host.Dispose();
            throw;
        }

        return host;
    }

    private static async Task<SendId> SendAsync(IHost host)
    {
        AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using (scope)
        {
            Result<SendReceipt> sent = await scope.ServiceProvider.GetRequiredService<IMessages>().SendAsync(
                new Send
                {
                    Kind = "sign.in",
                    Recipient = new Recipient.Person(Person),
                    Phone = CequensHarness.Number,
                    Values = new Dictionary<string, SendValue>(StringComparer.Ordinal) { ["code"] = new SendValue.PlainText(Code) },
                },
                TestContext.Current.CancellationToken);
            Assert.True(sent.IsSuccess);
            return sent.Value!.Send;
        }
    }

    private static async Task<SendRecord> ReadAsync(IHost host, SendId send)
    {
        AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using (scope)
        {
            return (await scope.ServiceProvider.GetRequiredService<IMessages>().ReadAsync(send, TestContext.Current.CancellationToken)).Value!;
        }
    }
}