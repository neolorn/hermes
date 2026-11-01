using System;
using System.Collections.Generic;
using System.Linq;

using Hermes.Conversations.Support;
using Hermes.Core;

using Microsoft.Extensions.Configuration;

using Xunit;

namespace Hermes.Conversations.Tests;

/// <summary>Support's keys under <c>Hermes:Conversations:</c> (<c>08</c> section 4; <c>09</c> MSG-CONV-002, MSG-CONV-010, MSG-CONV-011).</summary>
[Trait("Category", "unit")]
public sealed class SupportConfigurationTests
{
    private static readonly string[] Kinds = ["support-reply"];

    [Fact]
    public void Read_NoTopic_IsTheDefaults()
    {
        (SupportSettings? settings, string? invalid) = SupportConfiguration.Read(Hermes([]), Kinds);

        Assert.Null(invalid);
        Assert.Empty(settings!.Topics);
        Assert.Equal((3, 5, 20, 20, TimeSpan.FromDays(365)), (settings.Limit, settings.VisitorStarts, settings.VisitorMessages, settings.SourceStarts, settings.RetentionVisitor));
    }

    [Fact]
    public void Read_ATopic_ReadsItsZoneHoursExceptionsAndScope()
    {
        (SupportSettings? settings, string? invalid) = SupportConfiguration.Read(Hermes(Topic()), Kinds);

        Assert.Null(invalid);
        SupportTopic topic = settings!.Topics["billing"];
        Assert.Equal("Europe/Berlin", topic.Zone.Id);
        Assert.Equal([new SupportHours(9 * 60, 12 * 60), new SupportHours(13 * 60, 17 * 60)], topic.Hours[DayOfWeek.Monday]);
        Assert.Empty(topic.Exceptions[new DateOnly(2026, 12, 28)]);
        Assert.Equal(new ConversationScope { Kind = ScopeKind.Group, Reference = "team-billing" }, topic.Scope);
        Assert.Equal(("support-reply", "v3"), (settings.OfflineReplyKind, settings.NoticeVersion));
    }

    [Fact]
    public void IsWithinHours_IsReadInTheTopicsZoneAndAnExceptionReplacesItsDay()
    {
        SupportTopic topic = SupportConfiguration.Read(Hermes(Topic()), Kinds).Settings!.Topics["billing"];

        // Monday 2 November 2026: 08:30 UTC is 09:30 in Berlin; 11:30 UTC is 12:30, the lunch gap.
        Assert.True(topic.IsWithinHours(new DateTimeOffset(2026, 11, 2, 8, 30, 0, TimeSpan.Zero)));
        Assert.False(topic.IsWithinHours(new DateTimeOffset(2026, 11, 2, 7, 30, 0, TimeSpan.Zero)));
        Assert.False(topic.IsWithinHours(new DateTimeOffset(2026, 11, 2, 11, 30, 0, TimeSpan.Zero)));

        // Tuesday has no hours; Monday 28 December is closed by its exception; Thursday 24 December holds its own.
        Assert.False(topic.IsWithinHours(new DateTimeOffset(2026, 11, 3, 9, 0, 0, TimeSpan.Zero)));
        Assert.False(topic.IsWithinHours(new DateTimeOffset(2026, 12, 28, 9, 0, 0, TimeSpan.Zero)));
        Assert.True(topic.IsWithinHours(new DateTimeOffset(2026, 12, 24, 8, 0, 0, TimeSpan.Zero)));
    }

    [Theory]
    [InlineData("Hermes:Conversations:Topics:billing:TimeZone", "Mars/Olympus", "Conversations:Topics:billing:TimeZone")]
    [InlineData("Hermes:Conversations:Topics:billing:Hours:Monday:1", "11:00-17:00", "Conversations:Topics:billing:Hours:Monday")]
    [InlineData("Hermes:Conversations:Topics:billing:Hours:Monday:1", "17:00-13:00", "Conversations:Topics:billing:Hours:Monday")]
    [InlineData("Hermes:Conversations:Topics:billing:Hours:Monday:1", "13:00-25:00", "Conversations:Topics:billing:Hours:Monday")]
    [InlineData("Hermes:Conversations:Topics:billing:Hours:Funday:0", "09:00-10:00", "Conversations:Topics:billing:Hours:Funday")]
    [InlineData("Hermes:Conversations:Topics:billing:Exceptions:2026-12-28", "Open", "Conversations:Topics:billing:Exceptions:2026-12-28")]
    [InlineData("Hermes:Conversations:Topics:billing:Exceptions:28-12-2026", "Closed", "Conversations:Topics:billing:Exceptions:28-12-2026")]
    [InlineData("Hermes:Conversations:Topics:billing:Scope:Kind", "Team", "Conversations:Topics:billing:Scope:Kind")]
    [InlineData("Hermes:Conversations:Support:Limit", "101", "Conversations:Support:Limit")]
    [InlineData("Hermes:Conversations:Support:OfflineReplyKind", "unknown-kind", "Conversations:Support:OfflineReplyKind")]
    [InlineData("Hermes:Conversations:Support:TranscriptKind", "unknown-kind", "Conversations:Support:TranscriptKind")]
    [InlineData("Hermes:Conversations:Support:NoticeVersion", "", "Conversations:Support:NoticeVersion")]
    [InlineData("Hermes:Conversations:Limits:VisitorStarts", "0", "Conversations:Limits:VisitorStarts")]
    [InlineData("Hermes:Conversations:Limits:VisitorMessages", "10001", "Conversations:Limits:VisitorMessages")]
    [InlineData("Hermes:Conversations:Limits:SourceStarts", "many", "Conversations:Limits:SourceStarts")]
    [InlineData("Hermes:Conversations:Retention:Visitor", "12:00:00", "Conversations:Retention:Visitor")]
    public void Read_AValueOutsideItsRule_IsItsKey(string key, string value, string expected)
    {
        Dictionary<string, string?> settings = Topic();
        settings[key] = value;

        Assert.Equal(expected, SupportConfiguration.Read(Hermes(settings), Kinds).Invalid);
    }

    [Theory]
    [InlineData("Hermes:Conversations:Topics:billing:TimeZone", "Conversations:Topics:billing:TimeZone")]
    [InlineData("Hermes:Conversations:Topics:billing:Scope:Reference", "Conversations:Topics:billing:Scope:Reference")]
    [InlineData("Hermes:Conversations:Support:OfflineReplyKind", "Conversations:Support:OfflineReplyKind")]
    [InlineData("Hermes:Conversations:Support:TranscriptKind", "Conversations:Support:TranscriptKind")]
    [InlineData("Hermes:Conversations:Support:NoticeVersion", "Conversations:Support:NoticeVersion")]
    public void Read_AKeyRequiredWhereTopicsAreConfiguredMissing_IsItsKey(string key, string expected)
    {
        Dictionary<string, string?> settings = Topic();
        settings.Remove(key);

        Assert.Equal(expected, SupportConfiguration.Read(Hermes(settings), Kinds).Invalid);
    }

    [Fact]
    public void Read_ATopicNameOutsideTheNameRule_IsTheTopicsKey()
    {
        Dictionary<string, string?> settings = Topic("Billing");

        Assert.Equal("Conversations:Topics:Billing", SupportConfiguration.Read(Hermes(settings), Kinds).Invalid);
    }

    [Fact]
    public void UnknownKey_ATopicsKeysAndSupportsKeys_AreKnownAndAnotherKeyUnderATopicIsNot()
    {
        Dictionary<string, string?> settings = Topic();
        Assert.Null(ConversationsConfiguration.UnknownKey(Hermes(settings)));

        settings["Hermes:Conversations:Topics:billing:Colour"] = "blue";
        Assert.Equal("Conversations:Topics:billing:Colour", ConversationsConfiguration.UnknownKey(Hermes(settings)));
    }

    [Fact]
    public void MSG_CONV_003_AC1_RulesAreReadWithTheirTriggersTopicsAndWait()
    {
        Dictionary<string, string?> settings = Topic();
        settings["Hermes:Conversations:Replies:welcome:Trigger"] = "started";
        settings["Hermes:Conversations:Replies:patience:Trigger"] = "waiting";
        settings["Hermes:Conversations:Replies:patience:After"] = "00:10:00";
        settings["Hermes:Conversations:Replies:patience:Topics:0"] = "billing";

        (SupportSettings? read, string? invalid) = SupportConfiguration.Read(Hermes(settings), Kinds);

        Assert.Null(invalid);
        Assert.Equal(["patience", "welcome"], read!.Rules.Select(rule => rule.Name));
        ReplyRule patience = read.Rules[0];
        Assert.Equal(("waiting", TimeSpan.FromMinutes(10)), (patience.Trigger, patience.After));
        Assert.True(patience.Serves("billing"));
        Assert.False(patience.Serves("other"));
        Assert.True(read.Rules[1].Serves("other"));
        Assert.Null(ConversationsConfiguration.UnknownKey(Hermes(settings)));
    }

    [Theory]
    [InlineData("Hermes:Conversations:Replies:welcome:Trigger", "hello", "Conversations:Replies:welcome:Trigger")]
    [InlineData("Hermes:Conversations:Replies:welcome:Topics:0", "unknown", "Conversations:Replies:welcome:Topics:0")]
    [InlineData("Hermes:Conversations:Replies:welcome:After", "00:10:00", "Conversations:Replies:welcome:After")]
    [InlineData("Hermes:Conversations:Replies:Bad_Name:Trigger", "started", "Conversations:Replies:Bad_Name")]
    public void Read_ARuleOutsideItsRule_IsItsKey(string key, string value, string expected)
    {
        Dictionary<string, string?> settings = Topic();
        settings["Hermes:Conversations:Replies:welcome:Trigger"] = "started";
        settings[key] = value;

        Assert.Equal(expected, SupportConfiguration.Read(Hermes(settings), Kinds).Invalid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("00:00:59")]
    [InlineData("01:00:01")]
    public void Read_AWaitingRuleWithoutAWaitWithinItsRange_IsItsAfter(string? after)
    {
        Dictionary<string, string?> settings = Topic();
        settings["Hermes:Conversations:Replies:patience:Trigger"] = "waiting";
        settings["Hermes:Conversations:Replies:patience:After"] = after;

        Assert.Equal("Conversations:Replies:patience:After", SupportConfiguration.Read(Hermes(settings), Kinds).Invalid);
    }

    [Fact]
    public void Read_TagsCloseAfterAndCloseWarning_AreReadWithTheirDefaults()
    {
        Dictionary<string, string?> settings = Topic();
        SupportSettings defaults = SupportConfiguration.Read(Hermes(settings), Kinds).Settings!;
        Assert.Equal((TimeSpan.FromDays(1), TimeSpan.FromMinutes(5)), (defaults.CloseAfter, defaults.CloseWarning));
        Assert.Empty(defaults.Tags);

        settings["Hermes:Conversations:Tags:0"] = "vip";
        settings["Hermes:Conversations:Tags:1"] = "refund";
        settings["Hermes:Conversations:Support:CloseAfter"] = "02:00:00";
        settings["Hermes:Conversations:Support:CloseWarning"] = "00:15:00";
        SupportSettings read = SupportConfiguration.Read(Hermes(settings), Kinds).Settings!;

        Assert.Equal(["vip", "refund"], read.Tags);
        Assert.Equal((TimeSpan.FromHours(2), TimeSpan.FromMinutes(15)), (read.CloseAfter, read.CloseWarning));
        Assert.Null(ConversationsConfiguration.UnknownKey(Hermes(settings)));
    }

    [Fact]
    public void MSG_CONV_005_AC3_ATagOutsideTheNameRuleOrAFiftyFirstFailsTheStart()
    {
        Dictionary<string, string?> settings = Topic();
        settings["Hermes:Conversations:Tags:0"] = "Vip";
        Assert.Equal("Conversations:Tags:0", SupportConfiguration.Read(Hermes(settings), Kinds).Invalid);

        settings.Remove("Hermes:Conversations:Tags:0");
        for (int tag = 0; tag < 51; tag++)
        {
            settings["Hermes:Conversations:Tags:" + tag.ToString(System.Globalization.CultureInfo.InvariantCulture)] = "tag-" + tag.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        Assert.Equal("Conversations:Tags:50", SupportConfiguration.Read(Hermes(settings), Kinds).Invalid);
    }

    [Theory]
    [InlineData("Hermes:Conversations:Support:CloseAfter", "00:59:59", "Conversations:Support:CloseAfter")]
    [InlineData("Hermes:Conversations:Support:CloseAfter", "30.00:00:01", "Conversations:Support:CloseAfter")]
    [InlineData("Hermes:Conversations:Support:CloseWarning", "00:00:59", "Conversations:Support:CloseWarning")]
    [InlineData("Hermes:Conversations:Support:CloseWarning", "01:00:01", "Conversations:Support:CloseWarning")]
    public void Read_ACloseSpanOutsideItsRange_IsItsKey(string key, string value, string expected)
    {
        Dictionary<string, string?> settings = Topic();
        settings[key] = value;

        Assert.Equal(expected, SupportConfiguration.Read(Hermes(settings), Kinds).Invalid);
    }

    [Fact]
    public void Read_AWarningNoShorterThanTheClose_IsTheWarningsKey()
    {
        Dictionary<string, string?> settings = Topic();
        settings["Hermes:Conversations:Support:CloseAfter"] = "01:00:00";
        settings["Hermes:Conversations:Support:CloseWarning"] = "01:00:00";

        Assert.Equal("Conversations:Support:CloseWarning", SupportConfiguration.Read(Hermes(settings), Kinds).Invalid);
    }

    [Fact]
    public void NextOpening_IsTheNextRangesStartInTheTopicsZoneAndNoneWithoutHours()
    {
        SupportTopic topic = SupportConfiguration.Read(Hermes(Topic()), Kinds).Settings!.Topics["billing"];
        TimeSpan berlin = TimeSpan.FromHours(1);

        // Monday 2 November 2026: before the morning, in the lunch gap, and after the day, whose next range is Thursday's.
        Assert.Equal(new DateTimeOffset(2026, 11, 2, 9, 0, 0, berlin), topic.NextOpening(new DateTimeOffset(2026, 11, 2, 7, 30, 0, TimeSpan.Zero)));
        Assert.Equal(new DateTimeOffset(2026, 11, 2, 13, 0, 0, berlin), topic.NextOpening(new DateTimeOffset(2026, 11, 2, 11, 30, 0, TimeSpan.Zero)));
        Assert.Equal(new DateTimeOffset(2026, 11, 5, 9, 0, 0, berlin), topic.NextOpening(new DateTimeOffset(2026, 11, 2, 16, 30, 0, TimeSpan.Zero)));
        Assert.Equal(berlin, topic.NextOpening(new DateTimeOffset(2026, 11, 2, 16, 30, 0, TimeSpan.Zero))!.Value.Offset);

        Dictionary<string, string?> closed = Topic();
        foreach (string key in closed.Keys.Where(key => key.Contains(":Hours:", StringComparison.Ordinal) || key.Contains(":Exceptions:", StringComparison.Ordinal)).ToList())
        {
            closed.Remove(key);
        }

        Assert.Null(SupportConfiguration.Read(Hermes(closed), Kinds).Settings!.Topics["billing"].NextOpening(new DateTimeOffset(2026, 11, 2, 7, 30, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void Read_ATopicsSupervisors_AreItsScopeAndNoneWhereUnset()
    {
        Dictionary<string, string?> settings = Topic();
        Assert.Null(SupportConfiguration.Read(Hermes(settings), Kinds).Settings!.Topics["billing"].Supervisors);

        settings["Hermes:Conversations:Topics:billing:Supervisors:Kind"] = "Organization";
        settings["Hermes:Conversations:Topics:billing:Supervisors:Reference"] = "leads";

        Assert.Equal(new ConversationScope { Kind = ScopeKind.Organization, Reference = "leads" }, SupportConfiguration.Read(Hermes(settings), Kinds).Settings!.Topics["billing"].Supervisors);
        Assert.Null(ConversationsConfiguration.UnknownKey(Hermes(settings)));
    }

    [Theory]
    [InlineData("Hermes:Conversations:Topics:billing:Supervisors:Kind", "Group", "Conversations:Topics:billing:Supervisors:Reference")]
    [InlineData("Hermes:Conversations:Topics:billing:Supervisors:Reference", "leads", "Conversations:Topics:billing:Supervisors:Kind")]
    [InlineData("Hermes:Conversations:Topics:billing:Supervisors:Kind", "Team", "Conversations:Topics:billing:Supervisors:Kind")]
    public void Read_SupervisorsHalfGivenOrOutsideTheirRule_IsTheirKey(string key, string value, string expected)
    {
        Dictionary<string, string?> settings = Topic();
        settings[key] = value;

        Assert.Equal(expected, SupportConfiguration.Read(Hermes(settings), Kinds).Invalid);
    }

    private static Dictionary<string, string?> Topic(string name = "billing")
    {
        string topic = "Hermes:Conversations:Topics:" + name;
        return new(StringComparer.Ordinal)
        {
            [topic + ":TimeZone"] = "Europe/Berlin",
            [topic + ":Hours:Monday:0"] = "09:00-12:00",
            [topic + ":Hours:Monday:1"] = "13:00-17:00",
            [topic + ":Hours:Thursday:0"] = "09:00-17:00",
            [topic + ":Exceptions:2026-12-28"] = "Closed",
            [topic + ":Exceptions:2026-12-24:0"] = "08:00-10:00",
            [topic + ":Scope:Kind"] = "Group",
            [topic + ":Scope:Reference"] = "team-billing",
            ["Hermes:Conversations:Support:OfflineReplyKind"] = "support-reply",
            ["Hermes:Conversations:Support:TranscriptKind"] = "support-reply",
            ["Hermes:Conversations:Support:NoticeVersion"] = "v3",
        };
    }

    private static IConfigurationSection Hermes(Dictionary<string, string?> settings)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build().GetSection("Hermes");
    }
}