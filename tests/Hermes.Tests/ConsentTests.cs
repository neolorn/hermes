using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "integration")]
public sealed class ConsentTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    internal const string Marketing = "marketing";

    internal const string Purpose = "promotions";

    internal const string Promotion = "marketing.promotion";

    [Fact]
    public async Task MSG_SEND_011_AC1_ADeliveryWhoseRecipientsConsentTheSourceAnswersNotGivenEndsNoconsent()
    {
        ConsentSourceFake consent = new() { Answer = (_, _) => Task.FromResult(ConsentAnswer.NotGiven) };
        await using DispatchEngine engine = await StartAsync(consent);
        SendReceipt receipt = (await engine.Host.SendAsync(PromotionTo("+201000000001"))).Value!;

        await engine.Host.DispatchAsync();

        DeliveryRecord delivery = Assert.Single((await engine.Host.ReadAsync(receipt.Send)).Deliveries);
        Assert.Equal(DeliveryStatus.NoConsent, delivery.Status);
        Assert.Empty(delivery.Attempts);
        Assert.Empty(engine.Sms.Received);
        ConsentQuery query = Assert.Single(consent.Queries);
        Assert.Equal(new Recipient.Person("person-1"), query.Recipient);
        Assert.Equal(("+201000000001", Purpose, Channel.Sms), (query.Destination, query.Purpose, query.Channel));
    }

    [Fact]
    public async Task MSG_SEND_011_AC2_AConsentWithdrawnAfterTheSendAndBeforeTheDispatchStopsTheDelivery()
    {
        bool given = true;
        ConsentSourceFake consent = new() { Answer = (_, _) => Task.FromResult(given ? ConsentAnswer.Given : ConsentAnswer.NotGiven) };
        await using DispatchEngine engine = await StartAsync(consent);
        SendReceipt receipt = (await engine.Host.SendAsync(PromotionTo("+201000000001"))).Value!;

        given = false;
        await engine.Host.DispatchAsync();

        Assert.Equal(DeliveryStatus.NoConsent, await SuppressionTests.StatusAsync(engine.Host, receipt));
        Assert.Empty(engine.Sms.Received);
    }

    [Fact]
    public async Task Consent_GivenByTheSource_SendsTheDeliveryAndAnAnonymousRecipientIsAskedAsOne()
    {
        ConsentSourceFake consent = new();
        await using DispatchEngine engine = await StartAsync(consent);
        SendReceipt receipt = (await engine.Host.SendAsync(PromotionTo("+201000000001") with { Recipient = new Recipient.Anonymous() })).Value!;

        await engine.Host.DispatchAsync();

        Assert.Equal(DeliveryStatus.Sent, await SuppressionTests.StatusAsync(engine.Host, receipt));
        Assert.IsType<Recipient.Anonymous>(Assert.Single(consent.Queries).Recipient);
    }

    [Theory]
    [InlineData("unavailable")]
    [InlineData("throws")]
    [InlineData("hangs")]
    public async Task Consent_ASourceThatCannotAnswer_IsATransientAnswerCountedAsAnAttemptAndTheLastEndsFailedConsentunavailable(string answer)
    {
        ManualClock clock = new(EngineHost.Start);
        ConsentSourceFake consent = new()
        {
            Answer = async (_, cancellationToken) => answer switch
            {
                "unavailable" => ConsentAnswer.Unavailable,
                "throws" => throw new InvalidOperationException("The consent store is down."),
                _ => await HangAsync(clock, cancellationToken),
            },
        };
        await using DispatchEngine engine = await StartAsync(consent, clock, configuration =>
        {
            configuration["Hermes:Retry:MaxAttempts"] = "2";
            configuration["Hermes:Dispatch:Timeout"] = "00:00:01";
        });
        SendReceipt receipt = (await engine.Host.SendAsync(PromotionTo("+201000000001"))).Value!;

        await engine.Host.DispatchAsync();
        DeliveryRecord waiting = Assert.Single((await engine.Host.ReadAsync(receipt.Send)).Deliveries);
        engine.Host.Clock.Advance(TimeSpan.FromMinutes(5));
        await engine.Host.DispatchAsync();
        DeliveryRecord failed = Assert.Single((await engine.Host.ReadAsync(receipt.Send)).Deliveries);

        Assert.Equal((DeliveryStatus.Waiting, AttemptReason.ConsentUnavailable), (waiting.Status, waiting.Reason!.Value));
        AttemptRecord attempt = Assert.Single(waiting.Attempts);
        Assert.Equal((AttemptClass.Transient, AttemptReason.ConsentUnavailable), (attempt.AnswerClass!.Value, attempt.Reason!.Value));
        Assert.Empty(attempt.Calls);
        Assert.Equal((DeliveryStatus.Failed, AttemptReason.ConsentUnavailable), (failed.Status, failed.Reason!.Value));
        Assert.Equal(2, failed.Attempts.Count);
        Assert.Empty(engine.Sms.Received);
    }

    [Fact]
    public async Task Consent_AnEssentialCategorysDelivery_NeverAsksTheSource()
    {
        ConsentSourceFake consent = new() { Answer = (_, _) => Task.FromResult(ConsentAnswer.NotGiven) };
        await using DispatchEngine engine = await StartAsync(consent);
        SendReceipt receipt = (await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]))).Value!;

        await engine.Host.DispatchAsync();

        Assert.Equal(DeliveryStatus.Sent, await SuppressionTests.StatusAsync(engine.Host, receipt));
        Assert.Empty(consent.Queries);
    }

    /// <summary>An essential category of every channel, and the category <c>marketing</c> requiring consent to <c>promotions</c>, its quiet hours off unless a test turns them on.</summary>
    internal static void Declare(DeclarationBuilder declare, bool quietHours = false)
    {
        ArgumentNullException.ThrowIfNull(declare);
        EngineHost.Essential(declare);
        declare.Category(Marketing).RequiresConsent(Purpose).QuietHours(quietHours);
        declare.Kind(Promotion, Marketing).Sms();
    }

    internal static Send PromotionTo(string number)
    {
        return SendTests.Code(phone: number, channels: [Channel.Sms]) with { Kind = Promotion };
    }

    private Task<DispatchEngine> StartAsync(ConsentSourceFake consent, ManualClock? clock = null, Action<System.Collections.Generic.Dictionary<string, string?>>? configure = null)
    {
        return DispatchEngine.StartAsync(postgres, configure: configure, declare: declare => Declare(declare), services: services => services.AddSingleton<IConsentSource>(consent), clock: clock);
    }

    /// <summary>Answers nothing: the call's time passes its timeout on the test's clock, then it waits until cancelled.</summary>
    private static async Task<ConsentAnswer> HangAsync(ManualClock clock, CancellationToken cancellationToken)
    {
        clock.Advance(TimeSpan.FromSeconds(2));
        await Task.Delay(Timeout.Infinite, cancellationToken);
        return ConsentAnswer.Given;
    }
}