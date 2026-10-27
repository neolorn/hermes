using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using Npgsql;

using Xunit;

namespace Hermes.Tests;

/// <summary>The meter's count of final statuses and the health check <c>hermes.dispatch</c> (<c>06</c> MSG-OPS-005).</summary>
[Trait("Category", "integration")]
public sealed class DispatchHealthTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly TimeSpan Threshold = TimeSpan.FromMinutes(5);

    [Fact]
    public async Task MSG_OPS_005_AC2_TheMeterReportsADeliverysFinalStatusAndTheHealthCheckTurnsUnhealthyPastTheThreshold()
    {
        RecordingSmsProvider sms = new();
        await using EngineHost host = await StartAsync(sms);
        ConcurrentQueue<(long Value, string? Channel, string? Status)> measured = new();
        IMeterFactory factory = host.Services.GetRequiredService<IMeterFactory>();
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, subscribe) =>
        {
            if (instrument.Meter.Scope == factory && instrument.Name == "hermes.deliveries")
            {
                subscribe.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            Dictionary<string, object?> named = tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value, StringComparer.Ordinal);
            measured.Enqueue((value, named["channel"] as string, named["status"] as string));
        });
        listener.Start();
        await host.SendAsync(SendTests.Code(channels: [Channel.Sms]));
        await host.DispatchAsync();
        await host.SendAsync(SendTests.Code(channels: [Channel.Sms], recipient: new Recipient.Person("person-2")));

        host.Clock.Advance(Threshold);
        HealthStatus atTheThreshold = await StatusAsync(host);
        host.Clock.Advance(TimeSpan.FromSeconds(1));
        HealthStatus past = await StatusAsync(host);
        await host.DispatchAsync();
        HealthStatus dispatched = await StatusAsync(host);

        Assert.Equal([(1L, "sms", "sent"), (1L, "sms", "sent")], measured);
        Assert.Equal((HealthStatus.Healthy, HealthStatus.Unhealthy, HealthStatus.Healthy), (atTheThreshold, past, dispatched));
    }

    [Fact]
    public async Task Health_ADeliveryOnItsBackOffOrBeforeItsNotBefore_IsNotLate()
    {
        RecordingSmsProvider sms = new() { Answer = _ => new Attempt.Transient(AttemptReason.Temporary, null, null) };
        await using EngineHost host = await StartAsync(sms, configuration =>
        {
            configuration["Hermes:Retry:Initial"] = "01:00:00";
            configuration["Hermes:Retry:Max"] = "01:00:00";
        });
        await host.SendAsync(SendTests.Code(channels: [Channel.Sms]));
        await host.DispatchAsync();
        await host.SendAsync(SendTests.Code(channels: [Channel.Sms], recipient: new Recipient.Person("person-2")) with { NotBefore = EngineHost.Start.AddHours(1) });

        host.Clock.Advance(TimeSpan.FromMinutes(30));

        Assert.Equal(HealthStatus.Healthy, await StatusAsync(host));
    }

    [Fact]
    public async Task Health_ARowThisReleaseDoesNotKnow_IsLeftOut()
    {
        RecordingSmsProvider sms = new();
        await using EngineHost host = await StartAsync(sms);
        await host.SendAsync(SendTests.Code(channels: [Channel.Sms]));
        await host.RunAsync(connection => new NpgsqlCommand("ALTER TABLE messaging.deliveries DROP CONSTRAINT ck_deliveries_channel; UPDATE messaging.deliveries SET channel = 'fax'", connection));

        host.Clock.Advance(TimeSpan.FromHours(1));

        Assert.Equal(HealthStatus.Healthy, await StatusAsync(host));
    }

    [Fact]
    public async Task Health_AnApplicationThatDoesNotDispatch_RegistersNoCheck()
    {
        await using EngineHost host = await EngineHost.StartAsync(
            await EngineHost.DatabaseAsync(postgres),
            configure: configuration => configuration["Hermes:Dispatch:Enabled"] = "false",
            services: services => services.AddHealthChecks());
        await host.SendAsync(SendTests.Code(channels: [Channel.Sms]));

        host.Clock.Advance(TimeSpan.FromHours(1));
        HealthReport report = await host.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain("hermes.dispatch", report.Entries.Keys);
    }

    private async Task<EngineHost> StartAsync(RecordingSmsProvider sms, Action<Dictionary<string, string?>>? configure = null)
    {
        return await EngineHost.StartAsync(
            await EngineHost.DatabaseAsync(postgres),
            configure: configuration =>
            {
                EngineHost.Dispatching(configuration);
                configure?.Invoke(configuration);
            },
            services: services =>
            {
                services.AddSingleton<IEmailProvider>(_ => new RecordingEmailProvider());
                services.AddSingleton<ISmsProvider>(sms);
            });
    }

    private static async Task<HealthStatus> StatusAsync(EngineHost host)
    {
        HealthReport report = await host.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync(TestContext.Current.CancellationToken);
        return report.Entries["hermes.dispatch"].Status;
    }
}