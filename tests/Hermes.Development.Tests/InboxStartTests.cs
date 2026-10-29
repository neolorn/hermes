using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Xunit;

namespace Hermes.Development.Tests;

/// <summary>The inbox's keys of <c>08</c> section 4 and its start's check of the providers (<c>02</c> MSG-DEV-001).</summary>
[Trait("Category", "unit")]
public sealed class InboxStartTests
{
    [Theory]
    [InlineData("DevelopmentInbox:Folder", "relative/inbox")]
    [InlineData("DevelopmentInbox:KeepFor", "00:59:59")]
    [InlineData("DevelopmentInbox:KeepFor", "90.00:00:01")]
    [InlineData("DevelopmentInbox:KeepFor", "a week")]
    [InlineData("DevelopmentInbox:MaxMessages", "9")]
    [InlineData("DevelopmentInbox:MaxMessages", "100001")]
    [InlineData("DevelopmentInbox:AllowRemote", "yes")]
    public async Task AddHermesDevelopmentInbox_WithAKeyOutsideItsRule_StopsTheStartNamingTheKey(string key, string value)
    {
        using ScratchFolder folder = new();
        using IHost host = Host(Environments.Development, folder.Path, key, value);

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.configurationinvalid", failure.Code);
        Assert.Equal(key, failure.Details["key"]);
    }

    [Fact]
    public async Task AddHermesDevelopmentInbox_OutsideDevelopment_ReadsNoKey()
    {
        using ScratchFolder folder = new();
        using IHost host = Host(Environments.Production, folder.Path, "DevelopmentInbox:KeepFor", "a week");

        await host.StartAsync(TestContext.Current.CancellationToken);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AddHermesDevelopmentInbox_WithAServiceProviderFactoryTheHostSetsAfterIt_StopsTheStart()
    {
        using ScratchFolder folder = new();
        HostApplicationBuilder builder = Builder(Environments.Development, folder.Path);
        builder.AddHermesDevelopmentInbox();
        builder.ConfigureContainer(new DefaultServiceProviderFactory());
        using IHost host = builder.Build();

        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains("AddHermesDevelopmentInbox", failure.Message, StringComparison.Ordinal);
    }

    private static IHost Host(string environment, string folder, string key, string value)
    {
        HostApplicationBuilder builder = Builder(environment, folder);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Hermes:" + key] = value });
        builder.AddHermesDevelopmentInbox();
        return builder.Build();
    }

    private static HostApplicationBuilder Builder(string environment, string folder)
    {
        HostApplicationBuilder builder = Microsoft.Extensions.Hosting.Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = environment });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Hermes:DevelopmentInbox:Folder"] = folder });
        return builder;
    }
}