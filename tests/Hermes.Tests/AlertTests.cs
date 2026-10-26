using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Operations;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Npgsql;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "integration")]
public sealed class AlertTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Midnight = new(2026, 10, 26, 0, 0, 0, TimeSpan.Zero);

    private static readonly Dictionary<string, string> KeyDetails = new(StringComparer.Ordinal) { ["key"] = "QuietHours:Spread" };

    [Fact]
    public async Task MSG_OPS_006_AC1_AConditionHoldingFromMidnightToHalfPastTwoIsRaisedEachHourAndClearedOnce()
    {
        string connection = await MigratedAsync();
        ManualClock clock = new(Midnight);
        AlertSinkFake sink = new();
        await using Process process = Open(connection, clock, sink);
        List<DateTimeOffset> raised = [];
        List<DateTimeOffset> cleared = [];

        for (DateTimeOffset now = Midnight; now <= Midnight.AddHours(3); now = now.AddMinutes(10))
        {
            clock.Advance(now - clock.GetUtcNow());
            int before = sink.Calls.Count;
            if (now < Midnight.AddHours(2.5))
            {
                await process.Alerts.HoldingAsync("configuration-invalid", "QuietHours:Spread", KeyDetails, TestContext.Current.CancellationToken);
            }
            else
            {
                await process.Alerts.NotHoldingAsync("configuration-invalid", "QuietHours:Spread", TestContext.Current.CancellationToken);
            }

            foreach (AlertSinkFake.AlertCall call in sink.Calls.Skip(before))
            {
                (call.Raised ? raised : cleared).Add(now);
            }
        }

        Assert.Equal([Midnight, Midnight.AddHours(1), Midnight.AddHours(2)], raised);
        Assert.Equal([Midnight.AddHours(2.5)], cleared);
        Assert.All(sink.Calls.Where(call => call.Raised), call => Assert.Equal(("configuration-invalid", "QuietHours:Spread", "QuietHours:Spread"), (call.Condition, call.Scope, call.Details!["key"])));
    }

    [Fact]
    public async Task MSG_OPS_006_AC2_TwoProcessesSeeingTheSameConditionRaiseItOnce()
    {
        string connection = await MigratedAsync();
        ManualClock clock = new(Midnight);
        AlertSinkFake first = new();
        AlertSinkFake second = new();
        await using Process one = Open(connection, clock, first);
        await using Process two = Open(connection, clock, second);

        await Task.WhenAll(
            one.Alerts.HoldingAsync("configuration-invalid", "QuietHours:Spread", KeyDetails, TestContext.Current.CancellationToken),
            two.Alerts.HoldingAsync("configuration-invalid", "QuietHours:Spread", KeyDetails, TestContext.Current.CancellationToken));
        clock.Advance(TimeSpan.FromMinutes(10));
        await Task.WhenAll(
            one.Alerts.NotHoldingAsync("configuration-invalid", "QuietHours:Spread", TestContext.Current.CancellationToken),
            two.Alerts.NotHoldingAsync("configuration-invalid", "QuietHours:Spread", TestContext.Current.CancellationToken));

        AlertSinkFake.AlertCall[] calls = [.. first.Calls, .. second.Calls];
        Assert.Single(calls, call => call.Raised);
        Assert.Single(calls, call => !call.Raised);
    }

    [Fact]
    public async Task HoldsUntil_EachWrite_RecordsNowPlusTwiceTheIntervalWhereLaterAndRaisesWhenItStartsToHold()
    {
        string connection = await MigratedAsync();
        ManualClock clock = new(Midnight);
        AlertSinkFake sink = new();
        await using Process process = Open(connection, clock, sink);
        await RecordNewerReleaseUntilAsync(connection, Midnight.AddMinutes(1));
        Dictionary<string, string> details = new(StringComparer.Ordinal) { ["migration"] = "29990101000000_Later" };

        await process.Alerts.HoldsUntilAsync("newer-release", null, details, TestContext.Current.CancellationToken);
        DateTimeOffset? kept = await HoldsUntilAsync(connection, "newer-release", string.Empty);
        clock.Advance(TimeSpan.FromMinutes(1));
        await process.Alerts.HoldsUntilAsync("newer-release", null, details, TestContext.Current.CancellationToken);

        Assert.Equal(Midnight.AddMinutes(1), kept);
        Assert.Equal(Midnight.AddMinutes(1).AddSeconds(10), await HoldsUntilAsync(connection, "newer-release", string.Empty));
        AlertSinkFake.AlertCall raised = Assert.Single(sink.Calls);
        Assert.Equal((true, "newer-release", (string?)null, "29990101000000_Later"), (raised.Raised, raised.Condition, raised.Scope, raised.Details!["migration"]));
    }

    [Fact]
    public async Task HoldsUntil_AgainWithinTheInterval_WritesNothing()
    {
        string connection = await MigratedAsync();
        ManualClock clock = new(Midnight);
        await using Process process = Open(connection, clock, new AlertSinkFake());
        Dictionary<string, string> details = new(StringComparer.Ordinal) { ["table"] = "deliveries" };

        await process.Alerts.HoldsUntilAsync("content-rejected", "deliveries", details, TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(4));
        await process.Alerts.HoldsUntilAsync("content-rejected", "deliveries", details, TestContext.Current.CancellationToken);

        Assert.Equal(Midnight.AddSeconds(10), await HoldsUntilAsync(connection, "content-rejected", "deliveries"));
    }

    [Fact]
    public async Task Timer_ReloadedValueOutsideItsRule_RaisesConfigurationInvalidUntilItIsPutRight()
    {
        string connection = await MigratedAsync();
        ManualClock clock = new(Midnight);
        AlertSinkFake sink = new();
        using IHost host = Host(connection, clock, sink);
        await host.StartAsync(TestContext.Current.CancellationToken);
        IConfigurationRoot configuration = (IConfigurationRoot)host.Services.GetRequiredService<IConfiguration>();

        configuration["Hermes:QuietHours:Spread"] = "05:00:00";
        configuration.Reload();
        clock.Advance(TimeSpan.FromSeconds(5));
        AlertSinkFake.AlertCall raised = await WaitForAsync(sink, 1);
        configuration["Hermes:QuietHours:Spread"] = "01:00:00";
        configuration.Reload();
        clock.Advance(TimeSpan.FromSeconds(5));
        AlertSinkFake.AlertCall cleared = await WaitForAsync(sink, 2);
        await host.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal((true, "configuration-invalid", "QuietHours:Spread", "QuietHours:Spread"), (raised.Raised, raised.Condition, raised.Scope, raised.Details!["key"]));
        Assert.Equal((false, "configuration-invalid", "QuietHours:Spread"), (cleared.Raised, cleared.Condition, cleared.Scope));
    }

    [Fact]
    public async Task MSG_OPS_008_AC9_AMigrationThisReleaseDoesNotKnowRaisesNewerReleaseWithinTheInterval()
    {
        string connection = await MigratedAsync();
        await RecordLaterMigrationAsync(connection);
        ManualClock clock = new(Midnight);
        AlertSinkFake sink = new();
        using IHost host = Host(connection, clock, sink);
        await host.StartAsync(TestContext.Current.CancellationToken);

        clock.Advance(TimeSpan.FromSeconds(5));
        AlertSinkFake.AlertCall raised = await WaitForAsync(sink, 1);
        await host.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal((true, "newer-release", (string?)null, "29990101000000_Later"), (raised.Raised, raised.Condition, raised.Scope, raised.Details!["migration"]));
        Assert.Equal(Midnight.AddSeconds(15), await HoldsUntilAsync(connection, "newer-release", string.Empty));
    }

    [Fact]
    public async Task Timer_WriteVersionUsedForMoreThanMaxValues_RaisesContentKeyLimit()
    {
        string connection = await MigratedAsync();
        ManualClock clock = new(Midnight);
        AlertSinkFake sink = new();
        using IHost host = Host(connection, clock, sink, configuration => configuration["Hermes:Secrets:ContentKeys:MaxValues"] = "100");
        await host.StartAsync(TestContext.Current.CancellationToken);
        await SetValueCountAsync(connection, 101);

        clock.Advance(TimeSpan.FromSeconds(5));
        AlertSinkFake.AlertCall raised = await WaitForAsync(sink, 1);
        await host.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal((true, "content-key-limit", "1"), (raised.Raised, raised.Condition, raised.Scope));
        Assert.Equal(new Dictionary<string, string>(StringComparer.Ordinal) { ["version"] = "1", ["count"] = "101" }, raised.Details);
    }

    [Fact]
    public async Task Timer_WriteVersionAtMaxValues_RaisesNothing()
    {
        string connection = await MigratedAsync();
        ManualClock clock = new(Midnight);
        AlertSinkFake sink = new();
        using IHost host = Host(connection, clock, sink, configuration => configuration["Hermes:Secrets:ContentKeys:MaxValues"] = "100");
        await host.StartAsync(TestContext.Current.CancellationToken);
        await SetValueCountAsync(connection, 100);

        clock.Advance(TimeSpan.FromSeconds(5));
        await WaitForLastMadeAsync(connection, Midnight.AddSeconds(5));
        await host.StopAsync(TestContext.Current.CancellationToken);

        Assert.Empty(sink.Calls);
    }

    private static IHost Host(string connection, ManualClock clock, AlertSinkFake sink, Action<Dictionary<string, string?>>? configure = null)
    {
        Dictionary<string, string?> configuration = TestHost.Languages();
        configure?.Invoke(configuration);
        return TestHost.Build(
            configuration,
            declare =>
            {
                declare.Category("security").Essential();
                declare.Kind("sign.in", "security").Sms();
            },
            services => services.AddSingleton<ISecretSource>(Secrets.Held(connection)).AddSingleton<TimeProvider>(clock).AddSingleton<IAlertSink>(sink));
    }

    private static Process Open(string connection, ManualClock clock, AlertSinkFake sink)
    {
        ServiceCollection services = new();
        services.AddOptions<MessagingOptions>().Configure(options => options.Alerts.Repeat = TimeSpan.FromHours(1));
        services.AddSingleton<MessagingDatabase>();
        ServiceProvider provider = services.BuildServiceProvider();
        MessagingDatabase database = provider.GetRequiredService<MessagingDatabase>();
        database.Open(connection);
        return new Process(provider, new Alerts(database, provider.GetRequiredService<IOptionsMonitor<MessagingOptions>>(), sink, clock, Microsoft.Extensions.Logging.Abstractions.NullLogger<Alerts>.Instance));
    }

    private async Task<string> MigratedAsync()
    {
        string connection = await postgres.NewDatabaseAsync();
        Assert.Null(await SchemaMigration.ApplyAsync(connection, [], TestContext.Current.CancellationToken));
        return connection;
    }

    private static async Task<AlertSinkFake.AlertCall> WaitForAsync(AlertSinkFake sink, int count)
    {
        for (int attempt = 0; attempt < 200 && sink.Calls.Count < count; attempt++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(25), TestContext.Current.CancellationToken);
        }

        return sink.Calls.ElementAt(count - 1);
    }

    private static async Task WaitForLastMadeAsync(string connection, DateTimeOffset expected)
    {
        for (int attempt = 0; attempt < 200; attempt++)
        {
            await using NpgsqlConnection database = new(connection);
            await database.OpenAsync(TestContext.Current.CancellationToken);
            await using NpgsqlCommand command = new("SELECT last_made_at FROM messaging.key_versions WHERE key = 'hashKeys' AND version = 1", database);
            if (await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) is DateTime made && new DateTimeOffset(made) >= expected)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25), TestContext.Current.CancellationToken);
        }
    }

    private static async Task RecordNewerReleaseUntilAsync(string connection, DateTimeOffset until)
    {
        await using NpgsqlConnection database = new(connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("INSERT INTO messaging.alerts (condition, scope, holding, holds_until) VALUES ('newer-release', '', false, @until)", database);
        command.Parameters.AddWithValue("until", until);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task RecordLaterMigrationAsync(string connection)
    {
        await using NpgsqlConnection database = new(connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("INSERT INTO messaging.__migrations_history VALUES ('29990101000000_Later', '10.0.14')", database);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task SetValueCountAsync(string connection, long count)
    {
        await using NpgsqlConnection database = new(connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("UPDATE messaging.key_versions SET value_count = @count WHERE key = 'contentKeys' AND version = 1", database);
        command.Parameters.AddWithValue("count", count);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<DateTimeOffset?> HoldsUntilAsync(string connection, string condition, string scope)
    {
        await using NpgsqlConnection database = new(connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("SELECT holds_until FROM messaging.alerts WHERE condition = @condition AND scope = @scope", database);
        command.Parameters.AddWithValue("condition", condition);
        command.Parameters.AddWithValue("scope", scope);
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) is DateTime until ? new DateTimeOffset(until) : null;
    }

    private sealed class Process(ServiceProvider provider, Alerts alerts) : IAsyncDisposable
    {
        internal Alerts Alerts => alerts;

        public ValueTask DisposeAsync()
        {
            return provider.DisposeAsync();
        }
    }
}