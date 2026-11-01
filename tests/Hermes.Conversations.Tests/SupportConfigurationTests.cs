using System;
using System.Collections.Generic;

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
            ["Hermes:Conversations:Support:NoticeVersion"] = "v3",
        };
    }

    private static IConfigurationSection Hermes(Dictionary<string, string?> settings)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build().GetSection("Hermes");
    }
}