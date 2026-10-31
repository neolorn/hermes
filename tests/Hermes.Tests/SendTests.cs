using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Hermes.Content;
using Hermes.Core;
using Hermes.Sending;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "integration")]
public sealed class SendTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    internal const string Email = "someone@example.com";

    internal const string Phone = "+201000000001";

    internal const string Person = "person-1";

    [Fact]
    public async Task MSG_SEND_003_AC3_ASendToAPersonWithNoChannelNamedSendsOnEveryDeclaredChannelItHasADestinationFor()
    {
        await using EngineHost host = await StartAsync();

        Result<SendReceipt> result = await host.SendAsync(Code());

        Assert.True(result.IsSuccess);
        Assert.Equal([Channel.Email, Channel.Sms, Channel.Push], result.Value!.Deliveries.Select(delivery => delivery.Channel));
        Assert.Equal((1, 3, 0), await host.RecordedAsync());
    }

    [Fact]
    public async Task MSG_SEND_003_AC3_ASendWithNoChannelNamedLeavesOutAChannelItHasNoDestinationFor()
    {
        await using EngineHost host = await StartAsync();

        Result<SendReceipt> result = await host.SendAsync(Code(phone: null, recipient: new Recipient.Anonymous()));

        Assert.Equal([Channel.Email], result.Value!.Deliveries.Select(delivery => delivery.Channel));
    }

    [Fact]
    public async Task MSG_SEND_005_AC1_ASendOnThreeChannelsYieldsThreeDeliveriesWithTheirOwnStatuses()
    {
        await using EngineHost host = await StartAsync();

        Result<SendReceipt> result = await host.SendAsync(Code());
        Result<SendRecord> record = await host.WithMessagesAsync(messages => messages.ReadAsync(result.Value!.Send, TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(3, record.Value!.Deliveries.Select(delivery => delivery.Delivery).Distinct().Count());
        Assert.All(record.Value.Deliveries, delivery => Assert.Equal(DeliveryStatus.Waiting, delivery.Status));
        Assert.Equal(result.Value!.Deliveries.Select(delivery => delivery.Delivery.Value).Order(), record.Value.Deliveries.Select(delivery => delivery.Delivery.Value).Order());
    }

    [Theory]
    [InlineData("sms", null, Phone)]
    [InlineData("email", Email, null)]
    public async Task MSG_SEND_003_AC2_AChannelNamedWithNoDestinationIsRefusedNamingItAndRecordsNothing(string channel, string? email, string? phone)
    {
        await using EngineHost host = await StartAsync();
        Channel named = channel == "sms" ? Channel.Sms : Channel.Email;

        Result<SendReceipt> result = await host.SendAsync(Code(email: channel == "sms" ? email : null, phone: channel == "email" ? phone : null, channels: [named]));

        AssertRefused(result, "messaging.send.nodestination", channel);
        Assert.Equal((0, 0, 0), await host.RecordedAsync());
    }

    [Fact]
    public async Task MSG_SEND_003_AC2_PushNamedForAnAnonymousRecipientIsRefusedNamingIt()
    {
        await using EngineHost host = await StartAsync();

        Result<SendReceipt> result = await host.SendAsync(Code(recipient: new Recipient.Anonymous(), channels: [Channel.Email, Channel.Push]));

        AssertRefused(result, "messaging.send.nodestination", "push");
        Assert.Equal((0, 0, 0), await host.RecordedAsync());
    }

    [Fact]
    public async Task MSG_SEND_003_AC2_ASendLeftWithNoChannelIsRefusedNamingNone()
    {
        await using EngineHost host = await StartAsync();

        Result<SendReceipt> none = await host.SendAsync(Code(email: null, phone: null, recipient: new Recipient.Anonymous()));
        Result<SendReceipt> empty = await host.SendAsync(Code(channels: []));

        Assert.Equal("messaging.send.nodestination", none.Failure!.Code);
        Assert.Empty(none.Failure.Details);
        Assert.Equal("messaging.send.nodestination", empty.Failure!.Code);
        Assert.Empty(empty.Failure.Details);
        Assert.Equal((0, 0, 0), await host.RecordedAsync());
    }

    [Theory]
    [InlineData("email", "not an address", Phone)]
    [InlineData("email", "someone@", Phone)]
    [InlineData("email", "سمير@example.com", Phone)]
    [InlineData("sms", Email, "0201000000001")]
    [InlineData("sms", Email, "+0201000000001")]
    [InlineData("sms", Email, "+2010000000012345")]
    [InlineData("sms", Email, "+20 100 000 0001")]
    public async Task MSG_SEND_003_AC2_ADestinationThatDoesNotReadIsRefusedNamingItsChannel(string channel, string email, string phone)
    {
        await using EngineHost host = await StartAsync();

        Result<SendReceipt> result = await host.SendAsync(Code(email: email, phone: phone));

        AssertRefused(result, "messaging.send.destinationinvalid", channel);
        Assert.Equal((0, 0, 0), await host.RecordedAsync());
    }

    [Fact]
    public async Task MSG_SEND_003_AC2_ANumberOutsideTheAllowedPrefixesIsRefused()
    {
        await using EngineHost host = await StartAsync();

        Result<SendReceipt> result = await host.SendAsync(Code(phone: "+301000000001"));

        AssertRefused(result, "messaging.send.destinationnotallowed", "sms");
        Assert.Equal((0, 0, 0), await host.RecordedAsync());
    }

    [Fact]
    public async Task Send_AnAddressWhoseDomainIsInUnicode_IsSentInItsAsciiForm()
    {
        await using EngineHost host = await StartAsync();

        Result<SendReceipt> result = await host.SendAsync(Code(email: "someone@bücher.example", channels: [Channel.Email]));

        Assert.Equal("someone@xn--bcher-kva.example", await host.OpenAsync(result.Value!.Deliveries[0].Delivery.Value, "destination"));
    }

    [Fact]
    public async Task MSG_SMS_001_AC1_ASeventyOneCharacterArabicTextIsRefusedUnderABudgetOfOne()
    {
        string seventy = new('ع', 70);
        await using EngineHost host = await StartAsync(templates: key => key is { Channel: Channel.Sms } ? "{{ code }}" : EngineHost.Templates(key));

        Result<SendReceipt> fits = await host.SendAsync(Code(code: seventy, channels: [Channel.Sms]));
        Result<SendReceipt> over = await host.SendAsync(Code(code: seventy + "ع", channels: [Channel.Sms]));

        Assert.True(fits.IsSuccess);
        AssertRefused(over, "messaging.send.toolong", "sms");
        Assert.Equal((1, 1, 0), await host.RecordedAsync());
    }

    [Fact]
    public async Task Send_AnSmsWithinTheKindsSegments_IsRecorded()
    {
        await using EngineHost host = await StartAsync(
            declare: declare =>
            {
                declare.Category(EngineHost.Category).Essential();
                declare.Kind(EngineHost.Kind, EngineHost.Category).Sms(maxSegments: 2);
            },
            templates: key => key is { Channel: Channel.Sms } ? "{{ code }}" : EngineHost.Templates(key));

        Result<SendReceipt> two = await host.SendAsync(Code(code: new string('ع', 134), channels: [Channel.Sms]));
        Result<SendReceipt> three = await host.SendAsync(Code(code: new string('ع', 135), channels: [Channel.Sms]));

        Assert.True(two.IsSuccess);
        AssertRefused(three, "messaging.send.toolong", "sms");
    }

    [Fact]
    public async Task Send_AnEmailOverMaxBytes_IsRefusedWithToolong()
    {
        await using EngineHost host = await StartAsync(configure: configuration => configuration["Hermes:Email:MaxBytes"] = "10240");

        Result<SendReceipt> fits = await host.SendAsync(Code(code: new string('a', 5000), channels: [Channel.Email]));
        Result<SendReceipt> over = await host.SendAsync(Code(code: new string('a', 5200), channels: [Channel.Email]));

        Assert.True(fits.IsSuccess);
        AssertRefused(over, "messaging.send.toolong", "email");
    }

    [Fact]
    public async Task MSG_PUSH_002_AC2_APayloadOver3993BytesIsRefusedAtTheSend()
    {
        await using EngineHost host = await StartAsync(templates: key => key is { Channel: Channel.Push, Part: "body" } ? "{{ code }}" : key is { Channel: Channel.Push, Part: "title" } ? "T" : EngineHost.Templates(key));
        int fixedBytes = Encoding.UTF8.GetByteCount("""{"title":"T","body":"","lang":"en","dir":"ltr"}""");

        Result<SendReceipt> fits = await host.SendAsync(Code(code: new string('a', 3993 - fixedBytes), channels: [Channel.Push]));
        Result<SendReceipt> over = await host.SendAsync(Code(code: new string('a', 3994 - fixedBytes), channels: [Channel.Push]));

        Assert.True(fits.IsSuccess);
        AssertRefused(over, "messaging.send.toolong", "push");
    }

    [Fact]
    public async Task MSG_PUSH_002_AC1_APayloadCarriesItsMembersInTheSendsLanguageAndDirection()
    {
        await using EngineHost host = await StartAsync(templates: key => key switch
        {
            { Channel: Channel.Push, Part: "url" } => "https://www.example.com/orders/{{ code }}",
            { Channel: Channel.Push, Part: "tag" } => "order",
            _ => EngineHost.Templates(key),
        });

        Result<SendReceipt> result = await host.SendAsync(Code(language: "ar", channels: [Channel.Push]));
        Guid delivery = result.Value!.Deliveries[0].Delivery.Value;
        Dictionary<string, string> parts = new(StringComparer.Ordinal)
        {
            ["title"] = (await host.OpenAsync(delivery, "title"))!,
            ["body"] = (await host.OpenAsync(delivery, "body"))!,
            ["url"] = (await host.OpenAsync(delivery, "url"))!,
            ["tag"] = (await host.OpenAsync(delivery, "tag"))!,
        };

        Assert.Null(await host.OpenAsync(delivery, "icon"));
        Assert.Equal(
            """{"title":"Title ar","body":"Body ar 123456","url":"https://www.example.com/orders/123456","tag":"order","lang":"ar","dir":"rtl"}""",
            Encoding.UTF8.GetString(Hermes.Content.PushPayload.Of(parts, "ar", Hermes.Content.SendLanguage.Direction("ar"))));
    }

    [Fact]
    public async Task MSG_PUSH_002_AC4_TwoSendsWithTheSamePushTopicCarryTheSameTopicOf32Base64UrlCharacters()
    {
        await using EngineHost host = await StartAsync(declare: declare =>
        {
            EngineHost.Essential(declare);
            declare.Kind("sign.up", EngineHost.Category).Push();
        });

        string? first = await TopicAsync(host, EngineHost.Kind, "orders");
        string? second = await TopicAsync(host, EngineHost.Kind, "orders");
        string? another = await TopicAsync(host, "sign.up", "orders");
        string? none = await TopicAsync(host, EngineHost.Kind, null);

        Assert.Equal(first, second);
        Assert.Matches("^[A-Za-z0-9_-]{32}$", first);
        Assert.NotEqual(first, another);
        Assert.Null(none);
    }

    [Fact]
    public async Task MSG_TPL_002_AC4_ATemplateReadingTheFirstLineOfAnEmptyListIsRefusedWithRenderfailed()
    {
        await using EngineHost host = await StartAsync(templates: key => key is { Channel: Channel.Sms } ? "{{ lines.first.name }}" : EngineHost.Templates(key));

        Result<SendReceipt> result = await host.SendAsync(Code(channels: [Channel.Sms], values: new() { ["lines"] = new SendValue.List([]) }));

        AssertRefused(result, "messaging.send.renderfailed", "sms");
        Assert.Equal((0, 0, 0), await host.RecordedAsync());
    }

    [Fact]
    public async Task Send_ARenderPastItsSteps_IsRefusedWithRenderlimit()
    {
        await using EngineHost host = await StartAsync(
            templates: key => key is { Channel: Channel.Sms } ? "{% for a in (1..1000) %}{% for b in (1..1000) %}{% endfor %}{% endfor %}x" : EngineHost.Templates(key));

        Result<SendReceipt> result = await host.SendAsync(Code(channels: [Channel.Sms]));

        AssertRefused(result, "messaging.send.renderlimit", "sms");
        Assert.Equal((0, 0, 0), await host.RecordedAsync());
    }

    [Fact]
    public async Task Send_AListLongerThanTheStepsReadByATemplate_IsRefusedWithRenderlimit()
    {
        await using EngineHost host = await StartAsync(
            templates: key => key is { Channel: Channel.Sms } ? "{{ lines | size }}" : EngineHost.Templates(key),
            configure: configuration => configuration["Hermes:Templates:MaxSteps"] = "1000");
        List<IReadOnlyDictionary<string, SendValue>> lines = [.. Enumerable.Range(0, 1001).Select(_ => new Dictionary<string, SendValue>())];

        Result<SendReceipt> result = await host.SendAsync(Code(channels: [Channel.Sms], values: new() { ["lines"] = new SendValue.List(lines) }));

        AssertRefused(result, "messaging.send.renderlimit", "sms");
    }

    [Fact]
    public async Task Send_ADivisionByZero_IsRefusedWithRenderfailedNamingTheChannel()
    {
        await using EngineHost host = await StartAsync(templates: key => key is { Channel: Channel.Push, Part: "body" } ? "{{ 10 | divided_by: count }}" : EngineHost.Templates(key));

        Result<SendReceipt> result = await host.SendAsync(Code(values: new() { ["count"] = new SendValue.WholeNumber(0) }));

        AssertRefused(result, "messaging.send.renderfailed", "push");
        Assert.Equal((0, 0, 0), await host.RecordedAsync());
    }

    [Fact]
    public async Task MSG_TPL_002_AC1_ASendLeavingOutAValueItsTemplateReadsInABranchNotTakenIsAFaultAndRecordsNothing()
    {
        await using EngineHost host = await StartAsync(templates: key => key is { Channel: Channel.Sms } ? "{% if false %}{{ total }}{% endif %}{{ code }}" : EngineHost.Templates(key));

        await Assert.ThrowsAsync<ArgumentException>(() => host.SendAsync(Code(channels: [Channel.Sms])));
        Assert.Equal((0, 0, 0), await host.RecordedAsync());
    }

    [Fact]
    public async Task MSG_TPL_002_AC1_AValueReadOnlyByAChannelTheSendDoesNotUseNeedNotBePassed()
    {
        await using EngineHost host = await StartAsync(templates: key => key is { Channel: Channel.Push, Part: "body" } ? "{{ total }}" : EngineHost.Templates(key));

        Result<SendReceipt> result = await host.SendAsync(Code(channels: [Channel.Sms]));

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("orderId", "OrderID")]
    [InlineData("language", null)]
    [InlineData("Direction", null)]
    [InlineData("unsubscribe", null)]
    [InlineData("1st", null)]
    [InlineData("an-order", null)]
    [InlineData("", null)]
    public async Task MSG_TPL_002_AC2_AValueMisnamedNamedTwiceOrUnderAReservedNameIsAFault(string name, string? twice)
    {
        await using EngineHost host = await StartAsync();
        Dictionary<string, SendValue> values = new(StringComparer.Ordinal) { ["code"] = new SendValue.PlainText("1"), [name] = new SendValue.PlainText("x") };
        if (twice is not null)
        {
            values[twice] = new SendValue.PlainText("y");
        }

        await Assert.ThrowsAsync<ArgumentException>(() => host.SendAsync(Code(values: values)));
        Assert.Equal((0, 0, 0), await host.RecordedAsync());
    }

    [Fact]
    public async Task Send_AListInsideAList_IsAFault()
    {
        await using EngineHost host = await StartAsync();
        Dictionary<string, SendValue> inner = new() { ["lines"] = new SendValue.List([]) };

        await Assert.ThrowsAsync<ArgumentException>(() => host.SendAsync(Code(values: new() { ["lines"] = new SendValue.List([inner]) })));
    }

    [Fact]
    public async Task Send_AReservedNameInsideAListsMap_IsTaken()
    {
        await using EngineHost host = await StartAsync();
        Dictionary<string, SendValue> line = new() { ["language"] = new SendValue.PlainText("x") };

        Result<SendReceipt> result = await host.SendAsync(Code(values: new() { ["lines"] = new SendValue.List([line]) }));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task MSG_SEND_001_AC1_SendingAKindNotDeclaredOrRetiredIsAFaultAndRecordsNothing()
    {
        await using EngineHost host = await StartAsync(declare: declare =>
        {
            EngineHost.Essential(declare);
            declare.Retire("sign.up");
        });

        await Assert.ThrowsAsync<ArgumentException>(() => host.SendAsync(Code() with { Kind = "order.placed" }));
        await Assert.ThrowsAsync<ArgumentException>(() => host.SendAsync(Code() with { Kind = "sign.up" }));
        Assert.Equal((0, 0, 0), await host.RecordedAsync());
    }

    [Fact]
    public async Task Send_AChannelTheKindDoesNotDeclare_IsAFault()
    {
        await using EngineHost host = await StartAsync(declare: declare =>
        {
            declare.Category(EngineHost.Category).Essential();
            declare.Kind(EngineHost.Kind, EngineHost.Category).Sms();
        });

        await Assert.ThrowsAsync<ArgumentException>(() => host.SendAsync(Code(channels: [Channel.Email])));
        Assert.Equal((0, 0, 0), await host.RecordedAsync());
    }

    [Theory]
    [InlineData("reference")]
    [InlineData("key")]
    [InlineData("key-character")]
    [InlineData("topic")]
    public async Task Send_AReferenceKeyOrTopicOutsideItsBounds_IsAFault(string which)
    {
        await using EngineHost host = await StartAsync();
        Send send = which switch
        {
            "reference" => Code(recipient: new Recipient.Person(new string('p', 129))),
            "key" => Code() with { IdempotencyKey = new string('k', 129) },
            "key-character" => Code() with { IdempotencyKey = "keyé" },
            _ => Code() with { PushTopic = string.Empty },
        };

        await Assert.ThrowsAsync<ArgumentException>(() => host.SendAsync(send));
        Assert.Equal((0, 0, 0), await host.RecordedAsync());
    }

    [Fact]
    public async Task MSG_SEND_003_AC4_ALanguageMatchingNoneIsRenderedInTheDefaultAndArEgInAr()
    {
        await using EngineHost host = await StartAsync();

        Result<SendReceipt> french = await host.SendAsync(Code(language: "fr", channels: [Channel.Sms]));
        Result<SendReceipt> egyptian = await host.SendAsync(Code(language: "ar-EG", channels: [Channel.Sms]));
        Result<SendReceipt> upper = await host.SendAsync(Code(language: "AR", channels: [Channel.Sms]));
        Result<SendReceipt> none = await host.SendAsync(Code(language: null, channels: [Channel.Sms]));

        Assert.Equal("Sms en 123456", await host.OpenAsync(french.Value!.Deliveries[0].Delivery.Value, "text"));
        Assert.Equal("Sms ar 123456", await host.OpenAsync(egyptian.Value!.Deliveries[0].Delivery.Value, "text"));
        Assert.Equal("ar", await host.ColumnAsync<string>(upper.Value!.Deliveries[0].Delivery.Value, "language"));
        Assert.Equal("en", await host.ColumnAsync<string>(none.Value!.Deliveries[0].Delivery.Value, "language"));
    }

    [Fact]
    public async Task MSG_SMS_002_AC2_WithAllowedPrefixesPlus20ASendToPlus44IsRefusedAndRecordsNothing()
    {
        await using EngineHost host = await StartAsync(configure: configuration => configuration["Hermes:Sms:AllowedPrefixes:0"] = "+20");

        Result<SendReceipt> result = await host.SendAsync(Code(phone: "+447700900123", channels: [Channel.Sms]));

        AssertRefused(result, "messaging.send.destinationnotallowed", "sms");
        Assert.Equal((0, 0, 0), await host.RecordedAsync());
    }

    [Fact]
    public async Task MSG_TPL_005_AC3_AnArabicWordInAnHtmlPartAndInAPushPayloadArrivesAsItsOwnCharacters()
    {
        const string Word = "مرحبا";
        await using EngineHost host = await StartAsync(templates: key => key is { Channel: Channel.Email, Part: "html" } ? "<p>{{ code }}</p>" : EngineHost.Templates(key));

        Result<SendReceipt> sent = await host.SendAsync(Code(code: Word, language: "ar", channels: [Channel.Email]));
        string payload = Encoding.UTF8.GetString(PushPayload.Of(new Dictionary<string, string>(StringComparer.Ordinal) { ["title"] = Word, ["body"] = Word }, "ar", "rtl"));

        Assert.Equal($"<p>{Word}</p>", await host.OpenAsync(sent.Value!.Deliveries[0].Delivery.Value, "html"));
        Assert.Equal($$"""{"title":"{{Word}}","body":"{{Word}}","lang":"ar","dir":"rtl"}""", payload);
    }

    [Fact]
    public async Task MSG_TPL_007_AC1_ATemplateLoopingOverATenMillionItemListIsRefusedWithRenderlimitAndRecordsNothing()
    {
        IReadOnlyDictionary<string, SendValue> item = new Dictionary<string, SendValue>(StringComparer.Ordinal) { ["name"] = new SendValue.PlainText("x") };
        await using EngineHost host = await StartAsync(templates: key => key is { Channel: Channel.Sms } ? "{% for item in lines %}{% endfor %}{{ code }}" : EngineHost.Templates(key));

        Result<SendReceipt> result = await host.SendAsync(Code(channels: [Channel.Sms], values: new() { ["lines"] = new SendValue.List(Enumerable.Repeat(item, 10_000_000).ToArray()) }));

        AssertRefused(result, "messaging.send.renderlimit", "sms");
        Assert.Equal((0, 0, 0), await host.RecordedAsync());
    }

    [Theory]
    [InlineData("{% assign v = code %}{% for i in (1..30) %}{% assign v = v | append: v %}{% endfor %}{{ v }}")]
    [InlineData("{% assign c = code %}{% for i in (1..30) %}{% capture c %}{{ c }}{{ c }}{% endcapture %}{% endfor %}{{ c }}")]
    public async Task MSG_TPL_007_AC2_AStringGrownByAppendOrByACaptureInALoopIsRefusedWithToolongAndRecordsNothing(string template)
    {
        await using EngineHost host = await StartAsync(templates: key => key is { Channel: Channel.Email, Part: "text" } ? template : EngineHost.Templates(key));

        Result<SendReceipt> result = await host.SendAsync(Code(channels: [Channel.Email]));

        AssertRefused(result, "messaging.send.toolong", "email");
        Assert.Equal((0, 0, 0), await host.RecordedAsync());
    }

    [Fact]
    public async Task MSG_TPL_007_AC3_AJoinOfALongListWithALongSeparatorIsRefusedWithToolong()
    {
        SendValue.List lines = new([.. Enumerable.Range(0, 2000).Select(index => (IReadOnlyDictionary<string, SendValue>)new Dictionary<string, SendValue>(StringComparer.Ordinal) { ["name"] = new SendValue.PlainText("x") })]);
        await using EngineHost host = await StartAsync(templates: key => key is { Channel: Channel.Email, Part: "text" } ? "{{ lines | map: 'name' | join: separator }}" : EngineHost.Templates(key));

        Result<SendReceipt> result = await host.SendAsync(Code(channels: [Channel.Email], values: new() { ["lines"] = lines, ["separator"] = new SendValue.PlainText(new string('-', 1000)) }));

        AssertRefused(result, "messaging.send.toolong", "email");
        Assert.Equal((0, 0, 0), await host.RecordedAsync());
    }

    [Fact]
    public async Task MSG_TPL_001_AC1_AKindsEmailInArabicRendersItsArabicSubjectTextAndHtml()
    {
        await using EngineHost host = await StartAsync(templates: key => key switch
        {
            { Channel: Channel.Email, Part: "html", Language: "ar" } => "<p dir=\"{{ direction }}\">{{ code }}</p>",
            _ => EngineHost.Templates(key),
        });

        Result<SendReceipt> arabic = await host.SendAsync(Code(language: "ar", channels: [Channel.Email]));
        Result<SendReceipt> english = await host.SendAsync(Code(language: "en", channels: [Channel.Email]));
        Guid delivery = arabic.Value!.Deliveries[0].Delivery.Value;

        Assert.Equal("Subject ar 123456", await host.OpenAsync(delivery, "subject"));
        Assert.Equal("Text ar 123456", await host.OpenAsync(delivery, "text"));
        Assert.Equal("<p dir=\"rtl\">123456</p>", await host.OpenAsync(delivery, "html"));
        Assert.Null(await host.OpenAsync(english.Value!.Deliveries[0].Delivery.Value, "html"));
    }

    [Fact]
    public async Task MSG_TPL_002_AC3_FormatDateRendersInTheSendsTimeZoneAndATimeZoneThatDoesNotResolveIsTheDeployments()
    {
        await using EngineHost host = await StartAsync(templates: key => key is { Channel: Channel.Sms } ? "{{ at | format_date: \"HH:mm\" }}" : EngineHost.Templates(key));
        Dictionary<string, SendValue> values = new() { ["at"] = new SendValue.Instant(new DateTimeOffset(2026, 10, 27, 9, 30, 0, TimeSpan.Zero)) };

        Result<SendReceipt> cairo = await host.SendAsync(Code(channels: [Channel.Sms], values: values) with { TimeZone = "Africa/Cairo" });
        Result<SendReceipt> unknown = await host.SendAsync(Code(channels: [Channel.Sms], values: values) with { TimeZone = "Nowhere/Else" });

        Assert.Equal("12:30", await host.OpenAsync(cairo.Value!.Deliveries[0].Delivery.Value, "text"));
        Assert.Equal("Africa/Cairo", await host.ColumnAsync<string>(cairo.Value.Deliveries[0].Delivery.Value, "time_zone"));
        Assert.Equal("09:30", await host.OpenAsync(unknown.Value!.Deliveries[0].Delivery.Value, "text"));
        Assert.Equal("UTC", await host.ColumnAsync<string>(unknown.Value.Deliveries[0].Delivery.Value, "time_zone"));
    }

    [Fact]
    public async Task MSG_SEND_018_AC1_NoDestinationRenderedContentOrKeptIdempotencyKeyIsStoredInPlainText()
    {
        await using EngineHost host = await StartAsync();

        Result<SendReceipt> result = await host.SendAsync(Code(code: "secret-code-778899") with { IdempotencyKey = "order-secret-5566" });

        Assert.True(result.IsSuccess);
        string dump = await DumpAsync(host);
        Assert.DoesNotContain(Email, dump, StringComparison.Ordinal);
        Assert.DoesNotContain(Phone, dump, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-code-778899", dump, StringComparison.Ordinal);
        Assert.DoesNotContain("Subject en", dump, StringComparison.Ordinal);
        Assert.DoesNotContain("order-secret-5566", dump, StringComparison.Ordinal);
        Assert.Contains(Person, dump, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MSG_SEND_017_AC5_EachDeliveryKeepsItsSendsKeyProtectedAndItOpensInItsOwnRowAlone()
    {
        await using EngineHost host = await StartAsync();
        SendReceipt receipt = (await host.SendAsync(Code(channels: [Channel.Email, Channel.Sms]) with { IdempotencyKey = "order-7f3a" })).Value!;
        Guid email = receipt.Deliveries[0].Delivery.Value;
        Guid sms = receipt.Deliveries[1].Delivery.Value;
        byte[] stored = (await host.ColumnAsync<byte[]>(email, "idempotency_key"))!;

        ContentOpening elsewhere;
        AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using (scope)
        {
            elsewhere = await scope.ServiceProvider.GetRequiredService<ContentProtection>().OpenAsync(Person, stored, ContentContext.Of(ContentContext.Deliveries, sms, "idempotencykey"), TestContext.Current.CancellationToken);
        }

        Assert.False(stored.AsSpan().IndexOf("order-7f3a"u8) >= 0);
        Assert.Equal("order-7f3a", await host.OpenAsync(email, "idempotencykey", column: "idempotency_key"));
        Assert.Equal("order-7f3a", await host.OpenAsync(sms, "idempotencykey", column: "idempotency_key"));
        Assert.Equal(ContentOpeningStatus.Rejected, elsewhere.Status);
    }

    [Fact]
    public async Task MSG_SEND_018_AC2_ATemplateChangedAfterASendLeavesThatSendsContentAsRendered()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        Guid delivery;
        await using (EngineHost before = await EngineHost.StartAsync(connection))
        {
            Result<SendReceipt> result = await before.SendAsync(Code(channels: [Channel.Sms]));
            delivery = result.Value!.Deliveries[0].Delivery.Value;
        }

        await using EngineHost after = await EngineHost.StartAsync(connection, templates: key => key is { Channel: Channel.Sms } ? "Changed {{ code }}" : EngineHost.Templates(key));

        Assert.Equal("Sms en 123456", await after.OpenAsync(delivery, "text"));
    }

    [Fact]
    public async Task Send_ValuesOfTheLibrarysProtector_RecordTheirContentKeysVersion()
    {
        await using EngineHost host = await StartAsync();

        Result<SendReceipt> result = await host.SendAsync(Code(channels: [Channel.Sms]));

        Assert.Equal(1, await host.ColumnAsync<int?>(result.Value!.Deliveries[0].Delivery.Value, "content_key_version"));
    }

    [Fact]
    public async Task Send_AHostProtector_ProtectsAPersonsValuesAndTheLibrarysAnAnonymousRecipients()
    {
        await using EngineHost host = await StartAsync(services: services => services.AddSingleton<IContentProtector>(new ContentProtectorFake("vault")));

        Result<SendReceipt> person = await host.SendAsync(Code(channels: [Channel.Sms]));
        Result<SendReceipt> anonymous = await host.SendAsync(Code(channels: [Channel.Sms], recipient: new Recipient.Anonymous()));

        byte[] personal = (await host.ColumnAsync<byte[]>(person.Value!.Deliveries[0].Delivery.Value, "text"))!;
        byte[] library = (await host.ColumnAsync<byte[]>(anonymous.Value!.Deliveries[0].Delivery.Value, "text"))!;
        Assert.Equal([0x01, 5, .. "vault"u8], personal[..7]);
        Assert.Null(await host.ColumnAsync<int?>(person.Value.Deliveries[0].Delivery.Value, "content_key_version"));
        Assert.Equal([0x01, 9, .. "messaging"u8], library[..11]);
        Assert.Equal("Sms en 123456", await host.OpenAsync(person.Value.Deliveries[0].Delivery.Value, "text"));
    }

    [Fact]
    public async Task Send_AProtectorThatThrows_PropagatesAndRecordsNothing()
    {
        await using EngineHost host = await StartAsync(services: services => services.AddSingleton<IContentProtector>(new ContentProtectorFake("vault") { Throws = true }));

        await Assert.ThrowsAnyAsync<Exception>(() => host.SendAsync(Code(channels: [Channel.Sms])));
        Assert.Equal((0, 0, 0), await host.RecordedAsync());
    }

    [Fact]
    public async Task Send_NotBefore_IsTheDeliveriesDueInstant()
    {
        await using EngineHost host = await StartAsync();
        DateTimeOffset later = EngineHost.Start.AddHours(3);

        Result<SendReceipt> result = await host.SendAsync(Code(channels: [Channel.Sms]) with { NotBefore = later });

        Assert.Equal(later, await host.ColumnAsync<DateTime>(result.Value!.Deliveries[0].Delivery.Value, "due_at"));
    }

    [Fact]
    public async Task MSG_LIB_003_AC5_AnApplicationThatDoesNotRecordSendsThrowsFromSendAsync()
    {
        await using EngineHost host = await StartAsync(configure: configuration => configuration["Hermes:Messages:Enabled"] = "false");

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.SendAsync(Code()));
    }

    [Fact]
    public async Task MSG_LIB_003_AC6_AHostServiceRegisteredBeforeAddHermesThatSendsInItsStartIsRecordedAfterTheChecks()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        SendingService? sending = null;

        await using EngineHost host = await EngineHost.StartAsync(connection, services: services =>
            services.AddHostedService(provider => sending = new SendingService(Code(channels: [Channel.Sms]), provider.GetRequiredService<IServiceScopeFactory>())));

        Assert.True(sending!.Result!.IsSuccess);
        Assert.Equal((1, 1, 0), await host.RecordedAsync());
    }

    [Fact]
    public async Task Send_BeforeTheStart_IsAFault()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        Dictionary<string, string?> configuration = EngineHost.Configuration();
        using Microsoft.Extensions.Hosting.IHost host = TestHost.Build(configuration, EngineHost.Essential, services => services.AddSingleton<ISecretSource>(Secrets.Held(connection)));
        AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using (scope)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<IMessages>().SendAsync(Code(), TestContext.Current.CancellationToken).AsTask());
        }
    }

    private Task<EngineHost> StartAsync(
        Action<DeclarationBuilder>? declare = null,
        Func<TemplateKey, string?>? templates = null,
        Action<Dictionary<string, string?>>? configure = null,
        Action<IServiceCollection>? services = null)
    {
        return StartOnNewDatabaseAsync(declare, templates, configure, services);
    }

    private async Task<EngineHost> StartOnNewDatabaseAsync(
        Action<DeclarationBuilder>? declare,
        Func<TemplateKey, string?>? templates,
        Action<Dictionary<string, string?>>? configure,
        Action<IServiceCollection>? services)
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        return await EngineHost.StartAsync(connection, declare, templates, configure, services);
    }

    internal static Send Code(
        string? email = Email,
        string? phone = Phone,
        Recipient? recipient = null,
        IReadOnlyList<Channel>? channels = null,
        string code = "123456",
        string? language = "en",
        Dictionary<string, SendValue>? values = null)
    {
        Dictionary<string, SendValue> all = values ?? [];
        all.TryAdd("code", new SendValue.PlainText(code));
        return new Send
        {
            Kind = EngineHost.Kind,
            Recipient = recipient ?? new Recipient.Person(Person),
            Email = email,
            Phone = phone,
            Language = language,
            Values = all,
            Channels = channels,
        };
    }

    private static async Task<string?> TopicAsync(EngineHost host, string kind, string? topic)
    {
        Result<SendReceipt> result = await host.SendAsync(Code(channels: [Channel.Push]) with { Kind = kind, PushTopic = topic });
        return await host.ColumnAsync<string>(result.Value!.Deliveries[0].Delivery.Value, "push_topic");
    }

    private static async Task<string> DumpAsync(EngineHost host)
    {
        const string Dump = """
            SELECT string_agg(row_to_json(d)::text, ' ') FROM messaging.deliveries d
            UNION ALL SELECT string_agg(encode(coalesce(d.destination, ''::bytea) || coalesce(d.subject, ''::bytea) || coalesce(d.text, ''::bytea) || coalesce(d.body, ''::bytea) || coalesce(d.idempotency_key, ''::bytea), 'escape'), ' ') FROM messaging.deliveries d
            UNION ALL SELECT string_agg(row_to_json(s)::text, ' ') FROM messaging.sends s
            """;
        StringBuilder text = new();
        await using Npgsql.NpgsqlConnection database = new(host.Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using Npgsql.NpgsqlCommand command = new(Dump, database);
        await using Npgsql.NpgsqlDataReader reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            text.Append(await reader.IsDBNullAsync(0, TestContext.Current.CancellationToken) ? string.Empty : reader.GetString(0)).Append(' ');
        }

        return text.ToString();
    }

    private static void AssertRefused(Result<SendReceipt> result, string code, string channel)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(code, result.Failure!.Code);
        Assert.Equal(channel, result.Failure.Details["channel"]);
    }
}