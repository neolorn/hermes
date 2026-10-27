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
public sealed class SendRecordTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(500);

    [Fact]
    public async Task MSG_SEND_004_AC1_ASendInAHostTransactionThatRollsBackLeavesNothingAndWakesNoDispatcher()
    {
        await using EngineHost host = await EngineHost.StartAsync(await EngineHost.DatabaseAsync(postgres));
        await using NpgsqlConnection listener = await ListenAsync(host);
        await using NpgsqlConnection connection = await OpenAsync(host);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

        Result<SendReceipt> result = await InAsync(host, SendTests.Code(), transaction);
        await transaction.RollbackAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal((0, 0, 0), await host.RecordedAsync());
        Assert.False(await listener.WaitAsync(Quiet, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MSG_SEND_004_AC2_ASendInAHostTransactionIsUnseenBeforeItCommitsAndNotifiesTheDispatchersWhenItDoes()
    {
        await using EngineHost host = await EngineHost.StartAsync(await EngineHost.DatabaseAsync(postgres));
        await using NpgsqlConnection listener = await ListenAsync(host);
        await using NpgsqlConnection connection = await OpenAsync(host);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        List<string> channels = [];
        listener.Notification += (_, notice) => channels.Add(notice.Channel);

        Result<SendReceipt> result = await InAsync(host, SendTests.Code(), transaction);
        (long, long, long) before = await host.RecordedAsync();
        bool notifiedBefore = await listener.WaitAsync(Quiet, TestContext.Current.CancellationToken);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal((0, 0, 0), before);
        Assert.False(notifiedBefore);
        Assert.True(await listener.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.Equal(["messaging_dispatch"], channels);
        Assert.Equal((1, 3, 0), await host.RecordedAsync());
    }

    [Fact]
    public async Task Send_WithTheNotificationOff_NotifiesNone()
    {
        await using EngineHost host = await EngineHost.StartAsync(await EngineHost.DatabaseAsync(postgres), configure: configuration => configuration["Hermes:Dispatch:Notify"] = "false");
        await using NpgsqlConnection listener = await ListenAsync(host);

        Assert.True((await host.SendAsync(SendTests.Code())).IsSuccess);

        Assert.False(await listener.WaitAsync(Quiet, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Send_OnATransactionOtherThanAnOpenNpgsqlOne_IsAFault()
    {
        await using EngineHost host = await EngineHost.StartAsync(await EngineHost.DatabaseAsync(postgres));
        await using NpgsqlConnection connection = await OpenAsync(host);
        await using NpgsqlTransaction committed = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await committed.CommitAsync(TestContext.Current.CancellationToken);
        await using NpgsqlConnection other = await OpenAsync(host);
        await using NpgsqlTransaction closed = await other.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await other.CloseAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => InAsync(host, SendTests.Code(), committed));
        await Assert.ThrowsAsync<ArgumentException>(() => InAsync(host, SendTests.Code(), closed));
        await Assert.ThrowsAsync<ArgumentNullException>(() => InAsync(host, SendTests.Code(), null!));
        Assert.Equal((0, 0, 0), await host.RecordedAsync());
    }

    [Fact]
    public async Task Send_OnADatabaseMissingAMigration_IsAFault()
    {
        await using EngineHost host = await EngineHost.StartAsync(await EngineHost.DatabaseAsync(postgres));
        await using NpgsqlConnection connection = new(await postgres.NewDatabaseAsync());
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() => InAsync(host, SendTests.Code(), transaction));
    }

    [Fact]
    public async Task MSG_SEND_008_AC1_CancellingASendWithOneDeliveryWaitingAndOneSentCancelsTheFirstOnly()
    {
        await using EngineHost host = await EngineHost.StartAsync(await EngineHost.DatabaseAsync(postgres), configure: configuration => configuration["Hermes:Observer:Enabled"] = "true");
        SendReceipt receipt = (await host.SendAsync(SendTests.Code(channels: [Channel.Email, Channel.Sms]))).Value!;
        Guid sent = receipt.Deliveries.Single(delivery => delivery.Channel == Channel.Sms).Delivery.Value;
        Guid waiting = receipt.Deliveries.Single(delivery => delivery.Channel == Channel.Email).Delivery.Value;
        await MarkSentAsync(host, sent);
        host.Clock.Advance(TimeSpan.FromMinutes(1));

        Result<IReadOnlyList<DeliveryId>> cancelled = await host.WithMessagesAsync(messages => messages.CancelAsync(receipt.Send, TestContext.Current.CancellationToken).AsTask());
        Result<IReadOnlyList<DeliveryId>> again = await host.WithMessagesAsync(messages => messages.CancelAsync(receipt.Send, TestContext.Current.CancellationToken).AsTask());
        SendRecord record = (await host.WithMessagesAsync(messages => messages.ReadAsync(receipt.Send, TestContext.Current.CancellationToken).AsTask())).Value!;

        Assert.Equal([waiting], cancelled.Value!.Select(delivery => delivery.Value));
        Assert.Empty(again.Value!);
        Assert.Equal(DeliveryStatus.Cancelled, record.Deliveries.Single(delivery => delivery.Delivery.Value == waiting).Status);
        Assert.Equal(EngineHost.Start.AddMinutes(1), record.Deliveries.Single(delivery => delivery.Delivery.Value == waiting).LastMovedAt);
        Assert.Equal(DeliveryStatus.Sent, record.Deliveries.Single(delivery => delivery.Delivery.Value == sent).Status);
        Assert.Equal(1, await NoticesAsync(host, waiting));
        Assert.Equal(0, await NoticesAsync(host, sent));
    }

    [Fact]
    public async Task Cancel_WithTheObserverOff_WritesNoNotice()
    {
        await using EngineHost host = await EngineHost.StartAsync(await EngineHost.DatabaseAsync(postgres));
        SendReceipt receipt = (await host.SendAsync(SendTests.Code(channels: [Channel.Sms]))).Value!;

        Result<IReadOnlyList<DeliveryId>> cancelled = await host.WithMessagesAsync(messages => messages.CancelAsync(receipt.Send, TestContext.Current.CancellationToken).AsTask());

        Assert.Single(cancelled.Value!);
        Assert.Equal(0, await NoticesAsync(host, receipt.Deliveries[0].Delivery.Value));
    }

    [Fact]
    public async Task CancelAndRead_ASendTheLibraryDoesNotHold_AreAnsweredWithUnknown()
    {
        await using EngineHost host = await EngineHost.StartAsync(await EngineHost.DatabaseAsync(postgres));
        SendId unknown = new(Guid.CreateVersion7());

        Result<IReadOnlyList<DeliveryId>> cancelled = await host.WithMessagesAsync(messages => messages.CancelAsync(unknown, TestContext.Current.CancellationToken).AsTask());
        Result<SendRecord> read = await host.WithMessagesAsync(messages => messages.ReadAsync(unknown, TestContext.Current.CancellationToken).AsTask());

        Assert.Equal("messaging.send.unknown", cancelled.Failure!.Code);
        Assert.Equal("messaging.send.unknown", read.Failure!.Code);
    }

    [Fact]
    public async Task Read_ASend_AnswersItsKindDeliveriesAndInstants()
    {
        await using EngineHost host = await EngineHost.StartAsync(await EngineHost.DatabaseAsync(postgres));
        SendReceipt receipt = (await host.SendAsync(SendTests.Code(channels: [Channel.Sms]))).Value!;

        SendRecord record = (await host.WithMessagesAsync(messages => messages.ReadAsync(receipt.Send, TestContext.Current.CancellationToken).AsTask())).Value!;

        Assert.Equal(receipt.Send, record.Send);
        Assert.Equal(EngineHost.Kind, record.Kind);
        DeliveryRecord delivery = Assert.Single(record.Deliveries);
        Assert.Equal(Channel.Sms, delivery.Channel);
        Assert.Null(delivery.Reason);
        Assert.Equal(EngineHost.Start, delivery.RecordedAt);
        Assert.Equal(EngineHost.Start, delivery.LastMovedAt);
        Assert.Empty(delivery.Attempts);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MSG_SEND_009_AC1_TwoSendsWithOneKeyInsideTheWindowYieldOneSendAndTheSameReceipt(bool anonymous)
    {
        await using EngineHost host = await EngineHost.StartAsync(await EngineHost.DatabaseAsync(postgres));
        Send send = Keyed(anonymous);

        Result<SendReceipt> first = await host.SendAsync(send);
        host.Clock.Advance(TimeSpan.FromHours(23));
        Result<SendReceipt> second = await host.SendAsync(send);

        Assert.Equal(first.Value!.Send, second.Value!.Send);
        Assert.Equal(first.Value.Deliveries, second.Value.Deliveries);
        Assert.Equal((1, anonymous ? 1 : 2, 1), await host.RecordedAsync());
    }

    [Fact]
    public async Task MSG_SEND_009_AC1_TwoSendsWithOneKeyInsideHostTransactionsYieldOneSend()
    {
        await using EngineHost host = await EngineHost.StartAsync(await EngineHost.DatabaseAsync(postgres));
        List<SendId> sends = [];
        for (int index = 0; index < 2; index++)
        {
            await using NpgsqlConnection connection = await OpenAsync(host);
            await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
            sends.Add((await InAsync(host, Keyed(anonymous: false), transaction)).Value!.Send);
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        Assert.Single(sends.Distinct());
        Assert.Equal((1, 2, 1), await host.RecordedAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MSG_SEND_009_AC1_TwoSendsWithOneKeyAcrossAHashKeysWriteVersionChangeYieldOneSend(bool anonymous)
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        Result<SendReceipt> first;
        await using (EngineHost before = await EngineHost.StartAsync(connection))
        {
            first = await before.SendAsync(Keyed(anonymous));
        }

        await using EngineHost after = await EngineHost.StartAsync(connection, secrets: TwoHashKeys(connection), configure: WriteSecond);
        Result<SendReceipt> second = await after.SendAsync(Keyed(anonymous));

        Assert.Equal(first.Value!.Send, second.Value!.Send);
        Assert.Equal(1, (await after.RecordedAsync()).Sends);
    }

    [Fact]
    public async Task MSG_SEND_009_AC2_ASecondSendWithTheKeyAndAnotherValueIsRefusedAndRecordsNothing()
    {
        await using EngineHost host = await EngineHost.StartAsync(await EngineHost.DatabaseAsync(postgres));
        await host.SendAsync(Keyed(anonymous: false));

        Result<SendReceipt> other = await host.SendAsync(Keyed(anonymous: false, code: "654321"));

        Assert.Equal("messaging.send.idempotencyconflict", other.Failure!.Code);
        Assert.Equal((1, 2, 1), await host.RecordedAsync());
    }

    [Fact]
    public async Task MSG_SEND_009_AC2_ASecondSendWithTheSameValuesInAnotherOrderAnswersTheFirstReceipt()
    {
        await using EngineHost host = await EngineHost.StartAsync(await EngineHost.DatabaseAsync(postgres));
        Dictionary<string, SendValue> forward = new() { ["code"] = new SendValue.PlainText("123456"), ["total"] = new SendValue.WholeNumber(3) };
        Dictionary<string, SendValue> backward = new() { ["total"] = new SendValue.WholeNumber(3), ["code"] = new SendValue.PlainText("123456") };

        Result<SendReceipt> first = await host.SendAsync(Keyed(anonymous: false) with { Values = forward });
        Result<SendReceipt> second = await host.SendAsync(Keyed(anonymous: false) with { Values = backward });

        Assert.Equal(first.Value!.Send, second.Value!.Send);
    }

    [Fact]
    public async Task MSG_SEND_009_AC3_ASendWithAKeyWhoseRowHasLapsedIsRecordedAsANewSend()
    {
        await using EngineHost host = await EngineHost.StartAsync(await EngineHost.DatabaseAsync(postgres));
        Result<SendReceipt> first = await host.SendAsync(Keyed(anonymous: false));
        host.Clock.Advance(TimeSpan.FromDays(1));

        Result<SendReceipt> second = await host.SendAsync(Keyed(anonymous: false, code: "654321"));

        Assert.NotEqual(first.Value!.Send, second.Value!.Send);
        Assert.Equal((2, 4, 1), await host.RecordedAsync());
    }

    [Fact]
    public async Task MSG_SEND_009_AC4_ARowWrittenUnderAWindowOf7DaysIsLiveOnDay2ToAnApplicationWhoseWindowIs1Day()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        ManualClock clock = new(EngineHost.Start);
        Result<SendReceipt> first;
        await using (EngineHost week = await EngineHost.StartAsync(connection, configure: configuration => configuration["Hermes:Idempotency:Window"] = "7.00:00:00", clock: clock))
        {
            first = await week.SendAsync(Keyed(anonymous: false));
        }

        clock.Advance(TimeSpan.FromDays(2));
        await using EngineHost day = await EngineHost.StartAsync(connection, clock: clock);
        Result<SendReceipt> second = await day.SendAsync(Keyed(anonymous: false));

        Assert.Equal(first.Value!.Send, second.Value!.Send);
    }

    [Fact]
    public async Task Send_WithAKeyWhileAHashKeysVersionInUseIsNotHeld_IsRefusedWithKeyUnavailable()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        await using EngineHost one = await EngineHost.StartAsync(connection);
        await using (EngineHost two = await EngineHost.StartAsync(connection, secrets: TwoHashKeys(connection), configure: WriteSecond))
        {
            Assert.True((await two.SendAsync(Keyed(anonymous: true))).IsSuccess);
        }

        Result<SendReceipt> result = await one.SendAsync(Keyed(anonymous: true));

        Assert.Equal("messaging.key.unavailable", result.Failure!.Code);
        Assert.Equal(new Dictionary<string, string> { ["key"] = "hashKeys" }, result.Failure.Details);
        Assert.Equal((1, 1, 1), await one.RecordedAsync());
    }

    private static Send Keyed(bool anonymous, string code = "123456")
    {
        return SendTests.Code(phone: anonymous ? null : "+201000000001", recipient: anonymous ? new Recipient.Anonymous() : null, code: code, channels: anonymous ? [Channel.Email] : [Channel.Email, Channel.Sms])
            with
        { IdempotencyKey = "order-1001" };
    }

    private static void WriteSecond(Dictionary<string, string?> configuration)
    {
        configuration["Hermes:Secrets:HashKeys:WriteVersion"] = "2";
    }

    private static SecretSourceFake TwoHashKeys(string connection)
    {
        return Secrets.Held(connection).WithVersions("hashKeys", (1, Secrets.Bytes(32, 2)), (2, Secrets.Bytes(32, 5)));
    }

    private static async Task<Result<SendReceipt>> InAsync(EngineHost host, Send send, NpgsqlTransaction transaction)
    {
        AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using (scope)
        {
            return await scope.ServiceProvider.GetRequiredService<IMessages>().SendAsync(send, transaction, TestContext.Current.CancellationToken);
        }
    }

    private static async Task<NpgsqlConnection> OpenAsync(EngineHost host)
    {
        NpgsqlConnection connection = new(host.Connection);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return connection;
    }

    private static async Task<NpgsqlConnection> ListenAsync(EngineHost host)
    {
        NpgsqlConnection connection = await OpenAsync(host);
        await using NpgsqlCommand listen = new("LISTEN messaging_dispatch", connection);
        await listen.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        return connection;
    }

    private static Task<int> MarkSentAsync(EngineHost host, Guid delivery)
    {
        return host.RunAsync(connection =>
        {
            NpgsqlCommand command = new("UPDATE messaging.deliveries SET status = 'sent' WHERE id = @id", connection);
            command.Parameters.AddWithValue("id", delivery);
            return command;
        });
    }

    private static async Task<long> NoticesAsync(EngineHost host, Guid delivery)
    {
        await using NpgsqlConnection connection = await OpenAsync(host);
        await using NpgsqlCommand command = new("SELECT count(*) FROM messaging.notices WHERE delivery_id = @id AND status = 'cancelled'", connection);
        command.Parameters.AddWithValue("id", delivery);
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }
}