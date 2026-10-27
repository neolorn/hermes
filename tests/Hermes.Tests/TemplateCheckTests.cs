using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "unit")]
public sealed class TemplateCheckTests
{
    private const string Kind = "sign.in";

    private const string News = "news.weekly";

    [Fact]
    public async Task MSG_TPL_004_AC1_ARequiredPartMissingForADeclaredLanguageStopsTheStartNamingIt()
    {
        MessagingStartupException failure = await StartFailure(Essential, With(Kind, Channel.Sms, "text", null));

        AssertInvalid(failure, Kind, "sms.en.text");
    }

    [Fact]
    public async Task MSG_TPL_004_AC1_AnOptionalPartMissingDoesNotStopTheStart()
    {
        await AssertStartsPastTheTemplates(Essential, With(Kind, Channel.Email, "html", null));
    }

    [Theory]
    [InlineData("{% if code %}")]
    [InlineData("{{ code ")]
    [InlineData("{% endfor %}")]
    public async Task MSG_TPL_004_AC1_ATemplateThatDoesNotParseStopsTheStartNamingIt(string template)
    {
        MessagingStartupException failure = await StartFailure(Essential, With(Kind, Channel.Email, "subject", template));

        AssertInvalid(failure, Kind, "email.en.subject");
    }

    [Theory]
    [InlineData("{% include 'other' %}")]
    [InlineData("{% render 'other' %}")]
    [InlineData("{% raw %}{{ code }}{% endraw %}")]
    [InlineData("{% cycle 'a', 'b' %}")]
    [InlineData("{% increment n %}")]
    [InlineData("{% decrement n %}")]
    [InlineData("{% tablerow i in (1..2) %}{{ i }}{% endtablerow %}")]
    [InlineData("{% ifchanged %}x{% endifchanged %}")]
    [InlineData("{% echo code %}")]
    [InlineData("{% liquid echo code %}")]
    public async Task MSG_TPL_004_AC1_ATagOutsideTheAllowedSetStopsTheStartNamingIt(string template)
    {
        MessagingStartupException failure = await StartFailure(Essential, With(Kind, Channel.Sms, "text", template));

        AssertInvalid(failure, Kind, "sms.en.text");
    }

    [Theory]
    [InlineData("raw")]
    [InlineData("date: \"%Y\"")]
    [InlineData("json")]
    [InlineData("format_string")]
    [InlineData("time_zone: \"UTC\"")]
    [InlineData("newline_to_br")]
    [InlineData("md5")]
    [InlineData("sha256")]
    [InlineData("base64_encode")]
    [InlineData("escape")]
    [InlineData("split: \",\"")]
    [InlineData("concat: code")]
    [InlineData("unknown")]
    public async Task MSG_TPL_004_AC1_AFilterOutsideTheAllowedSetStopsTheStartNamingIt(string filter)
    {
        MessagingStartupException failure = await StartFailure(Essential, With(Kind, Channel.Sms, "text", "{{ code | " + filter + " }}"));

        AssertInvalid(failure, Kind, "sms.en.text");
    }

    [Fact]
    public async Task MSG_TPL_004_AC1_EveryAllowedTagAndFilterIsAccepted()
    {
        const string Template = """
            {% comment %}every allowed tag{% endcomment %}
            {% assign total = 0 %}
            {% capture heading %}{{ code | upcase }}{% endcapture %}
            {% if code == "a" %}a{% elsif code contains "b" %}b{% else %}c{% endif %}
            {% unless code %}none{% endunless %}
            {% case code %}{% when "a" %}a{% else %}other{% endcase %}
            {% for line in lines limit: 2 offset: 1 %}{% if forloop.first %}{% continue %}{% endif %}{{ line.name }}{% break %}{% else %}empty{% endfor %}
            {% for i in (1..3) reversed %}{{ i }}{% endfor %}
            {{ code | append: "x" | prepend: "y" | capitalize | downcase | lstrip | rstrip | strip | remove: "a" | remove_first: "b" | replace: "c", "d" | replace_first: "e", "f" | slice: 0, 2 | strip_html | strip_newlines | truncate: 5 | truncatewords: 2 | url_encode }}
            {{ total | abs | at_least: 1 | at_most: 9 | ceil | divided_by: 2 | floor | minus: 1 | modulo: 3 | plus: 4 | round: 2 | times: 5 }}
            {{ lines | first | size }}{{ lines | last | size }}{{ lines | map: "name" | join: ", " }}{{ lines | reverse | sort | sort_natural | uniq | compact | size }}{{ lines | where: "name", "a" | size }}
            {{ missing | default: "none" }}{{ at | format_date: "yyyy-MM-dd" }}{{ total | format_number: "N2" }}{{ heading }}{{ language }}{{ direction }}
            """;

        await AssertStartsPastTheTemplates(Essential, With(Kind, Channel.Email, "text", Template));
    }

    [Fact]
    public async Task MSG_TPL_004_AC1_ARangeWhoseBoundIsNotALiteralNumberStopsTheStartNamingIt()
    {
        MessagingStartupException failure = await StartFailure(Essential, With(Kind, Channel.Push, "body", "{% for i in (1..count) %}{{ i }}{% endfor %}"));

        AssertInvalid(failure, Kind, "push.en.body");
    }

    [Fact]
    public async Task MSG_TPL_007_AC4_ARangeSpanningPastTheStepsStopsTheStart()
    {
        MessagingStartupException failure = await StartFailure(Essential, With(Kind, Channel.Sms, "text", "{% for i in (1..2000000000) %}{{ i }}{% endfor %}"));

        AssertInvalid(failure, Kind, "sms.en.text");
    }

    [Fact]
    public async Task MSG_TPL_004_AC1_ARangeSpanningTheStepsIsAccepted()
    {
        await AssertStartsPastTheTemplates(Essential, With(Kind, Channel.Sms, "text", "{% for i in (1..100000) %}{% endfor %}"));
    }

    [Fact]
    public async Task MSG_TPL_007_AC4_IncludeAndRawStopTheStart()
    {
        MessagingStartupException include = await StartFailure(Essential, With(Kind, Channel.Sms, "text", "{% include 'other' %}"));
        MessagingStartupException raw = await StartFailure(Essential, With(Kind, Channel.Sms, "text", "{{ code | raw }}"));

        AssertInvalid(include, Kind, "sms.en.text");
        AssertInvalid(raw, Kind, "sms.en.text");
    }

    [Fact]
    public async Task MSG_TPL_007_AC4_ForLoopsNestedDeeperThanMaxRecursionStopTheStart()
    {
        Dictionary<string, string?> configuration = Configuration();
        configuration["Hermes:Templates:MaxRecursion"] = "2";

        MessagingStartupException failure = await StartFailure(Essential, With(Kind, Channel.Sms, "text", Nested(3)), configuration);

        AssertInvalid(failure, Kind, "sms.en.text");
    }

    [Fact]
    public async Task MSG_TPL_004_AC1_ForLoopsNestedAsDeepAsMaxRecursionAreAccepted()
    {
        Dictionary<string, string?> configuration = Configuration();
        configuration["Hermes:Templates:MaxRecursion"] = "2";

        await AssertStartsPastTheTemplates(Essential, With(Kind, Channel.Sms, "text", Nested(2) + "{% for i in lines %}{% endfor %}"), configuration);
    }

    [Fact]
    public async Task MSG_TPL_007_AC4_ForLoopsNestedDeeperThanTheDefaultMaxRecursionStopTheStart()
    {
        MessagingStartupException failure = await StartFailure(Essential, With(Kind, Channel.Sms, "text", Nested(21)));

        AssertInvalid(failure, Kind, "sms.en.text");
    }

    [Fact]
    public async Task MSG_TPL_004_AC3_AReplaceGivenOneArgumentStopsTheStart()
    {
        MessagingStartupException failure = await StartFailure(Essential, With(Kind, Channel.Sms, "text", "{{ code | replace: \"a\" }}"));

        AssertInvalid(failure, Kind, "sms.en.text");
    }

    [Theory]
    [InlineData("replace_first: \"a\"")]
    [InlineData("append")]
    [InlineData("prepend")]
    [InlineData("remove")]
    [InlineData("remove_first")]
    [InlineData("slice")]
    [InlineData("at_least")]
    [InlineData("at_most")]
    [InlineData("divided_by")]
    [InlineData("minus")]
    [InlineData("modulo")]
    [InlineData("plus")]
    [InlineData("times")]
    [InlineData("map")]
    [InlineData("where")]
    [InlineData("default")]
    [InlineData("format_date")]
    [InlineData("format_number")]
    public async Task MSG_TPL_004_AC1_AFilterGivenFewerArgumentsThanItRequiresStopsTheStart(string filter)
    {
        MessagingStartupException failure = await StartFailure(Essential, With(Kind, Channel.Sms, "text", "{{ code | " + filter + " }}"));

        AssertInvalid(failure, Kind, "sms.en.text");
    }

    [Fact]
    public async Task MSG_TPL_004_AC3_AFormatNumberWhoseFormatIsAValueStopsTheStart()
    {
        MessagingStartupException failure = await StartFailure(Essential, With(Kind, Channel.Sms, "text", "{{ total | format_number: format }}"));

        AssertInvalid(failure, Kind, "sms.en.text");
    }

    [Fact]
    public async Task MSG_TPL_004_AC1_AFormatDateWhoseFormatIsAValueStopsTheStart()
    {
        MessagingStartupException failure = await StartFailure(Essential, With(Kind, Channel.Sms, "text", "{{ at | format_date: format }}"));

        AssertInvalid(failure, Kind, "sms.en.text");
    }

    [Theory]
    [InlineData("F999999999")]
    [InlineData("F29")]
    [InlineData("n0029")]
    [InlineData("D99999999999")]
    public async Task MSG_TPL_004_AC3_AFormatNumberOfAPrecisionAboveTwentyEightStopsTheStart(string format)
    {
        MessagingStartupException failure = await StartFailure(Essential, With(Kind, Channel.Sms, "text", "{{ total | format_number: \"" + format + "\" }}"));

        AssertInvalid(failure, Kind, "sms.en.text");
    }

    [Theory]
    [InlineData("F28")]
    [InlineData("N2")]
    [InlineData("#,##0.00")]
    [InlineData("C")]
    public async Task MSG_TPL_004_AC1_AFormatNumberWithinItsPrecisionIsAccepted(string format)
    {
        await AssertStartsPastTheTemplates(Essential, With(Kind, Channel.Sms, "text", "{{ total | format_number: \"" + format + "\" }}"));
    }

    [Fact]
    public async Task MSG_TPL_004_AC1_UnsubscribeOutsideAnOptionalCategorysEmailStopsTheStart()
    {
        MessagingStartupException essential = await StartFailure(Essential, With(Kind, Channel.Email, "text", "{{ unsubscribe }}"));
        MessagingStartupException sms = await StartFailure(Optional, With(News, Channel.Sms, "text", "{{ unsubscribe }}"));

        AssertInvalid(essential, Kind, "email.en.text");
        AssertInvalid(sms, News, "sms.en.text");
    }

    [Fact]
    public async Task MSG_TPL_004_AC1_AnOptionalCategorysEmailTextNotUsingUnsubscribeStopsTheStart()
    {
        MessagingStartupException failure = await StartFailure(Optional, With(News, Channel.Email, "text", "Read the news."));

        AssertInvalid(failure, News, "email.en.text");
    }

    [Fact]
    public async Task MSG_TPL_004_AC1_AnOptionalCategorysEmailHtmlNotUsingUnsubscribeStopsTheStart()
    {
        MessagingStartupException failure = await StartFailure(Optional, With(News, Channel.Email, "html", "<p>Read the news.</p>"));

        AssertInvalid(failure, News, "email.en.html");
    }

    [Fact]
    public async Task MSG_TPL_004_AC1_AnOptionalCategorysEmailUsingUnsubscribeInEachPartIsAccepted()
    {
        await AssertStartsPastTheTemplates(Optional, With(News, Channel.Email, "html", "<a href=\"{{ unsubscribe }}\">x</a>"));
    }

    [Theory]
    [InlineData("unsubscribed", "{{ action }}")]
    [InlineData("unsubscribeinvalid", "{{ category }}")]
    [InlineData("unsubscribeinvalid", "{{ action }}")]
    [InlineData("unsubscribe", "{{ unsubscribe }}")]
    public async Task MSG_TPL_004_AC1_APageUsingAValueItIsNotGivenStopsTheStart(string page, string template)
    {
        MessagingStartupException failure = await StartFailure(Optional, Page(page, template));

        AssertInvalid(failure, page, "en.html");
    }

    [Fact]
    public async Task MSG_TPL_004_AC1_APageMissingForADeclaredLanguageWhereAnOptionalCategoryHasAnEmailKindStopsTheStart()
    {
        Dictionary<string, string?> configuration = Configuration("en", "fr");

        MessagingStartupException failure = await StartFailure(OptionalIn("en", "fr"), Shipped(TemplateSourceFake.Every(OptionalIn("en", "fr"))), configuration);

        AssertInvalid(failure, "unsubscribe", "fr.html");
    }

    [Fact]
    public async Task MSG_TPL_004_AC1_APageMissingWhereNoOptionalCategoryHasAnEmailKindDoesNotStopTheStart()
    {
        await AssertStartsPastTheTemplates(Essential, Shipped(TemplateSourceFake.Every(Essential)), Configuration("en", "fr"));
    }

    [Fact]
    public async Task MSG_TPL_004_AC2_ALanguageAddedWithoutItsTemplatesStopsTheStart()
    {
        TemplateSourceFake english = TemplateSourceFake.Every(Essential);

        MessagingStartupException failure = await StartFailure(Essential, key => key.Language == "en" ? Read(english, key) : null, Configuration("en", "ar"));

        AssertInvalid(failure, Kind, "email.ar.subject");
    }

    private static void Essential(DeclarationBuilder declare)
    {
        declare.Category("security").Essential();
        declare.Kind(Kind, "security").Email().Sms().Push();
    }

    private static void Optional(DeclarationBuilder declare)
    {
        declare.Category("news").Label("en", "News");
        declare.Kind(News, "news").Email().Sms();
    }

    private static Action<DeclarationBuilder> OptionalIn(params string[] languages)
    {
        return declare =>
        {
            CategoryBuilder category = declare.Category("news");
            foreach (string language in languages)
            {
                category.Label(language, "News");
            }

            declare.Kind(News, "news").Email();
        };
    }

    private static string Nested(int depth)
    {
        string template = "{{ code }}";
        for (int level = 0; level < depth; level++)
        {
            template = "{% for i" + level + " in lines %}" + template + "{% endfor %}";
        }

        return template;
    }

    /// <summary>The least templates of the essential and optional declarations, with <paramref name="text"/> answered for one part in every language.</summary>
    private static Func<TemplateKey, string?> With(string kind, Channel channel, string part, string? text)
    {
        Func<TemplateKey, string?> every = Shipped(TemplateSourceFake.Every(declare =>
        {
            Essential(declare);
            Optional(declare);
        }));
        return key => key.Kind == kind && key.Channel == channel && key.Part == part ? text : every(key);
    }

    private static Func<TemplateKey, string?> Page(string page, string text)
    {
        Func<TemplateKey, string?> every = With(News, Channel.Email, "subject", "News");
        return key => key.Page == page ? text : every(key);
    }

    /// <summary>The kinds' parts <paramref name="source"/> answers, every page left to the library's shipped ones.</summary>
    private static Func<TemplateKey, string?> Shipped(TemplateSourceFake source)
    {
        return key => key.Page is null ? Read(source, key) : null;
    }

    private static string? Read(TemplateSourceFake source, TemplateKey key)
    {
        return source.Answer(key);
    }

    private static Dictionary<string, string?> Configuration(params string[] languages)
    {
        Dictionary<string, string?> configuration = TestHost.Languages(languages);
        configuration["Hermes:PublicBaseAddress"] = "https://messages.example.com/notifications";
        return configuration;
    }

    private static async Task<MessagingStartupException> StartFailure(Action<DeclarationBuilder> declare, Func<TemplateKey, string?> answer, Dictionary<string, string?>? configuration = null)
    {
        using IHost host = TestHost.Build(configuration ?? Configuration(), declare, services => services.AddSingleton<ITemplateSource>(new TemplateSourceFake(answer)), hostTemplates: false);
        return await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));
    }

    private static async Task AssertStartsPastTheTemplates(Action<DeclarationBuilder> declare, Func<TemplateKey, string?> answer, Dictionary<string, string?>? configuration = null)
    {
        using IHost host = TestHost.Build(configuration ?? Configuration(), declare, services => services.AddSingleton<ITemplateSource>(new TemplateSourceFake(answer)), hostTemplates: false);
        MessagingStartupException? failure = await Record.ExceptionAsync(() => host.StartAsync(TestContext.Current.CancellationToken)) as MessagingStartupException;

        Assert.True(failure is null || failure.Code is not ("messaging.startup.declarationinvalid" or "messaging.startup.configurationinvalid"), $"The start stopped at the templates: {failure?.Code} {(failure is null ? null : string.Join(", ", failure.Details))}");
    }

    private static void AssertInvalid(MessagingStartupException failure, string declaration, string field)
    {
        Assert.Equal("messaging.startup.declarationinvalid", failure.Code);
        Assert.Equal(declaration, failure.Details["declaration"]);
        Assert.Equal(field, failure.Details["field"]);
    }
}