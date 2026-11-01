using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Xunit;

namespace Hermes.Conversations.Tests;

/// <summary>The package's registration and its checks at start (M-031).</summary>
[Trait("Category", "integration")]
public sealed class RegistrationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task MSG_LIB_003_AC7_AHostCallingAddHermesConversationsOnceAfterAddHermesStarts()
    {
        using IHost host = await ConversationsHost.BuildAsync(postgres, (services, configuration) =>
        {
            ConversationsHost.AddEngine(services, configuration);
            services.AddHermesConversations();
        });

        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AddHermesConversations_CalledTwice_StartsAndChecksOnce()
    {
        using IHost host = await ConversationsHost.BuildAsync(postgres, (services, configuration) =>
        {
            ConversationsHost.AddEngine(services, configuration);
            services.AddHermesConversations();
            services.AddHermesConversations();
        });

        await host.StartAsync(TestContext.Current.CancellationToken);
        Assert.Single(host.Services.GetServices<IHostedService>(), service => service is ConversationsStartup);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("Hermes:Conversations:Unknown", "Conversations:Unknown")]
    [InlineData("Hermes:Conversations:Support:Topics:0", "Conversations:Support:Topics:0")]
    [InlineData("Hermes:Conversations", "Conversations")]
    public async Task MSG_OPS_001_AC4_AKeyUnderConversationsThePackageDoesNotKnowStopsTheStartNamingIt(string path, string key)
    {
        using IHost host = await ConversationsHost.BuildAsync(
            postgres,
            (services, configuration) =>
            {
                ConversationsHost.AddEngine(services, configuration);
                services.AddHermesConversations();
            },
            configuration => configuration[path] = "on");

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.configurationinvalid", failure.Code);
        Assert.Equal(key, failure.Details["key"]);
    }

    [Fact]
    public async Task AddHermes_AKeyUnderConversationsWithoutThePackage_StartsSinceNothingReadsIt()
    {
        using IHost host = await ConversationsHost.BuildAsync(
            postgres,
            ConversationsHost.AddEngine,
            configuration => configuration["Hermes:Conversations:Unknown"] = "on");

        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }
}