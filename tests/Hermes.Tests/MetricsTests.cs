using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

using Hermes.Operations;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "unit")]
public sealed class MetricsTests
{
    [Fact]
    public void MSG_OPS_005_TheMeterHermesPublishesTheInstrumentsOfTheReference()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        services.AddSingleton<MessagingMetrics>();
        using ServiceProvider provider = services.BuildServiceProvider();
        IMeterFactory factory = provider.GetRequiredService<IMeterFactory>();
        List<(string, string, string?)> published = [];
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, _) =>
        {
            if (instrument.Meter.Scope == factory)
            {
                published.Add((instrument.Name, Kind(instrument), instrument.Unit));
            }
        };
        listener.Start();

        provider.GetRequiredService<MessagingMetrics>();

        Assert.Equal(
            [
                ("hermes.deliveries", "Counter", "{delivery}"),
                ("hermes.attempts", "Counter", "{attempt}"),
                ("hermes.dispatch.oldest_due_age", "Gauge", "s"),
                ("hermes.provider.duration", "Histogram", "s"),
                ("hermes.conversations.started", "Counter", "{conversation}"),
                ("hermes.conversations.closed", "Counter", "{conversation}"),
            ],
            published);
    }

    [Fact]
    public void MSG_OPS_005_EachInstrumentIsOfTheMeterHermes()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        services.AddSingleton<MessagingMetrics>();
        using ServiceProvider provider = services.BuildServiceProvider();
        IMeterFactory factory = provider.GetRequiredService<IMeterFactory>();
        HashSet<string> meters = [];
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, _) =>
        {
            if (instrument.Meter.Scope == factory)
            {
                meters.Add(instrument.Meter.Name);
            }
        };
        listener.Start();

        provider.GetRequiredService<MessagingMetrics>();

        Assert.Equal(["Hermes"], meters);
    }

    private static string Kind(Instrument instrument)
    {
        return instrument switch
        {
            Counter<long> => "Counter",
            Gauge<double> => "Gauge",
            Histogram<double> => "Histogram",
            _ => instrument.GetType().Name,
        };
    }
}