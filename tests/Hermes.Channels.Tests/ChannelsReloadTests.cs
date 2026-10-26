using System;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Xunit;

namespace Hermes.Channels.Tests;

[Trait("Category", "unit")]
public sealed class ChannelsReloadTests
{
    [Fact]
    public async Task MSG_OPS_001_AC2_ReloadedChannelValueWithinItsRuleTakesEffect()
    {
        (IHost host, IConfigurationRoot root) = await StartedAsync();
        using (host)
        {
            root["Hermes:Push:Rate"] = "75";
            root["Hermes:Dispatch:Timeout"] = "00:00:07";
            root.Reload();

            Assert.Equal(75, Current(host).Push.Rate);
            Assert.Equal(TimeSpan.FromSeconds(7), host.Services.GetRequiredService<CallTimeout>().Current);
            Assert.Empty(host.Services.GetRequiredService<ChannelsConfiguration>().InvalidKeys);
        }
    }

    [Fact]
    public async Task MSG_OPS_001_AC2_ReloadedChannelValueOutsideItsRuleLeavesTheLastValidValueInForceUntilItIsPutRight()
    {
        (IHost host, IConfigurationRoot root) = await StartedAsync();
        using (host)
        {
            ChannelsConfiguration state = host.Services.GetRequiredService<ChannelsConfiguration>();
            root["Hermes:Push:Rate"] = "0";
            root["Hermes:Sms:Sender"] = "Hermes Notices";
            root.Reload();

            Assert.Equal(50, Current(host).Push.Rate);
            Assert.Equal(ChannelsHost.Sender, Current(host).Sms.Sender);
            Assert.Equal(["Sms:Sender", "Push:Rate"], state.InvalidKeys);

            root["Hermes:Push:Rate"] = "60";
            root["Hermes:Sms:Sender"] = "Orders";
            root.Reload();

            Assert.Equal(60, Current(host).Push.Rate);
            Assert.Equal("Orders", Current(host).Sms.Sender);
            Assert.Empty(state.InvalidKeys);
        }
    }

    [Fact]
    public async Task MSG_OPS_001_AC2_ReloadedRequiredChannelKeyRemovedLeavesTheLastValidValueInForce()
    {
        (IHost host, IConfigurationRoot root) = await StartedAsync();
        using (host)
        {
            root["Hermes:Push:Subject"] = null;
            root.Reload();

            Assert.Equal(ChannelsHost.Subject, Current(host).Push.Subject);
            Assert.Equal(["Push:Subject"], host.Services.GetRequiredService<ChannelsConfiguration>().InvalidKeys);
        }
    }

    [Fact]
    public async Task MSG_OPS_001_AC2_ReloadedListWithAnItemOutsideItsRuleLeavesTheWholeListInForce()
    {
        (IHost host, IConfigurationRoot root) = await StartedAsync();
        using (host)
        {
            root["Hermes:Push:AllowedHosts:0"] = "push.example.net";
            root.Reload();
            root["Hermes:Push:AllowedHosts:0"] = "updates.example.net";
            root["Hermes:Push:AllowedHosts:1"] = "*.com";
            root.Reload();

            Assert.Equal(["push.example.net"], Current(host).Push.Hosts);
            Assert.Equal(["Push:AllowedHosts"], host.Services.GetRequiredService<ChannelsConfiguration>().InvalidKeys);
        }
    }

    [Fact]
    public async Task MSG_OPS_001_AC2_ReloadedValueThatBreaksAnotherChannelKeysRuleLeavesBothInForce()
    {
        (IHost host, IConfigurationRoot root) = await StartedAsync();
        using (host)
        {
            root["Hermes:Sms:DrainWindow"] = "1.00:00:00";
            root["Hermes:Sms:DrainMinHistory"] = "1.00:00:00";
            root.Reload();
            root["Hermes:Sms:DrainBaseline"] = "1.12:00:00";
            root.Reload();

            Assert.Equal(TimeSpan.FromDays(7), Current(host).Sms.DrainBaseline);
            Assert.Equal(TimeSpan.FromDays(1), Current(host).Sms.DrainWindow);
            Assert.Equal(["Sms:DrainBaseline"], host.Services.GetRequiredService<ChannelsConfiguration>().InvalidKeys);
        }
    }

    [Fact]
    public async Task MSG_OPS_001_AC2_ReloadedKeyOfAPartNotUsedIsNotRead()
    {
        IConfigurationRoot root = ChannelsHost.Root(new() { ["Hermes:Email:From:Address"] = ChannelsHost.FromAddress, ["Hermes:Email:Host"] = ChannelsHost.SubmissionHost });
        using IHost host = ChannelsHost.Build(root, services => services.AddSingleton<Core.ISecretSource>(ChannelsHost.Secrets()));
        await host.StartAsync(TestContext.Current.CancellationToken);

        root["Hermes:Push:Rate"] = "0";
        root.Reload();

        Assert.Empty(host.Services.GetRequiredService<ChannelsConfiguration>().InvalidKeys);
        Assert.Equal(50, Current(host).Push.Rate);
    }

    [Fact]
    public async Task Reload_StartOnlyKey_KeepsTheValueTheStartTookUntilTheNextStart()
    {
        (IHost host, IConfigurationRoot root) = await StartedAsync();
        using (host)
        {
            root["Hermes:Email:Host"] = "smtp.example.net";
            root["Hermes:Email:Port"] = "2525";
            root["Hermes:Email:Security"] = "Tls";
            root["Hermes:Email:ShortLivedMaxValidity"] = "3.00:00:00";
            root.Reload();

            Assert.Equal(ChannelsHost.SubmissionHost, Current(host).Email.Host);
            Assert.Null(Current(host).Email.Port);
            Assert.Equal(EmailSecurity.ImplicitTls, Current(host).Email.Security);
            Assert.Equal(TimeSpan.FromDays(3), Current(host).Email.ShortLivedMaxValidity);
            Assert.Empty(host.Services.GetRequiredService<ChannelsConfiguration>().InvalidKeys);
        }
    }

    private static async Task<(IHost Host, IConfigurationRoot Root)> StartedAsync()
    {
        IConfigurationRoot root = ChannelsHost.Root(ChannelsHost.Used());
        IHost host = ChannelsHost.Build(root, services => services.AddSingleton<Core.ISecretSource>(ChannelsHost.Secrets()));
        await host.StartAsync(TestContext.Current.CancellationToken);
        return (host, root);
    }

    private static ChannelsOptions Current(IHost host)
    {
        return host.Services.GetRequiredService<IOptionsMonitor<ChannelsOptions>>().CurrentValue;
    }
}