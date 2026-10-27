using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "integration")]
public sealed class BalanceTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly TimeSpan Poll = TimeSpan.FromMinutes(1);

    [Fact]
    public async Task MSG_SMS_006_AC1_BelowTheFloorAnOptionalCategorysSmsEndsBalanceFloorAndAnEssentialOnesIsSent()
    {
        await using DispatchEngine engine = await StartAsync(configuration => configuration["Hermes:Sms:BalanceFloor"] = "10");
        engine.Sms.Balance = () => new BalanceReading.Balance(5);
        await PollAsync(engine.Host);

        SendReceipt optional = (await engine.Host.SendAsync(LimitTests.ReminderTo("+201000000001"))).Value!;
        SendReceipt essential = (await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]))).Value!;
        await engine.Host.DispatchAsync();

        DeliveryRecord floored = Assert.Single((await engine.Host.ReadAsync(optional.Send)).Deliveries);
        Assert.Equal(DeliveryStatus.BalanceFloor, floored.Status);
        Assert.Empty(floored.Attempts);
        Assert.Equal(DeliveryStatus.Sent, await SuppressionTests.StatusAsync(engine.Host, essential));
        Assert.Single(engine.Sms.Received);
    }

    [Fact]
    public async Task MSG_SMS_006_AC1_UntilAFirstBalanceIsRecordedTheFloorRefusesNothing()
    {
        await using DispatchEngine engine = await StartAsync(configuration => configuration["Hermes:Sms:BalanceFloor"] = "10");

        SendReceipt optional = (await engine.Host.SendAsync(LimitTests.ReminderTo("+201000000001"))).Value!;
        await engine.Host.DispatchAsync();

        Assert.Equal(DeliveryStatus.Sent, await SuppressionTests.StatusAsync(engine.Host, optional));
    }

    [Fact]
    public async Task MSG_SMS_006_AC2_ADrainOverTheFactorRaisesTheAlertThroughTheAlertSink()
    {
        AlertSinkFake alerts = new();
        await using DispatchEngine engine = await StartAsync(Drain, alerts);
        decimal units = 100_000;
        engine.Sms.Balance = () => new BalanceReading.Balance(units);

        for (int minute = 0; minute <= 12; minute++)
        {
            await PollAsync(engine.Host);
            units -= 1;
        }

        bool raisedBefore = Raised(alerts);
        units -= 30;
        await PollAsync(engine.Host);

        Assert.False(raisedBefore);
        AlertSinkFake.AlertCall raised = Assert.Single(alerts.Calls, call => call.Raised && call.Condition == "sms-balance");
        Assert.Equal("drain", raised.Details!["which"]);
        Assert.Equal((units).ToString(System.Globalization.CultureInfo.InvariantCulture), raised.Details["balance"]);
    }

    [Fact]
    public async Task MSG_SMS_006_AC2_BeforeTheMinimumHistoryTheFirstSpendRaisesNothing()
    {
        AlertSinkFake alerts = new();
        await using DispatchEngine engine = await StartAsync(Drain, alerts);
        decimal units = 100_000;
        engine.Sms.Balance = () => new BalanceReading.Balance(units);

        await PollAsync(engine.Host);
        units -= 30;
        await PollAsync(engine.Host);

        Assert.Equal(2L, await ReadingsAsync(engine.Host));
        Assert.False(Raised(alerts));
    }

    [Fact]
    public async Task MSG_SMS_006_AC3_WithAProviderThatReportsNoBalanceNoSmsEndsBalanceFloorAndNoBalanceAlertIsRaised()
    {
        AlertSinkFake alerts = new();
        await using DispatchEngine engine = await StartAsync(configuration => configuration["Hermes:Sms:BalanceFloor"] = "1000", alerts);

        await PollAsync(engine.Host);
        SendReceipt optional = (await engine.Host.SendAsync(LimitTests.ReminderTo("+201000000001"))).Value!;
        await engine.Host.DispatchAsync();

        Assert.Equal(DeliveryStatus.Sent, await SuppressionTests.StatusAsync(engine.Host, optional));
        Assert.Equal(0L, await ReadingsAsync(engine.Host));
        Assert.DoesNotContain(alerts.Calls, call => call.Condition.StartsWith("sms-balance", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Balance_ReadsFailingForTheUnreadableSpan_RaiseSmsBalanceUnreadableSinceTheLastReadAndAReadClearsIt()
    {
        AlertSinkFake alerts = new();
        await using DispatchEngine engine = await StartAsync(configuration => configuration["Hermes:Sms:BalanceUnreadableAfter"] = "00:05:00", alerts);
        bool reads = true;
        int throws = 0;
        engine.Sms.Balance = () => reads ? new BalanceReading.Balance(100) : ++throws % 2 == 0 ? throw new InvalidOperationException() : new BalanceReading.Failed(AttemptReason.Temporary);
        await PollAsync(engine.Host);
        DateTimeOffset read = engine.Host.Clock.GetUtcNow();
        reads = false;

        for (int minute = 0; minute < 4; minute++)
        {
            await PollAsync(engine.Host);
        }

        bool raisedEarly = alerts.Calls.Any(call => call.Condition == "sms-balance-unreadable");
        await PollAsync(engine.Host);
        reads = true;
        await PollAsync(engine.Host);

        Assert.False(raisedEarly);
        AlertSinkFake.AlertCall raised = Assert.Single(alerts.Calls, call => call.Raised && call.Condition == "sms-balance-unreadable");
        Assert.Equal(read.ToString("O", System.Globalization.CultureInfo.InvariantCulture), raised.Details!["since"]);
        Assert.Single(alerts.Calls, call => !call.Raised && call.Condition == "sms-balance-unreadable");
        Assert.Equal(7L, await ReadingsAsync(engine.Host));
    }

    [Fact]
    public async Task MSG_OPS_004_AC2_TwoProcessesRunningTheBalanceJobStartedSomeSecondsApartRecordOneReadingPerPoll()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        ManualClock later = new(EngineHost.Start.AddSeconds(5));
        await using DispatchEngine one = await StartAsync(connection: connection);
        await using DispatchEngine two = await StartAsync(connection: connection, clock: later);
        one.Sms.Balance = () => new BalanceReading.Balance(100);
        two.Sms.Balance = () => new BalanceReading.Balance(100);

        for (int poll = 1; poll <= 3; poll++)
        {
            one.Host.Clock.Advance(Poll);
            two.Host.Clock.Advance(Poll);
            await UntilAsync(async () => await ReadingsAsync(one.Host) == poll);
            await Task.Delay(200, TestContext.Current.CancellationToken);
        }

        Assert.Equal(3L, await ReadingsAsync(one.Host));
        Assert.Equal(3, one.Sms.BalanceReads + two.Sms.BalanceReads);
    }

    [Fact]
    public async Task MSG_OPS_004_AC4_WithDispatchOffNoBalanceJobRunsAndWithItOnWhereSmsIsUsedItRunsWithTheJobsOff()
    {
        await using DispatchEngine off = await StartAsync(configuration => configuration["Hermes:Dispatch:Enabled"] = "false");
        off.Sms.Balance = () => new BalanceReading.Balance(100);
        await using DispatchEngine on = await StartAsync(configuration => configuration["Hermes:Jobs:Enabled"] = "false");
        on.Sms.Balance = () => new BalanceReading.Balance(100);

        off.Host.Clock.Advance(Poll);
        await PollAsync(on.Host);
        await Task.Delay(200, TestContext.Current.CancellationToken);

        Assert.Equal(0, off.Sms.BalanceReads);
        Assert.Null(await CompletedAsync(off.Host));
        Assert.Equal(1L, await ReadingsAsync(on.Host));
    }

    private static void Drain(Dictionary<string, string?> configuration)
    {
        configuration["Hermes:Sms:DrainWindow"] = "00:05:00";
        configuration["Hermes:Sms:DrainMinHistory"] = "00:10:00";
        configuration["Hermes:Sms:DrainBaseline"] = "00:30:00";
    }

    private static bool Raised(AlertSinkFake alerts)
    {
        return alerts.Calls.Any(call => call.Raised && call.Condition == "sms-balance");
    }

    /// <summary>Moves the clock one poll on and waits for the run it starts to complete.</summary>
    private static async Task PollAsync(EngineHost host)
    {
        DateTimeOffset? before = await CompletedAsync(host);
        host.Clock.Advance(Poll);
        await UntilAsync(async () => await CompletedAsync(host) is { } completed && (before is null || completed > before));
    }

    private static async Task<DateTimeOffset?> CompletedAsync(EngineHost host)
    {
        await using NpgsqlConnection connection = new(host.Connection);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("SELECT last_completed_at FROM messaging.jobs WHERE name = 'balance'", connection);
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) is DateTime completed ? new DateTimeOffset(completed, TimeSpan.Zero) : null;
    }

    private static async Task<long> ReadingsAsync(EngineHost host)
    {
        await using NpgsqlConnection connection = new(host.Connection);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("SELECT count(*) FROM messaging.balance_readings", connection);
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private static async Task UntilAsync(Func<Task<bool>> condition)
    {
        for (int tries = 0; tries < 400; tries++)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(25, TestContext.Current.CancellationToken);
        }

        Assert.Fail("The condition did not come to hold.");
    }

    private async Task<DispatchEngine> StartAsync(
        Action<Dictionary<string, string?>>? configure = null,
        AlertSinkFake? alerts = null,
        string? connection = null,
        ManualClock? clock = null)
    {
        return await DispatchEngine.StartAsync(
            postgres,
            configure: configuration =>
            {
                configuration["Hermes:Sms:BalancePoll"] = "00:01:00";
                configure?.Invoke(configuration);
            },
            declare: LimitTests.Declare(_ => { }),
            services: services =>
            {
                if (alerts is not null)
                {
                    services.AddSingleton<IAlertSink>(alerts);
                }
            },
            connection: connection,
            clock: clock);
    }
}