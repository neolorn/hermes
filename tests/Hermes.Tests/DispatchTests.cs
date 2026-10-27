using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "integration")]
public sealed class DispatchTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly TimeSpan Initial = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task MSG_SEND_003_AC1_SendAsyncReturnsBeforeAnyProviderIsCalled()
    {
        await using Engine engine = await StartAsync();

        SendReceipt receipt = (await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Email, Channel.Sms]))).Value!;

        Assert.Empty(engine.Email.Received);
        Assert.Empty(engine.Sms.Received);
        Assert.Equal(2, await engine.Host.DispatchAsync());
        Assert.Single(engine.Email.Received);
        Assert.Single(engine.Sms.Received);
        Assert.All((await engine.Host.ReadAsync(receipt.Send)).Deliveries, delivery => Assert.Equal(DeliveryStatus.Sent, delivery.Status));
    }

    [Fact]
    public async Task MSG_SEND_007_AC3_AnAcceptedAnswerEndsTheDeliverySentWithItsProviderReference()
    {
        await using Engine engine = await StartAsync();
        engine.Sms.Answer = _ => new Attempt.Accepted("provider-42");
        SendReceipt receipt = (await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]))).Value!;
        engine.Host.Clock.Advance(TimeSpan.FromSeconds(1));

        await engine.Host.DispatchAsync();

        DeliveryRecord delivery = Assert.Single((await engine.Host.ReadAsync(receipt.Send)).Deliveries);
        Assert.Equal(DeliveryStatus.Sent, delivery.Status);
        Assert.Null(delivery.Reason);
        Assert.Equal(EngineHost.Start.AddSeconds(1), delivery.LastMovedAt);
        AttemptRecord attempt = Assert.Single(delivery.Attempts);
        Assert.Equal((1, AttemptClass.Accepted), (attempt.Number, attempt.AnswerClass!.Value));
        CallRecord call = Assert.Single(attempt.Calls);
        Assert.Equal(AttemptClass.Accepted, call.AnswerClass);
        Assert.Equal("provider-42", await engine.Host.ColumnAsync<string>(delivery.Delivery.Value, "provider_reference"));
    }

    [Theory]
    [InlineData("permanent", "failed", "recipientrefused")]
    [InlineData("transient", "waiting", "temporary")]
    public async Task MSG_SEND_007_AC3_EachAnswerClassLeadsToItsStatusAndTheAttemptKeepsItsClassReasonAndStatus(string answer, string status, string reason)
    {
        await using Engine engine = await StartAsync();
        engine.Sms.Answer = _ => answer == "permanent"
            ? new Attempt.Permanent(AttemptReason.RecipientRefused, 400)
            : new Attempt.Transient(AttemptReason.Temporary, 503, null);
        SendReceipt receipt = (await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]))).Value!;

        await engine.Host.DispatchAsync();

        DeliveryRecord delivery = Assert.Single((await engine.Host.ReadAsync(receipt.Send)).Deliveries);
        Assert.Equal(status, Spellings.Of(delivery.Status));
        Assert.Equal(reason, Spellings.Of(delivery.Reason!.Value));
        CallRecord call = Assert.Single(Assert.Single(delivery.Attempts).Calls);
        Assert.Equal((reason, answer == "permanent" ? 400 : 503), (Spellings.Of(call.Reason!.Value), call.Status!.Value));
        if (answer == "transient")
        {
            Assert.Equal(EngineHost.Start + Initial, await engine.Host.ColumnAsync<DateTime>(delivery.Delivery.Value, "due_at"));
        }
    }

    [Fact]
    public async Task MSG_SEND_016_AC1_ADeliveryAnsweredTransientThreeTimesThenAcceptedEndsSentAfterFourAttemptsAtGrowingIntervals()
    {
        await using Engine engine = await StartAsync();
        int calls = 0;
        engine.Sms.Answer = _ => ++calls <= 3 ? new Attempt.Transient(AttemptReason.Timeout, null, null) : new Attempt.Accepted(null);
        SendReceipt receipt = (await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]))).Value!;
        Guid id = receipt.Deliveries[0].Delivery.Value;
        List<TimeSpan> waits = [];

        for (int attempt = 1; attempt <= 4; attempt++)
        {
            Assert.Equal(1, await engine.Host.DispatchAsync());
            if (attempt < 4)
            {
                DateTimeOffset due = await engine.Host.ColumnAsync<DateTime>(id, "due_at");
                waits.Add(due - engine.Host.Clock.GetUtcNow());
                engine.Host.Clock.Advance(waits[^1] - TimeSpan.FromSeconds(1));
                Assert.Equal(0, await engine.Host.DispatchAsync());
                engine.Host.Clock.Advance(TimeSpan.FromSeconds(1));
            }
        }

        DeliveryRecord delivery = Assert.Single((await engine.Host.ReadAsync(receipt.Send)).Deliveries);
        Assert.Equal(DeliveryStatus.Sent, delivery.Status);
        Assert.Equal([1, 2, 3, 4], delivery.Attempts.Select(attempt => attempt.Number));
        Assert.Equal([Initial, Initial * 2, Initial * 4], waits);
    }

    [Fact]
    public async Task MSG_SEND_016_AC2_ADeliveryAnsweredTransientOnEveryAttemptEndsFailedAfterTheLast()
    {
        await using Engine engine = await StartAsync(configure: configuration => configuration["Hermes:Retry:MaxAttempts"] = "3");
        engine.Sms.Answer = _ => new Attempt.Transient(AttemptReason.ConnectionFailed, null, null);
        SendReceipt receipt = (await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]))).Value!;

        for (int attempt = 0; attempt < 5; attempt++)
        {
            await engine.Host.DispatchAsync();
            engine.Host.Clock.Advance(TimeSpan.FromHours(1));
        }

        DeliveryRecord delivery = Assert.Single((await engine.Host.ReadAsync(receipt.Send)).Deliveries);
        Assert.Equal((DeliveryStatus.Failed, AttemptReason.ConnectionFailed), (delivery.Status, delivery.Reason!.Value));
        Assert.Equal(3, delivery.Attempts.Count);
        Assert.Equal(3, engine.Sms.Received.Count);
    }

    [Theory]
    [InlineData(600, 600)]
    [InlineData(7200, 3600)]
    [InlineData(10, 30)]
    public async Task MSG_SEND_016_AC3_ARetryAfterDelaysTheNextAttemptAtLeastThatLongAndAtMostRetryMax(int retryAfter, int expected)
    {
        await using Engine engine = await StartAsync();
        engine.Sms.Answer = _ => new Attempt.Transient(AttemptReason.RateLimited, 429, TimeSpan.FromSeconds(retryAfter));
        SendReceipt receipt = (await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]))).Value!;

        await engine.Host.DispatchAsync();

        Assert.Equal(EngineHost.Start.AddSeconds(expected), await engine.Host.ColumnAsync<DateTime>(receipt.Deliveries[0].Delivery.Value, "due_at"));
    }

    [Fact]
    public async Task MSG_SEND_007_AC4_WithTheClockMovedPastADeliverysDueInstantTheNextPassTakesItAndNotBefore()
    {
        await using Engine engine = await StartAsync();
        await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]) with { NotBefore = EngineHost.Start.AddMinutes(10) });

        engine.Host.Clock.Advance(TimeSpan.FromMinutes(10) - TimeSpan.FromTicks(10));
        int before = await engine.Host.DispatchAsync();
        engine.Host.Clock.Advance(TimeSpan.FromTicks(10));
        int after = await engine.Host.DispatchAsync();

        Assert.Equal((0, 1), (before, after));
        Assert.Single(engine.Sms.Received);
    }

    [Fact]
    public async Task MSG_SEND_007_AC1_TwoDispatchersAndTwoPassesOfOneProcessCallTheProviderOncePerDelivery()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        ManualClock clock = new(EngineHost.Start);
        await using Engine one = await StartAsync(connection: connection, clock: clock);
        await using Engine two = await StartAsync(connection: connection, clock: clock);
        for (int index = 0; index < 30; index++)
        {
            await one.Host.SendAsync(SendTests.Code(channels: [Channel.Sms], recipient: new Recipient.Person($"person-{index}")));
        }

        int[] took = await Task.WhenAll(one.Host.DispatchAsync(), two.Host.DispatchAsync(), one.Host.DispatchAsync(), two.Host.DispatchAsync());

        Assert.Equal(30, took.Sum());
        List<string> references = [.. one.Sms.Received.Concat(two.Sms.Received).Select(message => message.DeliveryReference)];
        Assert.Equal(30, references.Count);
        Assert.Equal(30, references.Distinct().Count());
        Assert.Equal(0, await one.Host.DispatchAsync());
    }

    [Fact]
    public async Task MSG_SEND_007_AC2_ADeliveryWhoseClaimExpiredWhileAnEarlierOneOfThePassWasSentIsNotSentByTheFirstHolderOnceAnotherTookIt()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        ManualClock clock = new(EngineHost.Start);
        await using Engine first = await StartAsync(connection: connection, clock: clock);
        await using Engine second = await StartAsync(connection: connection, clock: clock);
        SendReceipt a = (await first.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]))).Value!;
        clock.Advance(TimeSpan.FromSeconds(1));
        SendReceipt b = (await first.Host.SendAsync(SendTests.Code(channels: [Channel.Sms], recipient: new Recipient.Person("person-2")))).Value!;
        TaskCompletionSource calling = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        first.Sms.Before = async (_, _) =>
        {
            calling.TrySetResult();
            await release.Task;
        };

        Task<int> held = first.Host.DispatchAsync();
        await calling.Task;
        clock.Advance(TimeSpan.FromMinutes(3));
        int taken = await second.Host.DispatchAsync();
        release.SetResult();
        await held;

        Assert.Equal(2, taken);
        Assert.Single(first.Sms.Received);
        Assert.Single(second.Sms.Received);
        Assert.Equal(DeliveryStatus.Sent, (await first.Host.ReadAsync(b.Send)).Deliveries[0].Status);
        DeliveryRecord earlier = (await first.Host.ReadAsync(a.Send)).Deliveries[0];
        Assert.Equal((DeliveryStatus.Waiting, AttemptReason.ClaimLost), (earlier.Status, earlier.Reason!.Value));
        Assert.Equal(AttemptReason.ClaimLost, Assert.Single(earlier.Attempts).Reason);
    }

    [Fact]
    public async Task MSG_SEND_007_AC6_APassStoppedDuringTheSecondCallOfTenReturnsTheOtherEightAtOnceThenRecordsTheSecondsAnswer()
    {
        await using Engine engine = await StartAsync();
        for (int index = 0; index < 10; index++)
        {
            await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms], recipient: new Recipient.Person($"person-{index}")));
            engine.Host.Clock.Advance(TimeSpan.FromMilliseconds(1));
        }

        TaskCompletionSource calling = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        engine.Sms.Before = async (_, _) =>
        {
            if (Interlocked.Increment(ref calls) == 2)
            {
                calling.SetResult();
                await release.Task;
            }
        };
        using CancellationTokenSource stop = new();

        Task<int> pass = engine.Host.DispatchAsync(stop.Token);
        await calling.Task;
        await stop.CancelAsync();
        await UntilAsync(async () => await CountAsync(engine.Host, status: "waiting", attempts: 0, unclaimed: true) == 8);
        release.SetResult();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pass);
        Assert.Equal(2, await CountAsync(engine.Host, status: "sent"));
        Assert.Equal(8, await CountAsync(engine.Host, status: "waiting", attempts: 0, dueNow: true));
        Assert.Equal(2, engine.Sms.Received.Count);
    }

    [Fact]
    public async Task MSG_SEND_007_AC6_APassStoppedWhileTheProtectorAnswersAtCheck1ReturnsThatDeliveryToo()
    {
        ContentProtectorFake vault = new("vault");
        await using Engine engine = await StartAsync(services: services => services.AddSingleton<IContentProtector>(vault));
        await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]));
        TaskCompletionSource opening = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        vault.BeforeOpen = async () =>
        {
            opening.TrySetResult();
            await release.Task;
        };
        using CancellationTokenSource stop = new();

        Task<int> pass = engine.Host.DispatchAsync(stop.Token);
        await opening.Task;
        await stop.CancelAsync();
        await UntilAsync(async () => await CountAsync(engine.Host, status: "waiting") == 1);
        release.SetResult();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pass);
        Assert.Empty(engine.Sms.Received);
        Assert.Equal(1, await CountAsync(engine.Host, status: "waiting", attempts: 0));
    }

    [Fact]
    public async Task MSG_SEND_007_AC6_ARunPassAsyncWhoseTokenIsAlreadyCancelledThrowsAndClaimsNothing()
    {
        await using Engine engine = await StartAsync();
        await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]));
        using CancellationTokenSource stop = new();
        await stop.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => engine.Host.DispatchAsync(stop.Token));

        Assert.Equal(1, await CountAsync(engine.Host, status: "waiting", unclaimed: true));
    }

    [Fact]
    public async Task MSG_SEND_007_AC7_ADeliveryTakenFromAStoppedHolderWithAnUnansweredAttemptRecordsItClaimlost()
    {
        await using Engine engine = await StartAsync();
        SendReceipt receipt = (await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]))).Value!;
        Guid id = receipt.Deliveries[0].Delivery.Value;
        await LoseAsync(engine.Host, id);

        int took = await engine.Host.DispatchAsync();

        DeliveryRecord delivery = (await engine.Host.ReadAsync(receipt.Send)).Deliveries[0];
        Assert.Equal(1, took);
        Assert.Empty(engine.Sms.Received);
        Assert.Equal((DeliveryStatus.Waiting, AttemptReason.ClaimLost), (delivery.Status, delivery.Reason!.Value));
        AttemptRecord attempt = Assert.Single(delivery.Attempts);
        Assert.Equal((AttemptClass.Transient, AttemptReason.ClaimLost), (attempt.AnswerClass!.Value, attempt.Reason!.Value));
        Assert.Equal(EngineHost.Start + Initial, await engine.Host.ColumnAsync<DateTime>(id, "due_at"));
    }

    [Fact]
    public async Task MSG_SEND_007_AC7_AClaimlostAttemptThatUsesUpTheAttemptsEndsTheDeliveryFailed()
    {
        await using Engine engine = await StartAsync(configure: configuration => configuration["Hermes:Retry:MaxAttempts"] = "1");
        SendReceipt receipt = (await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]))).Value!;
        await LoseAsync(engine.Host, receipt.Deliveries[0].Delivery.Value);

        await engine.Host.DispatchAsync();

        DeliveryRecord delivery = (await engine.Host.ReadAsync(receipt.Send)).Deliveries[0];
        Assert.Equal((DeliveryStatus.Failed, AttemptReason.ClaimLost), (delivery.Status, delivery.Reason!.Value));
    }

    [Fact]
    public async Task Pass_ThatHasRunForTheInterval_HandsBackTheDeliveriesItHasNotReached()
    {
        await using Engine engine = await StartAsync();
        for (int index = 0; index < 4; index++)
        {
            await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms], recipient: new Recipient.Person($"person-{index}")));
        }

        engine.Sms.Before = (_, _) =>
        {
            engine.Host.Clock.Advance(TimeSpan.FromSeconds(5));
            return Task.CompletedTask;
        };

        int took = await engine.Host.DispatchAsync();

        Assert.Equal(4, took);
        Assert.Single(engine.Sms.Received);
        Assert.Equal(3, await CountAsync(engine.Host, status: "waiting", attempts: 0, dueNow: true));
    }

    [Fact]
    public async Task MSG_SEND_007_AC5_ARowOfAChannelOrReasonThisReleaseDoesNotKnowAndARowOfAKindNeitherDeclaredNorRetiredAreNeverClaimed()
    {
        const string Widen = """
            ALTER TABLE messaging.deliveries DROP CONSTRAINT ck_deliveries_channel;
            ALTER TABLE messaging.deliveries DROP CONSTRAINT ck_deliveries_reason;
            UPDATE messaging.deliveries SET channel = 'fax' WHERE id = @channel;
            UPDATE messaging.deliveries SET reason = 'carrierlost' WHERE id = @reason;
            UPDATE messaging.deliveries SET kind = 'sign.out' WHERE id = @kind;
            """;
        await using Engine engine = await StartAsync();
        List<Guid> rows = [];
        for (int index = 0; index < 3; index++)
        {
            rows.Add((await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms], recipient: new Recipient.Person($"person-{index}")))).Value!.Deliveries[0].Delivery.Value);
        }

        await engine.Host.RunAsync(connection =>
        {
            NpgsqlCommand command = new(Widen, connection);
            command.Parameters.AddWithValue("channel", rows[0]);
            command.Parameters.AddWithValue("reason", rows[1]);
            command.Parameters.AddWithValue("kind", rows[2]);
            return command;
        });

        Assert.Equal(0, await engine.Host.DispatchAsync());
        Assert.Equal(3, await CountAsync(engine.Host, status: "waiting", attempts: 0, unclaimed: true));
        Assert.Empty(engine.Sms.Received);
    }

    [Fact]
    public async Task MSG_SEND_006_AC1_ADeliveryOfARetiredKindEndsCancelledAndCallsNoProvider()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        ManualClock clock = new(EngineHost.Start);
        SendReceipt receipt;
        await using (Engine before = await StartAsync(connection: connection, clock: clock))
        {
            receipt = (await before.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]))).Value!;
        }

        await using Engine after = await StartAsync(connection: connection, clock: clock, declare: declare =>
        {
            declare.Category(EngineHost.Category).Essential();
            declare.Kind("sign.up", EngineHost.Category).Sms();
            declare.Retire(EngineHost.Kind);
        });

        Assert.Equal(1, await after.Host.DispatchAsync());

        Assert.Empty(after.Sms.Received);
        Assert.Equal(DeliveryStatus.Cancelled, (await after.Host.ReadAsync(receipt.Send)).Deliveries[0].Status);
        Assert.Equal(0, await RateCountAsync(after.Host));
    }

    [Fact]
    public async Task MSG_SEND_018_AC3_AValueCopiedIntoAnotherDeliverysRowEndsItFailedWithValuerejectedAndRaisesTheAlert()
    {
        AlertSinkFake alerts = new();
        await using Engine engine = await StartAsync(services: services => services.AddSingleton<IAlertSink>(alerts));
        SendReceipt one = (await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]))).Value!;
        SendReceipt two = (await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms], code: "999999"))).Value!;
        await engine.Host.RunAsync(connection =>
        {
            NpgsqlCommand command = new("UPDATE messaging.deliveries SET text = (SELECT text FROM messaging.deliveries WHERE id = @from) WHERE id = @to", connection);
            command.Parameters.AddWithValue("from", one.Deliveries[0].Delivery.Value);
            command.Parameters.AddWithValue("to", two.Deliveries[0].Delivery.Value);
            return command;
        });

        await engine.Host.DispatchAsync();

        DeliveryRecord rejected = (await engine.Host.ReadAsync(two.Send)).Deliveries[0];
        Assert.Equal((DeliveryStatus.Failed, AttemptReason.ValueRejected), (rejected.Status, rejected.Reason!.Value));
        Assert.Equal(DeliveryStatus.Sent, (await engine.Host.ReadAsync(one.Send)).Deliveries[0].Status);
        Assert.Single(engine.Sms.Received);
        Assert.Contains(alerts.Calls, call => call is { Raised: true, Condition: "content-rejected", Scope: "deliveries" });
    }

    [Fact]
    public async Task MSG_SEND_006_AC4_ADeliveryHeldForAKeyIsSentByAProcessThatHoldsItHoweverManyPassesItWasHeld()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        ManualClock clock = new(EngineHost.Start);
        AlertSinkFake alerts = new();
        await using Engine holding = await StartAsync(connection: connection, clock: clock, services: services => services.AddSingleton<IContentProtector>(new ContentProtectorFake("vault")));
        await using Engine lacking = await StartAsync(connection: connection, clock: clock, services: services => services.AddSingleton<IAlertSink>(alerts));
        SendReceipt receipt = (await holding.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]))).Value!;
        Guid id = receipt.Deliveries[0].Delivery.Value;

        for (int pass = 0; pass < 3; pass++)
        {
            Assert.Equal(1, await lacking.Host.DispatchAsync());
            Assert.Equal(clock.GetUtcNow() + Initial, await lacking.Host.ColumnAsync<DateTime>(id, "due_at"));
            clock.Advance(Initial);
        }

        DeliveryRecord waiting = (await holding.Host.ReadAsync(receipt.Send)).Deliveries[0];
        Assert.Equal((DeliveryStatus.Waiting, AttemptReason.KeyUnavailable), (waiting.Status, waiting.Reason!.Value));
        Assert.Empty(waiting.Attempts);
        Assert.Equal(EngineHost.Start, await holding.Host.ColumnAsync<DateTime>(id, "first_held_at"));
        Assert.Equal(EngineHost.Start, await holding.Host.ColumnAsync<DateTime>(id, "became_due_at"));
        Assert.Contains(alerts.Calls, call => call is { Raised: true, Condition: "key-unavailable", Scope: "vault" });
        Assert.Equal(0, await RateCountAsync(lacking.Host));

        Assert.Equal(1, await holding.Host.DispatchAsync());

        Assert.Equal(DeliveryStatus.Sent, (await holding.Host.ReadAsync(receipt.Send)).Deliveries[0].Status);
        Assert.Null(await holding.Host.ColumnAsync<DateTime?>(id, "first_held_at"));
        Assert.Empty(lacking.Sms.Received);
        Assert.Single(holding.Sms.Received);
    }

    [Fact]
    public async Task MSG_SEND_006_AC6_ADeliveryHeldForAKeyForMaxHoldEndsFailedAtItsNextHoldAndANotBeforeBeyondMaxHoldIsHeldAtItsFirst()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        ManualClock clock = new(EngineHost.Start);
        await using Engine holding = await StartAsync(connection: connection, clock: clock, services: services => services.AddSingleton<IContentProtector>(new ContentProtectorFake("vault")));
        await using Engine lacking = await StartAsync(connection: connection, clock: clock, configure: configuration => configuration["Hermes:Dispatch:MaxHold"] = "01:00:00");
        SendReceipt now = (await holding.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]))).Value!;
        SendReceipt later = (await holding.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]) with { NotBefore = EngineHost.Start.AddHours(2) })).Value!;

        await lacking.Host.DispatchAsync();
        clock.Advance(TimeSpan.FromMinutes(59));
        await lacking.Host.DispatchAsync();
        DeliveryStatus beforeMaxHold = (await lacking.Host.ReadAsync(now.Send)).Deliveries[0].Status;
        clock.Advance(TimeSpan.FromMinutes(61));
        await lacking.Host.DispatchAsync();

        Assert.Equal(DeliveryStatus.Waiting, beforeMaxHold);
        DeliveryRecord failed = (await lacking.Host.ReadAsync(now.Send)).Deliveries[0];
        Assert.Equal((DeliveryStatus.Failed, AttemptReason.KeyUnavailable), (failed.Status, failed.Reason!.Value));
        DeliveryRecord held = (await lacking.Host.ReadAsync(later.Send)).Deliveries[0];
        Assert.Equal((DeliveryStatus.Waiting, AttemptReason.KeyUnavailable), (held.Status, held.Reason!.Value));
    }

    [Fact]
    public async Task MSG_SEND_015_AC1_ADeliveryWhoseFirstCheckComesAfterItsExpiryEndsExpiredAndOneWhoseRetryWouldFallAfterItToo()
    {
        await using Engine engine = await StartAsync(declare: declare =>
        {
            declare.Category(EngineHost.Category).Essential();
            declare.Kind(EngineHost.Kind, EngineHost.Category).Sms().ExpiresAfter(TimeSpan.FromMinutes(10));
        });
        SendReceipt late = (await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]))).Value!;
        engine.Host.Clock.Advance(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1));
        SendReceipt retried = (await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms], recipient: new Recipient.Person("person-2")))).Value!;
        engine.Sms.Answer = _ => new Attempt.Transient(AttemptReason.Temporary, 503, TimeSpan.FromMinutes(11));

        Assert.Equal(2, await engine.Host.DispatchAsync());

        Assert.Single(engine.Sms.Received);
        Assert.Equal(DeliveryStatus.Expired, (await engine.Host.ReadAsync(late.Send)).Deliveries[0].Status);
        DeliveryRecord expired = (await engine.Host.ReadAsync(retried.Send)).Deliveries[0];
        Assert.Equal(DeliveryStatus.Expired, expired.Status);
        Assert.Single(expired.Attempts);
    }

    [Fact]
    public async Task MSG_SMS_002_ANumberNoLongerWithinTheAllowedPrefixesWhenItIsCheckedEndsFailedWithDestinationnotallowed()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        ManualClock clock = new(EngineHost.Start);
        SendReceipt receipt;
        await using (Engine before = await StartAsync(connection: connection, clock: clock))
        {
            receipt = (await before.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]))).Value!;
        }

        await using Engine after = await StartAsync(connection: connection, clock: clock, configure: configuration => configuration["Hermes:Sms:AllowedPrefixes:0"] = "+30");

        await after.Host.DispatchAsync();

        DeliveryRecord delivery = (await after.Host.ReadAsync(receipt.Send)).Deliveries[0];
        Assert.Equal((DeliveryStatus.Failed, AttemptReason.DestinationNotAllowed), (delivery.Status, delivery.Reason!.Value));
        AttemptRecord attempt = Assert.Single(delivery.Attempts);
        Assert.Empty(attempt.Calls);
        Assert.Empty(after.Sms.Received);
    }

    [Fact]
    public async Task MSG_SEND_007_AC10_APassClaimsNoMoreThanTheRateLeavesInItsSecondAndOpensNoOtherValues()
    {
        ContentProtectorFake vault = new("vault");
        await using Engine engine = await StartAsync(
            configure: configuration => configuration["Hermes:Sms:Rate"] = "5",
            services: services => services.AddSingleton<IContentProtector>(vault));
        for (int index = 0; index < 8; index++)
        {
            await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms], recipient: new Recipient.Person($"person-{index}")));
        }

        int first = await engine.Host.DispatchAsync();
        int same = await engine.Host.DispatchAsync();
        int opened = vault.Opens;
        engine.Host.Clock.Advance(TimeSpan.FromSeconds(1));
        int next = await engine.Host.DispatchAsync();

        Assert.Equal((5, 0, 3), (first, same, next));
        Assert.Equal(10, opened);
        Assert.Equal(8, engine.Sms.Received.Count);
    }

    [Fact]
    public async Task MSG_SEND_007_AC11_AnEssentialDeliveryRecordedAfterOptionalOnesIsClaimedBeforeThem()
    {
        await using Engine engine = await StartAsync(
            configure: configuration => configuration["Hermes:Dispatch:BatchSize"] = "1",
            declare: declare =>
            {
                EngineHost.Essential(declare);
                declare.Category("news");
                declare.Kind("news.weekly", "news").Sms();
            },
            templates: key => key.Kind == "news.weekly" && key is { Channel: Channel.Sms } ? "News {{ code }}" : EngineHost.Templates(key));
        for (int index = 0; index < 3; index++)
        {
            await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]) with { Kind = "news.weekly" });
        }

        engine.Host.Clock.Advance(TimeSpan.FromSeconds(1));
        await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]));

        await engine.Host.DispatchAsync();

        Assert.Equal("Sms en 123456", Assert.Single(engine.Sms.Received).Text);
    }

    [Fact]
    public async Task MSG_TPL_005_AC2_ASubjectValueCarryingALineFeedAndBccArrivesAsOneSubjectLine()
    {
        await using Engine engine = await StartAsync(templates: key => key is { Channel: Channel.Email, Part: "subject" } ? "Code {{ code }}" : EngineHost.Templates(key));
        await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Email], code: "1\nBcc: x@example.com\r\u007F"));

        await engine.Host.DispatchAsync();

        Assert.Equal("Code 1 Bcc: x@example.com  ", Assert.Single(engine.Email.Received).Subject);
    }

    [Fact]
    public async Task Pass_ADisplayNameCarryingControlCharacters_ArrivesWithSpacesInTheirPlace()
    {
        EmailAddress from = new() { Address = "orders@example.net", DisplayName = "Orders\r\nBcc: x@example.com" };
        EmailAddress replyTo = new() { Address = "help@example.net", DisplayName = "Help\tdesk" };
        await using Engine engine = await StartAsync(declare: declare =>
        {
            declare.Category(EngineHost.Category).Essential();
            declare.Kind(EngineHost.Kind, EngineHost.Category).Email(from, replyTo);
        });
        await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Email]));

        await engine.Host.DispatchAsync();

        EmailMessage message = Assert.Single(engine.Email.Received);
        Assert.Equal(("Orders  Bcc: x@example.com", "orders@example.net"), (message.From.DisplayName, message.From.Address));
        Assert.Equal(("Help desk", "help@example.net"), (message.ReplyTo!.DisplayName, message.ReplyTo.Address));
    }

    [Fact]
    public async Task MSG_EMAIL_002_AC1_AnEmailOfAKindWithItsOwnSenderCarriesItAndItsParts()
    {
        EmailAddress from = new() { Address = "orders@example.net", DisplayName = "Orders" };
        EmailAddress replyTo = new() { Address = "help@example.net" };
        await using Engine engine = await StartAsync(
            declare: declare =>
            {
                declare.Category(EngineHost.Category).Essential();
                declare.Kind(EngineHost.Kind, EngineHost.Category).Email(from, replyTo);
            },
            templates: key => key is { Channel: Channel.Email, Part: "html" } ? "<p>{{ code }}</p>" : EngineHost.Templates(key));
        SendReceipt receipt = (await engine.Host.SendAsync(SendTests.Code(language: "ar"))).Value!;

        await engine.Host.DispatchAsync();

        EmailMessage message = Assert.Single(engine.Email.Received);
        Assert.Equal(from, message.From);
        Assert.Equal(replyTo, message.ReplyTo);
        Assert.Equal("someone@example.com", message.ToAddress);
        Assert.Equal(("Subject ar 123456", "Text ar 123456", "<p>123456</p>", "ar"), (message.Subject, message.Text, message.Html, message.Language));
        Assert.Equal($"{receipt.Deliveries[0].Delivery.Value:D}@example.net", message.MessageId);
        Assert.Null(message.Unsubscribe);
        Assert.Equal(1, engine.Email.Opened);
    }

    [Fact]
    public async Task Pass_AnEmailOfAKindWithoutItsOwnSender_GoesFromTheDeployments()
    {
        await using Engine engine = await StartAsync();
        await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Email]));

        await engine.Host.DispatchAsync();

        EmailMessage message = Assert.Single(engine.Email.Received);
        Assert.Equal(TestHost.FromAddress, message.From.Address);
        Assert.Null(message.Html);
    }

    [Fact]
    public async Task Pass_AnSms_CarriesItsNumberSenderTextReferenceAndMeasure()
    {
        await using Engine engine = await StartAsync();
        SendReceipt receipt = (await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms], language: "ar"))).Value!;

        await engine.Host.DispatchAsync();

        SmsMessage message = Assert.Single(engine.Sms.Received);
        Assert.Equal(("+201000000001", TestHost.Sender, "Sms ar 123456"), (message.ToNumber, message.Sender, message.Text));
        Assert.Matches("^[A-Za-z0-9_-]{22}$", message.DeliveryReference);
        Assert.Equal((SmsEncoding.Gsm7, 1), (message.Encoding, message.Segments));
        Assert.Null(message.ReportAddress);
        Assert.Equal(message.DeliveryReference, await AttemptReferenceAsync(engine.Host, receipt.Deliveries[0].Delivery.Value));
    }

    [Theory]
    [InlineData("throws")]
    [InlineData("hangs")]
    public async Task MSG_CHAN_001_AC2_AProviderThatThrowsOrDoesNotAnswerLeavesItsDeliveryWaitingAndLogsOnlyTheExceptionsType(string fault)
    {
        using LogCapture logs = new();
        await using Engine engine = await StartAsync(logs: logs);
        engine.Sms.Before = fault == "throws"
            ? (_, _) => throw new InvalidOperationException("+201000000001 was refused")
            : async (_, token) =>
            {
                engine.Host.Clock.Advance(TimeSpan.FromSeconds(20));
                await Task.Delay(Timeout.Infinite, token);
            };
        SendReceipt receipt = (await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]))).Value!;

        await engine.Host.DispatchAsync();

        DeliveryRecord delivery = (await engine.Host.ReadAsync(receipt.Send)).Deliveries[0];
        Assert.Equal(DeliveryStatus.Waiting, delivery.Status);
        Assert.Equal(fault == "throws" ? AttemptReason.ProviderFault : AttemptReason.Timeout, delivery.Reason);
        Assert.DoesNotContain(logs.Entries, entry => entry.Message.Contains("+20", StringComparison.Ordinal));
        if (fault == "throws")
        {
            Assert.Contains(logs.Entries, entry => entry.Message.Contains(nameof(InvalidOperationException), StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task MSG_OPS_005_AC1_TheLogsOfASendsLifeHoldNoDestinationContentOrPersonReference()
    {
        using LogCapture logs = new(everyCategory: true);
        await using Engine engine = await StartAsync(logs: logs);
        int calls = 0;
        engine.Sms.Answer = _ => ++calls == 1 ? new Attempt.Transient(AttemptReason.Temporary, 503, null) : new Attempt.Accepted("ref-1");
        engine.Email.Answer = _ => new Attempt.Permanent(AttemptReason.RecipientRefused, 550);
        await engine.Host.SendAsync(SendTests.Code(code: "778899"));

        await engine.Host.DispatchAsync();
        engine.Host.Clock.Advance(TimeSpan.FromMinutes(1));
        await engine.Host.DispatchAsync();

        Assert.NotEmpty(logs.Entries);
        Assert.All(logs.Entries, entry =>
        {
            Assert.DoesNotContain("someone@example.com", entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("+201000000001", entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("778899", entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("person-1", entry.Message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task MSG_OPS_004_AC1_WithDispatchingOffRunPassAsyncThrowsAndTheApplicationStillRecordsSends()
    {
        await using Engine engine = await StartAsync(configure: configuration => configuration["Hermes:Dispatch:Enabled"] = "false");

        Assert.True((await engine.Host.SendAsync(SendTests.Code())).IsSuccess);
        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.Host.DispatchAsync());
        Assert.Equal(3, await CountAsync(engine.Host, status: "waiting"));
    }

    [Fact]
    public async Task Pass_AFinalStatusWithTheObserverOn_WritesItsNoticeUnderTheNextSequence()
    {
        await using Engine engine = await StartAsync(
            configure: configuration => configuration["Hermes:Observer:Enabled"] = "true",
            services: services => services.AddSingleton<IDeliveryObserver>(new DeliveryObserverFake()));
        engine.Sms.Answer = _ => new Attempt.Transient(AttemptReason.Temporary, null, null);
        SendReceipt receipt = (await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms, Channel.Email]))).Value!;

        await engine.Host.DispatchAsync();

        Guid email = receipt.Deliveries.Single(delivery => delivery.Channel == Channel.Email).Delivery.Value;
        Guid sms = receipt.Deliveries.Single(delivery => delivery.Channel == Channel.Sms).Delivery.Value;
        Assert.Equal(1, await CountAsync(engine.Host, id: email, sequence: 2));
        Assert.Equal(1, await NoticeCountAsync(engine.Host, email, "sent"));
        Assert.Equal(0, await NoticeCountAsync(engine.Host, sms, null));
    }

    private Task<Engine> StartAsync(
        Action<Dictionary<string, string?>>? configure = null,
        Action<DeclarationBuilder>? declare = null,
        Func<TemplateKey, string?>? templates = null,
        Action<IServiceCollection>? services = null,
        string? connection = null,
        ManualClock? clock = null,
        LogCapture? logs = null)
    {
        return Engine.StartAsync(postgres, configure, declare, templates, services, connection, clock, logs);
    }

    private static async Task UntilAsync(Func<Task<bool>> condition)
    {
        for (int tries = 0; tries < 200; tries++)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(25, TestContext.Current.CancellationToken);
        }

        Assert.Fail("The condition did not come to hold.");
    }

    private static async Task<long> CountAsync(EngineHost host, string? status = null, int? attempts = null, bool unclaimed = false, bool dueNow = false, Guid? id = null, int? sequence = null)
    {
        const string Count = """
            SELECT count(*) FROM messaging.deliveries
            WHERE (@status::text IS NULL OR status = @status::text) AND (@attempts::int IS NULL OR attempts = @attempts::int)
              AND (NOT @unclaimed OR claimed_by IS NULL) AND (NOT @dueNow OR due_at = @now)
              AND (@id::uuid IS NULL OR id = @id::uuid) AND (@sequence::int IS NULL OR next_sequence = @sequence::int)
            """;
        await using NpgsqlConnection connection = new(host.Connection);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new(Count, connection);
        command.Parameters.AddWithValue("status", (object?)status ?? DBNull.Value);
        command.Parameters.AddWithValue("attempts", attempts is { } count ? count : DBNull.Value);
        command.Parameters.AddWithValue("unclaimed", unclaimed);
        command.Parameters.AddWithValue("dueNow", dueNow);
        command.Parameters.AddWithValue("now", host.Clock.GetUtcNow());
        command.Parameters.AddWithValue("id", id is { } row ? row : DBNull.Value);
        command.Parameters.AddWithValue("sequence", sequence is { } next ? next : DBNull.Value);
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private static async Task<long> NoticeCountAsync(EngineHost host, Guid delivery, string? status)
    {
        await using NpgsqlConnection connection = new(host.Connection);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("SELECT count(*) FROM messaging.notices WHERE delivery_id = @id AND sequence = 1 AND (@status::text IS NULL OR status = @status::text)", connection);
        command.Parameters.AddWithValue("id", delivery);
        command.Parameters.AddWithValue("status", (object?)status ?? DBNull.Value);
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private static async Task<long> RateCountAsync(EngineHost host)
    {
        await using NpgsqlConnection connection = new(host.Connection);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("SELECT coalesce(sum(count), 0)::bigint FROM messaging.rate_counters", connection);
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private static async Task<string?> AttemptReferenceAsync(EngineHost host, Guid delivery)
    {
        await using NpgsqlConnection connection = new(host.Connection);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("SELECT reference FROM messaging.attempts WHERE delivery_id = @id", connection);
        command.Parameters.AddWithValue("id", delivery);
        return (string?)await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Makes the delivery as a holder that stopped after counting its attempt leaves it: inflight, its claim expired, its attempt unanswered.</summary>
    private static async Task LoseAsync(EngineHost host, Guid delivery)
    {
        const string Lose = """
            UPDATE messaging.deliveries SET status = 'inflight', attempts = 1, claimed_by = @holder, claimed_at = @at, claim_expires_at = @at WHERE id = @id;
            INSERT INTO messaging.attempts (delivery_id, number, reference, at) VALUES (@id, 1, 'lost-reference', @at);
            """;
        await host.RunAsync(connection =>
        {
            NpgsqlCommand command = new(Lose, connection);
            command.Parameters.AddWithValue("holder", Guid.CreateVersion7());
            command.Parameters.AddWithValue("at", host.Clock.GetUtcNow());
            command.Parameters.AddWithValue("id", delivery);
            return command;
        });
    }

    /// <summary>A started host that dispatches when a test runs a pass, through recording providers.</summary>
    private sealed class Engine : IAsyncDisposable
    {
        private EngineHost? host;

        internal EngineHost Host => host!;

        internal RecordingEmailProvider Email { get; } = new();

        internal RecordingSmsProvider Sms { get; } = new();

        internal static async Task<Engine> StartAsync(
            PostgresFixture postgres,
            Action<Dictionary<string, string?>>? configure,
            Action<DeclarationBuilder>? declare,
            Func<TemplateKey, string?>? templates,
            Action<IServiceCollection>? services,
            string? connection,
            ManualClock? clock,
            LogCapture? logs)
        {
            Engine engine = new();
            RecordingEmailProvider email = engine.Email;
            RecordingSmsProvider sms = engine.Sms;
            engine.host = await EngineHost.StartAsync(
                connection ?? await EngineHost.DatabaseAsync(postgres),
                declare,
                templates,
                configuration =>
                {
                    EngineHost.Dispatching(configuration);
                    configure?.Invoke(configuration);
                },
                collection =>
                {
                    collection.AddSingleton<IEmailProvider>(email);
                    collection.AddSingleton<ISmsProvider>(sms);
                    services?.Invoke(collection);
                },
                clock: clock,
                logs: logs);
            return engine;
        }

        public ValueTask DisposeAsync()
        {
            return host?.DisposeAsync() ?? ValueTask.CompletedTask;
        }
    }
}