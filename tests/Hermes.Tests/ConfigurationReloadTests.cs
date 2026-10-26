using System;
using System.Collections.Generic;

using Hermes.Operations;

using Microsoft.Extensions.Configuration;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "unit")]
public sealed class ConfigurationReloadTests
{
    [Fact]
    public void MSG_OPS_001_AC2_ReloadedValueWithinItsRuleTakesEffect()
    {
        IConfigurationRoot root = Root();
        using ConfigurationState state = Started(root);

        root["Hermes:QuietHours:Start"] = "22:15";
        root.Reload();

        Assert.Equal(new TimeOnly(22, 15), Bound(state).QuietHours.Start);
        Assert.Empty(state.InvalidKeys);
    }

    [Fact]
    public void MSG_OPS_001_AC2_StartOnlyKeyChangedWhileRunningTakesEffectOnlyAtTheNextStart()
    {
        IConfigurationRoot root = Root();
        using ConfigurationState state = Started(root);

        root["Hermes:Dispatch:MaxPasses"] = "8";
        root["Hermes:Languages:1"] = "ar";
        root.Reload();

        Assert.Equal(4, Bound(state).Dispatch.MaxPasses);
        Assert.Equal(["en"], Bound(state).Languages);
        using ConfigurationState restarted = Started(root);
        Assert.Equal(8, Bound(restarted).Dispatch.MaxPasses);
        Assert.Equal(["en", "ar"], Bound(restarted).Languages);
    }

    [Fact]
    public void MSG_OPS_001_AC2_ReloadedValueOutsideItsRuleLeavesTheLastValidValueInForce()
    {
        IConfigurationRoot root = Root();
        root["Hermes:QuietHours:Spread"] = "01:00:00";
        using ConfigurationState state = Started(root);

        root["Hermes:QuietHours:Spread"] = "05:00:00";
        root["Hermes:Dispatch:BatchSize"] = "200";
        root.Reload();

        Assert.Equal(TimeSpan.FromHours(1), Bound(state).QuietHours.Spread);
        Assert.Equal(200, Bound(state).Dispatch.BatchSize);
        Assert.Equal(["QuietHours:Spread"], state.InvalidKeys);

        root["Hermes:QuietHours:Spread"] = "02:00:00";
        root.Reload();

        Assert.Equal(TimeSpan.FromHours(2), Bound(state).QuietHours.Spread);
        Assert.Empty(state.InvalidKeys);
    }

    [Fact]
    public void MSG_OPS_001_AC2_ReloadedValueThatBreaksAnotherKeysRuleLeavesBothInForce()
    {
        IConfigurationRoot root = Root();
        using ConfigurationState state = Started(root);

        // A Timeout of 50 seconds needs a ClaimTimeout of at least 130 seconds, above the 2 minutes in force.
        root["Hermes:Dispatch:Timeout"] = "00:00:50";
        root.Reload();

        Assert.Equal(TimeSpan.FromSeconds(20), Bound(state).Dispatch.Timeout);
        Assert.Equal(TimeSpan.FromMinutes(2), Bound(state).Dispatch.ClaimTimeout);
        Assert.Equal(["Dispatch:ClaimTimeout"], state.InvalidKeys);
    }

    [Fact]
    public void MSG_OPS_001_AC2_ReloadedTimeZoneOutsideItsRuleLeavesTheLastValidOneInForce()
    {
        IConfigurationRoot root = Root();
        root["Hermes:TimeZone"] = "Africa/Cairo";
        using ConfigurationState state = Started(root);

        root["Hermes:TimeZone"] = "Cairo";
        root.Reload();

        Assert.Equal("Africa/Cairo", Bound(state).TimeZone);
        Assert.Equal(["TimeZone"], state.InvalidKeys);
    }

    [Fact]
    public void MSG_OPS_001_AC2_ReloadedKeyRemovedTakesItsDefault()
    {
        IConfigurationRoot root = Root();
        root["Hermes:Dispatch:BatchSize"] = "50";
        using ConfigurationState state = Started(root);

        root["Hermes:Dispatch:BatchSize"] = null;
        root.Reload();

        Assert.Equal(100, Bound(state).Dispatch.BatchSize);
    }

    [Fact]
    public void Reload_EachReload_SignalsTheOptionsChange()
    {
        IConfigurationRoot root = Root();
        using ConfigurationState state = Started(root);
        bool changed = false;
        using IDisposable registration = state.GetChangeToken().RegisterChangeCallback(_ => changed = true, null);

        root["Hermes:Dispatch:BatchSize"] = "10";
        root.Reload();

        Assert.True(changed);
    }

    private static IConfigurationRoot Root()
    {
        Dictionary<string, string?> values = TestHost.Languages();
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static ConfigurationState Started(IConfigurationRoot root)
    {
        ConfigurationState state = new(root.GetSection("Hermes"), publicBaseAddressRequired: false, TimeZoneInfo.GetSystemTimeZones);
        state.Start();
        return state;
    }

    private static MessagingOptions Bound(ConfigurationState state)
    {
        MessagingOptions options = new();
        state.Effective.Bind(options);
        return options;
    }
}