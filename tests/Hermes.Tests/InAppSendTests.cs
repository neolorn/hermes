using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Hermes.Core;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "integration")]
public sealed class InAppSendTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task MSG_INAPP_001_AC2_ASendNamingInappForAnAnonymousRecipientIsRefusedAndRecordsNothing()
    {
        await using EngineHost host = await InApp.StartAsync(postgres);

        Result<SendReceipt> result = await host.SendAsync(InApp.Send() with { Recipient = new Recipient.Anonymous(), Channels = [Channel.Sms, Channel.InApp] });

        Assert.Equal("messaging.send.nodestination", result.Failure!.Code);
        Assert.Equal("inapp", result.Failure.Details["channel"]);
        Assert.Equal((0, 0, 0), await host.RecordedAsync());
    }

    [Fact]
    public async Task MSG_SEND_003_AC3_APersonsSendWithNoChannelNamedIncludesTheInAppChannelAndAnAnonymousOneLeavesItOut()
    {
        await using EngineHost host = await InApp.StartAsync(postgres);

        Result<SendReceipt> person = await host.SendAsync(InApp.Send() with { Channels = null });
        Result<SendReceipt> anonymous = await host.SendAsync(InApp.Send() with { Recipient = new Recipient.Anonymous(), Channels = null });

        Assert.Equal([Channel.Sms, Channel.InApp], person.Value!.Deliveries.Select(delivery => delivery.Channel));
        Assert.Equal([Channel.Sms], anonymous.Value!.Deliveries.Select(delivery => delivery.Channel));
    }

    [Fact]
    public async Task MSG_INAPP_002_AC1_AnInAppDeliveryHoldsItsPartsInEveryDeclaredLanguageEachInItsLanguageAndItsTargetValues()
    {
        await using EngineHost host = await InApp.StartAsync(postgres);

        Result<SendReceipt> result = await host.SendAsync(InApp.Send(language: "ar"));
        Guid delivery = result.Value!.Deliveries[0].Delivery.Value;
        Dictionary<string, string> opened = await InApp.OpenAsync(host, delivery);

        Assert.Equal(["body.ar", "body.en", "target", "title.ar", "title.en"], opened.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("Order A-1001 en en", opened["title.en"]);
        Assert.Equal("Order A-1001 ar ar", opened["title.ar"]);
        Assert.Equal("Body ar rtl", opened["body.ar"]);
        Assert.Equal("Body en ltr", opened["body.en"]);
        Assert.Equal("""{"orderId":{"text":"A-1001"}}""", opened["target"]);
        Assert.Equal("ar", await host.ColumnAsync<string>(delivery, "language"));
        Assert.Null(await host.ColumnAsync<byte[]>(delivery, "title"));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ar")]
    public async Task MSG_INAPP_002_AC2_ASendWhoseEnglishTitleRendersPastItsLengthIsRefusedInWhateverLanguageItIsSent(string language)
    {
        await using EngineHost host = await StartLongAsync();

        Result<SendReceipt> refused = await host.SendAsync(InApp.Send(language: language, order: new string('x', 129)));
        Result<SendReceipt> taken = await host.SendAsync(InApp.Send(language: language, order: new string('x', 128)));

        Assert.Equal("messaging.send.toolong", refused.Failure!.Code);
        Assert.Equal("inapp", refused.Failure.Details["channel"]);
        Assert.True(taken.IsSuccess);
        Assert.Equal((1, 1, 0), await host.RecordedAsync());
    }

    [Fact]
    public async Task MSG_INAPP_002_AC2_ABodyPastItsLengthIsRefused()
    {
        await using EngineHost host = await EngineHost.StartAsync(
            await EngineHost.DatabaseAsync(postgres),
            InApp.Declare,
            key => key switch
            {
                { Channel: Channel.InApp, Part: "body" } => "{{ orderId }}{{ orderId }}",
                { Channel: Channel.InApp, Part: "title" } => "Order",
                _ => InApp.Templates(key),
            });

        Result<SendReceipt> refused = await host.SendAsync(InApp.Send(order: new string('x', 513)));
        Result<SendReceipt> taken = await host.SendAsync(InApp.Send(order: new string('x', 512)));

        Assert.Equal("messaging.send.toolong", refused.Failure!.Code);
        Assert.True(taken.IsSuccess);
    }

    [Fact]
    public async Task MSG_INAPP_001_AC1_ATargetValueTheSendDoesNotPassIsAFaultOfTheCallingCode()
    {
        await using EngineHost host = await EngineHost.StartAsync(
            await EngineHost.DatabaseAsync(postgres),
            InApp.Declare,
            key => key is { Channel: Channel.InApp, Part: "title" } ? "Order" : InApp.Templates(key));

        await Assert.ThrowsAsync<ArgumentException>(() => host.SendAsync(InApp.Send() with { Values = new Dictionary<string, SendValue> { ["other"] = new SendValue.PlainText("A-1001") } }));
        await Assert.ThrowsAsync<ArgumentException>(() => host.SendAsync(InApp.Send() with
        {
            Values = new Dictionary<string, SendValue> { ["orderId"] = new SendValue.List([new Dictionary<string, SendValue> { ["line"] = new SendValue.WholeNumber(1) }]) },
        }));
        Assert.Equal((0, 0, 0), await host.RecordedAsync());
    }

    [Fact]
    public async Task MSG_INAPP_001_AC1_AnSmsSendOfAnInAppKindNeedsNoTargetValue()
    {
        await using EngineHost host = await EngineHost.StartAsync(
            await EngineHost.DatabaseAsync(postgres),
            InApp.Declare,
            key => key is { Channel: Channel.Sms } ? "Sms" : InApp.Templates(key));

        Result<SendReceipt> result = await host.SendAsync(InApp.Send(channels: [Channel.Sms]) with { Values = new Dictionary<string, SendValue>() });

        Assert.True(result.IsSuccess);
    }

    private async Task<EngineHost> StartLongAsync()
    {
        return await EngineHost.StartAsync(
            await EngineHost.DatabaseAsync(postgres),
            InApp.Declare,
            key => key switch
            {
                { Channel: Channel.InApp, Part: "title", Language: "en" } => "{{ orderId }}",
                { Channel: Channel.InApp, Part: "title" } => "Short",
                _ => InApp.Templates(key),
            });
    }
}