using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

using Fluid;
using Fluid.Values;

using Hermes.Content;
using Hermes.Core;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "unit")]
public sealed class TemplateRenderingTests
{
    private const int Megabyte = 1024 * 1024;

    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en", predefinedOnly: true);

    [Fact]
    public async Task MSG_TPL_005_AC1_AScriptValueInAnHtmlPartArrivesEncoded()
    {
        Rendering rendering = await Html("<p>{{ v }}</p>", ("v", Text("<script>")));

        Assert.Equal("<p>&lt;script&gt;</p>", rendering.Text);
    }

    [Fact]
    public async Task MSG_TPL_005_AC4_ACapturesMarkupAppendedWithAValueKeepsTheMarkupAndEncodesTheValue()
    {
        Rendering rendering = await Html("{% capture c %}x{% endcapture %}{{ c | append: v }}", ("v", Text("<script>")));

        Assert.Equal("x&lt;script&gt;", rendering.Text);
    }

    [Fact]
    public async Task RenderAsync_ValueAppendedWithACapturesMarkup_EncodesTheValueAndKeepsTheMarkup()
    {
        Rendering rendering = await Html("{% capture c %}<b>{{ v }}</b>{% endcapture %}{{ v | append: c }}", ("v", Text("<i>")));

        Assert.Equal("&lt;i&gt;<b>&lt;i&gt;</b>", rendering.Text);
    }

    [Fact]
    public async Task RenderAsync_TwoValuesAppendedInAnHtmlPart_AreEncodedOnce()
    {
        Rendering rendering = await Html("{{ v | append: w }}", ("v", Text("<a>")), ("w", Text("&")));

        Assert.Equal("&lt;a&gt;&amp;", rendering.Text);
    }

    [Fact]
    public async Task RenderAsync_DefaultWithACapturedDefault_EncodesTheValueItAnswers()
    {
        Rendering rendering = await Html(
            "{% capture c %}<b>{{ v }}</b>{% endcapture %}{% for line in lines %}{{ line.note | default: c }}{% endfor %}",
            ("v", Text("<i>")),
            ("lines", List(Entries(("note", Text("<u>"))))));

        Assert.Equal("&lt;u&gt;", rendering.Text);
    }

    [Fact]
    public async Task RenderAsync_JoinWithACapturedSeparator_EncodesTheItemsAndKeepsTheSeparator()
    {
        Rendering rendering = await Html(
            "{% capture c %}<br>{% endcapture %}{{ lines | map: \"name\" | join: c }}",
            ("lines", List(Entries(("name", Text("<a>"))), Entries(("name", Text("b"))))));

        Assert.Equal("&lt;a&gt;<br>b", rendering.Text);
    }

    [Fact]
    public async Task RenderAsync_ArabicWordInAnHtmlPart_ArrivesAsItsOwnCharacters()
    {
        Rendering rendering = await Html("<p>{{ v }}</p>", ("v", Text("مرحبا \"ok\"")));

        Assert.Equal("<p>مرحبا &quot;ok&quot;</p>", rendering.Text);
    }

    [Fact]
    public async Task RenderAsync_ATextPart_IsNotEncoded()
    {
        Rendering rendering = await Plain("{{ v }}", Megabyte, ("v", Text("<b> & \"x\"")));

        Assert.Equal("<b> & \"x\"", rendering.Text);
    }

    [Fact]
    public async Task MSG_TPL_002_AC3_FormatDateRendersAnInstantInTheSendsTimeZoneAndLanguage()
    {
        DateTimeOffset instant = new(2026, 3, 1, 22, 30, 0, TimeSpan.Zero);
        TimeZoneInfo cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        CultureInfo arabic = CultureInfo.GetCultureInfo("ar", predefinedOnly: true);

        Rendering rendering = await Render("{{ at | format_date: \"dddd HH:mm\" }}", html: false, RenderBudget.Bytes(Megabyte), arabic, cairo, ("at", new SendValue.Instant(instant)));

        string expected = TimeZoneInfo.ConvertTime(instant, cairo).ToString("dddd HH:mm", arabic);
        Assert.Equal(expected, rendering.Text);
        Assert.StartsWith(arabic.DateTimeFormat.GetDayName(DayOfWeek.Monday), rendering.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RenderAsync_FormatNumber_WritesTheNumberInTheSendsCulture()
    {
        CultureInfo german = CultureInfo.GetCultureInfo("de", predefinedOnly: true);

        Rendering rendering = await Render("{{ total | format_number: \"N2\" }}", html: false, RenderBudget.Bytes(Megabyte), german, TimeZoneInfo.Utc, ("total", new SendValue.DecimalNumber(1234.5m)));

        Assert.Equal("1.234,50", rendering.Text);
    }

    [Fact]
    public async Task MSG_TPL_002_AC4_AFirstLineReadFromAnEmptyListIsRefusedWithRenderFailed()
    {
        Rendering rendering = await Plain("{{ lines.first.name }}", Megabyte, ("lines", List()));

        Assert.Equal("messaging.send.renderfailed", rendering.Refusal);
    }

    [Fact]
    public async Task RenderAsync_AMemberMissingFromAPassedMap_IsRefusedWithRenderFailed()
    {
        Rendering rendering = await Plain("{% for line in lines %}{{ line.price }}{% endfor %}", Megabyte, ("lines", List(Entries(("name", Text("tea"))))));

        Assert.Equal("messaging.send.renderfailed", rendering.Refusal);
    }

    [Fact]
    public async Task RenderAsync_AListsEntryReadInAnotherCase_IsFound()
    {
        Rendering rendering = await Plain("{% for line in lines %}{{ line.Name }}{% endfor %}", Megabyte, ("lines", List(Entries(("name", Text("tea"))))));

        Assert.Equal("tea", rendering.Text);
    }

    [Fact]
    public async Task RenderAsync_AnAssignedNameReadBeforeItIsSet_IsRefusedWithRenderFailed()
    {
        Rendering rendering = await Plain("{{ total }}{% assign total = 1 %}", Megabyte);

        Assert.Equal("messaging.send.renderfailed", rendering.Refusal);
    }

    [Fact]
    public async Task RenderAsync_ForLoopReadOutsideALoop_IsRefusedWithRenderFailed()
    {
        Rendering rendering = await Plain("{{ forloop.index }}", Megabyte);

        Assert.Equal("messaging.send.renderfailed", rendering.Refusal);
    }

    [Fact]
    public async Task RenderAsync_DivisionByZero_IsRefusedWithRenderFailed()
    {
        Rendering rendering = await Plain("{{ total | divided_by: count }}", Megabyte, ("total", new SendValue.WholeNumber(4)), ("count", new SendValue.WholeNumber(0)));

        Assert.Equal("messaging.send.renderfailed", rendering.Refusal);
    }

    [Fact]
    public async Task RenderAsync_AnOverflow_IsRefusedWithRenderFailed()
    {
        Rendering rendering = await Plain("{{ total | times: total | times: total }}", Megabyte, ("total", new SendValue.DecimalNumber(decimal.MaxValue)));

        Assert.Equal("messaging.send.renderfailed", rendering.Refusal);
    }

    [Theory]
    [InlineData("{{ v | slice: offset }}")]
    [InlineData("{{ v | slice: 0, offset }}")]
    public async Task RenderAsync_SliceOffsetOrLengthNotAWholeNumber_IsRefusedWithRenderFailed(string template)
    {
        Rendering rendering = await Plain(template, Megabyte, ("v", Text("abc")), ("offset", new SendValue.DecimalNumber(1.5m)));

        Assert.Equal("messaging.send.renderfailed", rendering.Refusal);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(29)]
    public async Task RenderAsync_RoundDigitsOutsideZeroToTwentyEight_IsRefusedWithRenderFailed(long digits)
    {
        Rendering rendering = await Plain("{{ v | round: digits }}", Megabyte, ("v", new SendValue.DecimalNumber(1.25m)), ("digits", new SendValue.WholeNumber(digits)));

        Assert.Equal("messaging.send.renderfailed", rendering.Refusal);
    }

    [Fact]
    public async Task RenderAsync_RoundWithinItsDigits_Rounds()
    {
        Rendering rendering = await Plain("{{ v | round: 1 }}", Megabyte, ("v", new SendValue.DecimalNumber(1.26m)));

        Assert.Equal("1.3", rendering.Text);
    }

    [Fact]
    public async Task RenderAsync_ReplaceWithAnEmptySearch_AnswersItsInputUnchanged()
    {
        Rendering rendering = await Plain("{{ v | replace: \"\", \"x\" }}", Megabyte, ("v", Text("abc")));

        Assert.Equal("abc", rendering.Text);
    }

    [Fact]
    public async Task RenderAsync_ValueDoubledWithAppendThirtyTimes_IsRefusedWithTooLong()
    {
        string doubling = "{% assign v = v | append: v %}";
        string template = string.Concat(Enumerable.Repeat(doubling, 30)) + "{{ v }}";

        Rendering rendering = await Plain(template, Megabyte, ("v", Text("abcdefgh")));

        Assert.Equal("messaging.send.toolong", rendering.Refusal);
    }

    [Fact]
    public async Task RenderAsync_ValueBuiltInACaptureInsideALoop_IsRefusedWithTooLong()
    {
        Rendering rendering = await Plain("{% for i in (1..100000) %}{% capture c %}{{ c }}{{ v }}{% endcapture %}{% endfor %}", Megabyte, ("c", Text(string.Empty)), ("v", Text(new string('x', 10_000))));

        Assert.Equal("messaging.send.toolong", rendering.Refusal);
    }

    [Fact]
    public async Task RenderAsync_AReplaceWhoseResultWouldPassTheLimit_IsRefusedWithTooLong()
    {
        Rendering rendering = await Plain("{{ v | replace: \"a\", w }}", 10_000, ("v", Text(new string('a', 100))), ("w", Text(new string('b', 101))));

        Assert.Equal("messaging.send.toolong", rendering.Refusal);
    }

    [Fact]
    public async Task RenderAsync_AJoinOfALongListWithALongSeparator_IsRefusedWithTooLong()
    {
        IReadOnlyDictionary<string, SendValue>[] lines = [.. Enumerable.Repeat(Entries(("name", Text("tea"))), 1_000)];

        Rendering rendering = await Plain("{{ lines | map: \"name\" | join: separator }}", Megabyte, ("lines", new SendValue.List(lines)), ("separator", Text(new string('-', 2_000))));

        Assert.Equal("messaging.send.toolong", rendering.Refusal);
    }

    [Fact]
    public async Task RenderAsync_AUrlEncodeWhoseResultWouldPassTheLimit_IsRefusedWithTooLong()
    {
        Rendering rendering = await Plain("{{ v | url_encode | size }}", 10_000, ("v", Text(new string('م', 2_000))));

        Assert.Equal("messaging.send.toolong", rendering.Refusal);
    }

    [Fact]
    public async Task RenderAsync_AnSmsBufferPastItsUnits_IsRefusedWithTooLong()
    {
        Rendering rendering = await Render("{{ v }}{{ v }}", html: false, RenderBudget.Units(RenderBudget.SmsUnits), English, TimeZoneInfo.Utc, ("v", Text(new string('x', 800))));

        Assert.Equal("messaging.send.toolong", rendering.Refusal);
    }

    [Fact]
    public async Task RenderAsync_PartsSharingABudget_AreRefusedOnceTogetherTheyPassIt()
    {
        RenderBudget budget = RenderBudget.Bytes(10_000);
        Rendering first = await Render("{{ v }}", html: false, budget, English, TimeZoneInfo.Utc, ("v", Text(new string('x', 6_000))));
        Rendering second = await Render("{{ v }}", html: false, budget, English, TimeZoneInfo.Utc, ("v", Text(new string('x', 6_000))));

        Assert.NotNull(first.Text);
        Assert.Equal("messaging.send.toolong", second.Refusal);
    }

    [Fact]
    public void Take_AnArabicLetterAndAPairWrittenInTwoParts_CountTheirUtf8Bytes()
    {
        RenderBudget budget = RenderBudget.Bytes(100);
        bool afterHigh = false;
        string pair = char.ConvertFromUtf32(0x1F600);

        budget.Take("م".AsSpan(), ref afterHigh);
        budget.Take(pair.AsSpan(0, 1), ref afterHigh);
        budget.Take(pair.AsSpan(1, 1), ref afterHigh);

        Assert.Equal(6, budget.Used);
    }

    [Fact]
    public async Task RenderAsync_NestedLoopsPastTheSteps_AreRefusedWithRenderLimit()
    {
        Rendering rendering = await Plain("{% for i in (1..1000) %}{% for j in (1..1000) %}{% assign k = j %}{% endfor %}{% endfor %}", Megabyte);

        Assert.Equal("messaging.send.renderlimit", rendering.Refusal);
    }

    [Fact]
    public async Task RenderAsync_AFilterResultOfMoreItemsThanTheSteps_IsRefusedWithRenderLimit()
    {
        Rendering rendering = await Plain("{{ v | reverse | size }}", 2 * Megabyte, ("v", Text(new string('x', 100_001))));

        Assert.Equal("messaging.send.renderlimit", rendering.Refusal);
    }

    [Fact]
    public void Of_AListOfTenMillionItems_IsNotConverted()
    {
        SendValue.List lines = new(new RepeatedMaps(10_000_000, Entries(("name", Text("tea")))));

        Assert.Null(TemplateValues.Of(lines, 100_000));
    }

    private static SendValue.PlainText Text(string value)
    {
        return new SendValue.PlainText(value);
    }

    private static SendValue.List List(params IReadOnlyDictionary<string, SendValue>[] items)
    {
        return new SendValue.List(items);
    }

    private static Dictionary<string, SendValue> Entries(params (string Name, SendValue Value)[] entries)
    {
        return entries.ToDictionary(entry => entry.Name, entry => entry.Value, StringComparer.Ordinal);
    }

    private static Task<Rendering> Html(string template, params (string Name, SendValue Value)[] values)
    {
        return Render(template, html: true, RenderBudget.Bytes(Megabyte), English, TimeZoneInfo.Utc, values);
    }

    private static Task<Rendering> Plain(string template, long limit, params (string Name, SendValue Value)[] values)
    {
        return Render(template, html: false, RenderBudget.Bytes(limit), English, TimeZoneInfo.Utc, values);
    }

    private static async Task<Rendering> Render(string template, bool html, RenderBudget budget, CultureInfo culture, TimeZoneInfo zone, params (string Name, SendValue Value)[] values)
    {
        using IHost host = TestHost.Build(TestHost.Languages());
        IFluidTemplate parsed = host.Services.GetRequiredService<TemplateParser>().Parse(template) ?? throw new InvalidOperationException("The template does not parse.");
        TemplateRenderer renderer = host.Services.GetRequiredService<TemplateRenderer>();
        Dictionary<string, FluidValue> converted = values.ToDictionary(value => value.Name, value => TemplateValues.Of(value.Value, renderer.MaxSteps)!, StringComparer.OrdinalIgnoreCase);
        return await renderer.RenderAsync(parsed, converted, html, budget, culture, zone);
    }

    /// <summary>A list answering one map for each of its items, so a test holds a long list without its memory.</summary>
    private sealed class RepeatedMaps(int count, IReadOnlyDictionary<string, SendValue> map) : IReadOnlyList<IReadOnlyDictionary<string, SendValue>>
    {
        public int Count => count;

        public IReadOnlyDictionary<string, SendValue> this[int index] => map;

        public IEnumerator<IReadOnlyDictionary<string, SendValue>> GetEnumerator()
        {
            return Enumerable.Repeat(map, count).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}