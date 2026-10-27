using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "integration")]
public sealed class WatchTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task MSG_OPS_004_AC5_AJobNoApplicationRunsRaisesNoJobStalledAndOneStartedThatStopsRaisesItUntilItCompletes()
    {
        AlertSinkFake alerts = new();
        await using DispatchEngine engine = await StartAsync(alerts);
        await Watching.RunAsync(engine.Host);
        bool unstarted = alerts.Calls.Any(call => call.Condition == "job-stalled");

        await ExecuteAsync(engine.Host, connection => new("INSERT INTO messaging.jobs (name, first_started_at, last_started_at, last_completed_at) VALUES ('retention', @at, @at, @at)", connection) { Parameters = { new("at", engine.Host.Clock.GetUtcNow().AddDays(-4)) } });
        await Watching.RunAsync(engine.Host);
        AlertSinkFake.AlertCall[] stalled = [.. alerts.Calls.Where(call => call.Condition == "job-stalled")];
        await ExecuteAsync(engine.Host, connection => new("UPDATE messaging.jobs SET last_completed_at = @at WHERE name = 'retention'", connection) { Parameters = { new("at", engine.Host.Clock.GetUtcNow()) } });
        await Watching.RunAsync(engine.Host);

        Assert.False(unstarted);
        AlertSinkFake.AlertCall raised = Assert.Single(stalled);
        Assert.Equal((true, "retention", "retention"), (raised.Raised, raised.Scope, raised.Details!["job"]));
        Assert.Single(alerts.Calls, call => !call.Raised && call.Condition == "job-stalled" && call.Scope == "retention");
    }

    [Fact]
    public async Task JobStalled_AJobThatRenewsItsClaimIsNotStalledThoughItHasNotCompleted()
    {
        AlertSinkFake alerts = new();
        await using DispatchEngine engine = await StartAsync(alerts);
        DateTimeOffset now = engine.Host.Clock.GetUtcNow();
        await ExecuteAsync(engine.Host, connection => new("INSERT INTO messaging.jobs (name, first_started_at, last_started_at, last_renewed_at) VALUES ('retention', @at, @at, @renewed)", connection) { Parameters = { new("at", now.AddDays(-4)), new("renewed", now) } });

        await Watching.RunAsync(engine.Host);

        Assert.DoesNotContain(alerts.Calls, call => call.Condition == "job-stalled");
    }

    [Fact]
    public async Task DispatchLag_ADeliveryDueLongerThanTheThresholdRaisesItForItsChannelAndRecordsTheGaugeAndOnceSentClearsIt()
    {
        AlertSinkFake alerts = new();
        await using DispatchEngine engine = await StartAsync(alerts);
        ConcurrentQueue<(double Age, string? Channel)> ages = [];
        using MeterListener listener = Listen(engine.Host, ages);
        await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]));
        await ExecuteAsync(engine.Host, connection => new("UPDATE messaging.deliveries SET became_due_at = @at", connection) { Parameters = { new("at", engine.Host.Clock.GetUtcNow().AddMinutes(-6)) } });

        await Watching.RunAsync(engine.Host);
        AlertSinkFake.AlertCall raised = Assert.Single(alerts.Calls, call => call.Condition == "dispatch-lag");
        double lagging = ages.Last(age => age.Channel == "sms").Age;
        await engine.Host.DispatchAsync();
        await Watching.RunAsync(engine.Host);

        Assert.Equal((true, "sms", "sms", "365"), (raised.Raised, raised.Scope, raised.Details!["channel"], raised.Details["age"]));
        Assert.Equal(365, lagging, 0.5);
        Assert.Equal(0, ages.Last(age => age.Channel == "sms").Age);
        Assert.Equal(0, ages.Last(age => age.Channel == "email").Age);
        Assert.Single(alerts.Calls, call => !call.Raised && call.Condition == "dispatch-lag" && call.Scope == "sms");
    }

    [Fact]
    public async Task MSG_SEND_007_AC10_FortySmsNotifiedWithinASecondAreClaimedFiveInItAndDispatchLagReadsTheLastFromWhenItBecameDue()
    {
        AlertSinkFake alerts = new();
        ContentProtectorFake vault = new("vault");
        await using DispatchEngine engine = await StartAsync(
            alerts,
            configuration =>
            {
                configuration["Hermes:Dispatch:Hosted"] = "true";
                configuration["Hermes:Dispatch:MaxPasses"] = "4";
                configuration["Hermes:Sms:Rate"] = "5";
            },
            services => services.AddSingleton<IContentProtector>(vault));
        ConcurrentQueue<(double Age, string? Channel)> ages = [];
        using MeterListener listener = Listen(engine.Host, ages);
        for (int index = 0; index < 40; index++)
        {
            await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms], recipient: new Recipient.Person($"person-{index}")));
        }

        await Watching.UntilAsync(() => Task.FromResult(engine.Sms.Received.Count == 5));
        await Task.Delay(300, TestContext.Current.CancellationToken);
        (int Sent, int Opened) inSecond = (engine.Sms.Received.Count, vault.Opens);
        await Watching.RunAsync(engine.Host, "watch", Watching.Interval);

        Assert.Equal((5, 10), inSecond);
        Assert.InRange(engine.Sms.Received.Count, 6, 30);
        Assert.Equal(Watching.Interval.TotalSeconds, ages.Last(age => age.Channel == "sms").Age, 0.5);
    }

    [Fact]
    public async Task MSG_SEND_017_AC3_AnObserverThatKeepsThrowingRaisesTheAlertOnceItsOldestNoticePassesTheThresholdAndOnceTakenClearsIt()
    {
        AlertSinkFake alerts = new();
        DeliveryObserverFake observer = new() { Before = (_, _) => throw new InvalidOperationException("The observer is down.") };
        await using DispatchEngine engine = await StartAsync(
            alerts,
            configuration => configuration["Hermes:Observer:Enabled"] = "true",
            services => services.AddSingleton<IDeliveryObserver>(observer));
        await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]));
        await engine.Host.DispatchAsync();
        await ExecuteAsync(engine.Host, connection => new("UPDATE messaging.notices SET at = @at", connection) { Parameters = { new("at", engine.Host.Clock.GetUtcNow().AddMinutes(-16)) } });

        await Watching.RunAsync(engine.Host);
        AlertSinkFake.AlertCall raised = Assert.Single(alerts.Calls, call => call.Condition == "observer-failing");
        observer.Before = null;
        await Watching.RunAsync(engine.Host, "observer", Watching.Interval);
        await Watching.UntilAsync(async () => await NoticesAsync(engine.Host) == 0);
        await Watching.RunAsync(engine.Host);

        Assert.Equal((true, (string?)null, "965"), (raised.Raised, raised.Scope, raised.Details!["age"]));
        Assert.Single(alerts.Calls, call => !call.Raised && call.Condition == "observer-failing");
    }

    [Fact]
    public async Task MSG_SMS_004_RejectedReportsOverTheThresholdAreRaisedAgainAfterTheRepeatAndClearedOnceTheWindowHasPassed()
    {
        AlertSinkFake alerts = new();
        await using DispatchEngine engine = await StartAsync(alerts);
        await ExecuteAsync(engine.Host, connection => new("INSERT INTO messaging.rejected_reports (minute, count) VALUES (@at, 21)", connection) { Parameters = { new("at", engine.Host.Clock.GetUtcNow()) } });

        await Watching.RunAsync(engine.Host);
        await Watching.RunAsync(engine.Host);
        int once = alerts.Calls.Count(call => call.Raised && call.Condition == "sms-report-rejected");
        await ExecuteAsync(engine.Host, connection => new("UPDATE messaging.alerts SET last_raised_at = @at WHERE condition = 'sms-report-rejected'", connection) { Parameters = { new("at", engine.Host.Clock.GetUtcNow().AddHours(-1)) } });
        await Watching.RunAsync(engine.Host);
        int again = alerts.Calls.Count(call => call.Raised && call.Condition == "sms-report-rejected");
        await ExecuteAsync(engine.Host, connection => new("UPDATE messaging.rejected_reports SET minute = @at", connection) { Parameters = { new("at", engine.Host.Clock.GetUtcNow().AddHours(-1)) } });
        await Watching.RunAsync(engine.Host);

        Assert.Equal((1, 2), (once, again));
        Assert.Equal("21", alerts.Calls.First(call => call.Condition == "sms-report-rejected").Details!["count"]);
        Assert.Single(alerts.Calls, call => !call.Raised && call.Condition == "sms-report-rejected");
    }

    [Fact]
    public async Task MSG_PUSH_005_PushAuthFailingIsJudgedPerServiceAndClearedOnceTheWindowHasPassed()
    {
        AlertSinkFake alerts = new();
        await using DispatchEngine engine = await StartAsync(alerts);
        await ExecuteAsync(engine.Host, connection => new("INSERT INTO messaging.push_auth_failures (service, minute, count) VALUES ('fcm', @at, 21), ('mozilla', @at, 20)", connection) { Parameters = { new("at", engine.Host.Clock.GetUtcNow()) } });

        await Watching.RunAsync(engine.Host);
        AlertSinkFake.AlertCall raised = Assert.Single(alerts.Calls, call => call.Condition == "push-auth-failing");
        await ExecuteAsync(engine.Host, connection => new("UPDATE messaging.push_auth_failures SET minute = @at", connection) { Parameters = { new("at", engine.Host.Clock.GetUtcNow().AddHours(-1)) } });
        await Watching.RunAsync(engine.Host);

        Assert.Equal((true, "fcm"), (raised.Raised, raised.Scope));
        Assert.Equal(("fcm", "21"), (raised.Details!["service"], raised.Details["count"]));
        Assert.Single(alerts.Calls, call => !call.Raised && call.Condition == "push-auth-failing" && call.Scope == "fcm");
        Assert.DoesNotContain(alerts.Calls, call => call.Scope == "mozilla");
    }

    [Theory]
    [InlineData("newer-release", "")]
    [InlineData("key-unavailable", "hashKeys")]
    [InlineData("content-key-limit", "1")]
    [InlineData("content-rejected", "deliveries")]
    public async Task AConditionAProcessWritesAnInstantFor_IsClearedOnceThatInstantHasPassedAndNotBefore(string condition, string scope)
    {
        AlertSinkFake alerts = new();
        await using DispatchEngine engine = await StartAsync(alerts);
        DateTimeOffset now = engine.Host.Clock.GetUtcNow();
        await ExecuteAsync(
            engine.Host,
            connection => new("INSERT INTO messaging.alerts (condition, scope, holding, last_raised_at, holds_until) VALUES (@condition, @scope, true, @at, @until)", connection)
            {
                Parameters = { new("condition", condition), new("scope", scope), new("at", now), new("until", now.AddSeconds(8)) },
            });

        await Watching.RunAsync(engine.Host);
        bool early = alerts.Calls.Any(call => call.Condition == condition);
        await Watching.RunAsync(engine.Host);
        await Watching.RunAsync(engine.Host);

        Assert.False(early);
        AlertSinkFake.AlertCall cleared = Assert.Single(alerts.Calls, call => call.Condition == condition);
        Assert.Equal((false, string.IsNullOrEmpty(scope) ? null : scope), (cleared.Raised, cleared.Scope));
    }

    private static MeterListener Listen(EngineHost host, ConcurrentQueue<(double Age, string? Channel)> ages)
    {
        IMeterFactory factory = host.Services.GetRequiredService<IMeterFactory>();
        MeterListener listener = new();
        listener.InstrumentPublished = (instrument, meters) =>
        {
            if (instrument.Meter.Scope == factory && instrument.Name == "hermes.dispatch.oldest_due_age")
            {
                meters.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((_, value, tags, _) =>
        {
            string? channel = null;
            foreach (KeyValuePair<string, object?> tag in tags)
            {
                if (tag.Key == "channel")
                {
                    channel = tag.Value as string;
                }
            }

            ages.Enqueue((value, channel));
        });
        listener.Start();
        return listener;
    }

    private static async Task ExecuteAsync(EngineHost host, Func<NpgsqlConnection, NpgsqlCommand> statement)
    {
        await using NpgsqlConnection connection = new(host.Connection);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = statement(connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<long> NoticesAsync(EngineHost host)
    {
        await using NpgsqlConnection connection = new(host.Connection);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("SELECT count(*) FROM messaging.notices", connection);
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private Task<DispatchEngine> StartAsync(AlertSinkFake alerts, Action<Dictionary<string, string?>>? configure = null, Action<IServiceCollection>? services = null)
    {
        return DispatchEngine.StartAsync(
            postgres,
            configure: configuration =>
            {
                configuration["Hermes:Jobs:Enabled"] = "true";
                configure?.Invoke(configuration);
            },
            services: collection =>
            {
                collection.AddSingleton<IAlertSink>(alerts);
                services?.Invoke(collection);
            });
    }
}