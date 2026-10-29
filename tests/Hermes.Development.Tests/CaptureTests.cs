using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Testing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Xunit;

namespace Hermes.Development.Tests;

/// <summary>What the inbox captures, and where it does nothing (<c>02</c> MSG-DEV-001).</summary>
[Trait("Category", "integration")]
public sealed class CaptureTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Theory]
    [InlineData(HostOrder.ProvidersBefore)]
    [InlineData(HostOrder.ProvidersAfter)]
    public async Task MSG_DEV_001_AC1_AnEmailAnSmsAndAPushTheEngineDispatchesAreWrittenToTheInboxAndNoneReachesTheHostsProviders(HostOrder order)
    {
        using ScratchFolder folder = new();
        await using InboxHost host = await InboxHost.StartAsync(postgres, folder.Path, order);
        await host.SubscribeAsync();

        await host.SendAsync([Channel.Email, Channel.Sms, Channel.Push]);
        IReadOnlyList<InboxMessage> messages = await Captured.WaitAsync(folder.Path, 3);

        Assert.Equal([Channel.Email, Channel.Sms, Channel.Push], messages.Select(message => message.Channel).Order());
        Assert.Equal(0, host.Providers.Calls);
    }

    [Theory]
    [InlineData(HostOrder.ProvidersBefore)]
    [InlineData(HostOrder.ProvidersAfter)]
    public async Task MSG_DEV_001_AC1_AnEmailHandedToTheProvidersDirectlyIsWrittenToTheInboxAndReachesNoneOfTheHostsProviders(HostOrder order)
    {
        using ScratchFolder folder = new();
        HostProviders providers = new();
        using IHost host = Captured.Direct(Environments.Development, folder.Path, order, services => InboxHost.Register(services, providers));
        await host.StartAsync(TestContext.Current.CancellationToken);

        IEmailSession session = await host.Services.GetRequiredService<IEmailProvider>().OpenAsync(TestContext.Current.CancellationToken);
        Attempt answer = await session.SendAsync(Captured.Email(), TestContext.Current.CancellationToken);
        await session.DisposeAsync();

        InboxMessage message = Assert.Single(Captured.In(folder.Path));
        Assert.Equal(new Attempt.Accepted(message.Id), answer);
        Assert.Equal(Channel.Email, message.Channel);
        Assert.Equal(0, providers.Calls);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData(null)]
    public async Task MSG_DEV_001_AC2_OutsideDevelopmentTheCallRegistersNothingAndTheHostsOwnProvidersAreCalled(string? environment)
    {
        using ScratchFolder folder = new();
        HostProviders providers = new();
        using IHost host = Captured.Direct(environment, folder.Path, HostOrder.ProvidersBefore, services => InboxHost.Register(services, providers));
        await host.StartAsync(TestContext.Current.CancellationToken);

        IEmailSession session = await host.Services.GetRequiredService<IEmailProvider>().OpenAsync(TestContext.Current.CancellationToken);
        await session.SendAsync(Captured.Email(), TestContext.Current.CancellationToken);
        await host.Services.GetRequiredService<ISmsProvider>().SendAsync(Captured.Sms(), TestContext.Current.CancellationToken);
        await host.Services.GetRequiredService<IPushProvider>().SendAsync(Captured.Push("https://fcm.googleapis.com/fcm/send/one"), TestContext.Current.CancellationToken);

        Assert.Equal((1, 1, 1), (providers.Email.Calls, providers.Sms.Calls, providers.Push.Calls));
        Assert.False(Directory.Exists(folder.Path));
        Assert.DoesNotContain(host.Services.GetServices<IHostedService>(), service => service.GetType().Assembly == typeof(InboxFilter).Assembly);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData(null)]
    public void MSG_DEV_001_AC2_OutsideDevelopmentTheMappingMapsNoRoute(string? environment)
    {
        WebApplicationBuilder builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseKestrelCore();
        builder.Services.AddRouting();
        builder.AddHermesDevelopmentInbox();
        using WebApplication app = builder.Build();

        app.MapHermesDevelopmentInbox(InboxHost.Prefix);

        Assert.NotEqual(Environments.Development, app.Environment.EnvironmentName);
        Assert.Empty(((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MSG_DEV_001_AC3_WithAddHermesTestingBeforeOrAfterTheInMemoryProvidersReceiveEveryMessageAndTheInboxHoldsNone(bool testingFirst)
    {
        using ScratchFolder folder = new();
        using IHost host = Captured.Direct(
            Environments.Development,
            folder.Path,
            testingFirst ? HostOrder.ProvidersBefore : HostOrder.ProvidersAfter,
            services => services.AddHermesTesting());
        await host.StartAsync(TestContext.Current.CancellationToken);

        IEmailSession session = await host.Services.GetRequiredService<IEmailProvider>().OpenAsync(TestContext.Current.CancellationToken);
        await session.SendAsync(Captured.Email(), TestContext.Current.CancellationToken);
        await host.Services.GetRequiredService<ISmsProvider>().SendAsync(Captured.Sms(), TestContext.Current.CancellationToken);
        await host.Services.GetRequiredService<IPushProvider>().SendAsync(Captured.Push("https://fcm.googleapis.com/fcm/send/one"), TestContext.Current.CancellationToken);

        Assert.Single(host.Services.GetRequiredService<InMemoryEmailProvider>().Received);
        Assert.Single(host.Services.GetRequiredService<InMemorySmsProvider>().Received);
        Assert.Single(host.Services.GetRequiredService<InMemoryPushProvider>().Received);
        Assert.Empty(Captured.In(folder.Path));
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task MSG_DEV_001_AC4_TheStartLogsOneLineNamingTheFolderAndTheGroupAndNothingOfAMessage()
    {
        using ScratchFolder folder = new();
        await using InboxHost host = await InboxHost.StartAsync(postgres, folder.Path);
        await host.SendAsync([Channel.Email]);
        await Captured.WaitAsync(folder.Path, 1);

        (LogLevel level, string _, string text) = Assert.Single(host.Logs.Lines, line => line.Category.StartsWith("Hermes.Development", StringComparison.Ordinal));
        Assert.Equal(LogLevel.Information, level);
        Assert.Contains(folder.Path, text, StringComparison.Ordinal);
        Assert.Contains(new Uri(host.Client.BaseAddress!, InboxHost.Prefix + "/").AbsoluteUri, text, StringComparison.Ordinal);
        Assert.DoesNotContain("someone@example.com", text, StringComparison.Ordinal);
        Assert.DoesNotContain("4521", text, StringComparison.Ordinal);
    }
}