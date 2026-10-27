using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Xunit;

namespace Hermes.Testing.Tests;

/// <summary>A host's test through <c>AddHermesTesting</c>, on a database of its own that <see cref="MessagingTestDatabase"/> migrates.</summary>
[Trait("Category", "integration")]
public sealed class TestingHostTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Start = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task MSG_LIB_008_AC1_AHostTestSendsAKindAndReadsTheRenderedMessageFromTheInMemoryProvider()
    {
        using IHost host = await StartAsync(new ManualClock(Start));

        Result<SendReceipt> sent = await SendAsync(host, [Channel.Email]);
        int taken = await host.Services.GetRequiredService<TestDispatcher>().RunUntilIdleAsync(TestContext.Current.CancellationToken);

        Assert.True(sent.IsSuccess);
        Assert.Equal(1, taken);
        EmailMessage message = Assert.Single(host.Services.GetRequiredService<InMemoryEmailProvider>().Received);
        Assert.Equal(("someone@example.com", "Your code", "Your code is 123456."), (message.ToAddress, message.Subject, message.Text));
    }

    [Fact]
    public async Task MSG_LIB_008_AC2_ATestCanMakeAnAttemptTransientAndSeeTheRetryOnceItsClockPassesTheBackOff()
    {
        ManualClock clock = new(Start);
        using IHost host = await StartAsync(clock);
        InMemorySmsProvider sms = host.Services.GetRequiredService<InMemorySmsProvider>();
        TestDispatcher dispatcher = host.Services.GetRequiredService<TestDispatcher>();
        int calls = 0;
        sms.AnswerWith(_ => ++calls == 1 ? new Attempt.Transient(AttemptReason.Temporary, null, null) : new Attempt.Accepted(null));
        Result<SendReceipt> sent = await SendAsync(host, [Channel.Sms]);

        int first = await dispatcher.RunUntilIdleAsync(TestContext.Current.CancellationToken);
        int beforeTheBackOff = await dispatcher.RunUntilIdleAsync(TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(36));
        int after = await dispatcher.RunUntilIdleAsync(TestContext.Current.CancellationToken);

        Assert.Equal((1, 0, 1), (first, beforeTheBackOff, after));
        Assert.Equal(2, sms.Received.Count);
        Assert.Equal(DeliveryStatus.Sent, (await ReadAsync(host, sent.Value!.Send)).Deliveries[0].Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AddHermesTesting_BeforeOrAfterAddHermes_LeavesOnlyATestsPassesToDispatch(bool testingFirst)
    {
        ManualClock clock = new(Start);
        using IHost host = await StartAsync(clock, testingFirst: testingFirst);
        await SendAsync(host, [Channel.Sms]);

        clock.Advance(TimeSpan.FromMinutes(1));
        await Task.Delay(300, TestContext.Current.CancellationToken);
        int beforeAPass = host.Services.GetRequiredService<InMemorySmsProvider>().Received.Count;
        await host.Services.GetRequiredService<TestDispatcher>().RunUntilIdleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, beforeAPass);
        Assert.Single(host.Services.GetRequiredService<InMemorySmsProvider>().Received);
        Assert.DoesNotContain(host.Services.GetServices<IHostedService>(), service => service.GetType().Name is "HostedDispatcher" or "ObserverJob");
    }

    [Fact]
    public async Task RunUntilIdleAsync_WithTheObserverOn_DeliversEachPendingNoticeAfterTheDeliveries()
    {
        DeliveryObserverFake observer = new();
        using IHost host = await StartAsync(
            new ManualClock(Start),
            configure: configuration => configuration["Hermes:Observer:Enabled"] = "true",
            services: services => services.AddSingleton<IDeliveryObserver>(observer));
        Result<SendReceipt> sent = await SendAsync(host, [Channel.Email, Channel.Sms]);

        await host.Services.GetRequiredService<TestDispatcher>().RunUntilIdleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            sent.Value!.Deliveries.Select(delivery => (delivery.Delivery.Value, DeliveryStatus.Sent)).Order(),
            observer.Notices.Select(notice => (notice.Delivery.Value, notice.Status)).Order());
    }

    private async Task<IHost> StartAsync(
        ManualClock clock,
        Action<Dictionary<string, string?>>? configure = null,
        Action<IServiceCollection>? services = null,
        bool testingFirst = false)
    {
        string connection = await postgres.NewDatabaseAsync();
        await MessagingTestDatabase.MigrateAsync(connection, TestContext.Current.CancellationToken);
        Dictionary<string, string?> configuration = new(StringComparer.Ordinal)
        {
            ["Hermes:Languages:0"] = "en",
            ["Hermes:DefaultLanguage"] = "en",
            ["Hermes:Email:From:Address"] = "notices@example.com",
            ["Hermes:Sms:Sender"] = "Hermes",
            ["Hermes:Sms:AllowedPrefixes:0"] = "+20",
            ["Hermes:Retry:Jitter"] = "0.2",
        };
        configure?.Invoke(configuration);
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(configuration);
        Action testing = () => builder.Services.AddHermesTesting(secrets => secrets.SetSecret("connection", new SecretVersions { Versions = [new SecretVersion { Version = 1, Value = Encoding.UTF8.GetBytes(connection) }] }));
        if (testingFirst)
        {
            testing();
        }

        builder.Services.AddHermes(builder.Configuration, declare =>
        {
            declare.Category("security").Essential();
            declare.Kind("sign.in", "security").Email().Sms();
        });
        if (!testingFirst)
        {
            testing();
        }

        builder.Services.AddSingleton<TimeProvider>(clock);
        builder.Services.AddSingleton<ITemplateSource>(new TemplateSourceFake());
        builder.Services.AddSingleton(provider => new TestDispatcher(provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<TimeProvider>()));
        services?.Invoke(builder.Services);
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

    private static async Task<Result<SendReceipt>> SendAsync(IHost host, IReadOnlyList<Channel> channels)
    {
        AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using (scope)
        {
            return await scope.ServiceProvider.GetRequiredService<IMessages>().SendAsync(
                new Send
                {
                    Kind = "sign.in",
                    Recipient = new Recipient.Person("person-1"),
                    Email = "someone@example.com",
                    Phone = "+201000000001",
                    Values = new Dictionary<string, SendValue>(StringComparer.Ordinal) { ["code"] = new SendValue.PlainText("123456") },
                    Channels = channels,
                },
                TestContext.Current.CancellationToken);
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