using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;

using Hermes.Operations;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "contract")]
public sealed class ReferenceTests
{
    [Fact]
    public void MSG_ENG_007_AC1_EveryConfigurationKeyTheEngineBindsIsOneSection4Lists()
    {
        IEnumerable<string> bound = ConfigurationRules.Keys.Select(key => key.Path)
            .Concat(ConfigurationRules.WriteVersions)
            .Concat(["PublicBaseAddress", "Languages", "DefaultLanguage", "TimeZone"]);

        Assert.Empty(bound.Except(Reference.FirstColumn(4), StringComparer.Ordinal));
    }

    [Fact]
    public void MSG_ENG_007_AC1_TheAlertConditionsAreThoseSection5Lists()
    {
        Assert.Equal(Reference.FirstColumn(5), AlertState.Conditions);
    }

    [Fact]
    public void MSG_ENG_007_AC1_EverySecretTheEngineReadsIsOneSection7Lists()
    {
        string[] read = [SecretCheck.Connection, KeyNeeds.ContentKeys, KeyNeeds.HashKeys, KeyNeeds.TokenKeys, KeyNeeds.SmsReportKey, KeyNeeds.PushVapidKeys];

        Assert.Empty(read.Except(Reference.Named(7), StringComparer.Ordinal));
    }

    [Fact]
    public void MSG_ENG_007_AC1_EveryInstrumentATestHostsMeterPublishesIsOneSection8Lists()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        services.AddSingleton<MessagingMetrics>();
        using ServiceProvider provider = services.BuildServiceProvider();
        IMeterFactory factory = provider.GetRequiredService<IMeterFactory>();
        List<string> published = [];
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, _) =>
        {
            if (instrument.Meter.Scope == factory)
            {
                published.Add(instrument.Name);
            }
        };
        listener.Start();

        provider.GetRequiredService<MessagingMetrics>();

        Assert.NotEmpty(published);
        Assert.Empty(published.Except(Reference.FirstColumn(8), StringComparer.Ordinal));
    }

    [Fact]
    public void FirstColumn_Section4_HoldsTheKeysWithTheirPaths()
    {
        Assert.Contains("Dispatch:Interval", Reference.FirstColumn(4));
        Assert.Contains("Secrets:ContentKeys:WriteVersion", Reference.FirstColumn(4));
    }
}