using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using Hermes.Core;
using Hermes.Operations;
using Hermes.Sending;

using Microsoft.Extensions.Configuration;

namespace Hermes.Conversations.Support;

/// <summary>
/// Support's keys under <c>Hermes:Conversations:</c> (<c>08</c> section 4; <c>09</c>
/// MSG-CONV-002, MSG-CONV-010, MSG-CONV-011), read at start: each topic, the staff limit, the
/// offline reply's kind and the notice's version, required where topics are configured, the
/// limits and the visitor's retention. A value outside its rule is answered by its key.
/// </summary>
internal static partial class SupportConfiguration
{
    internal const string Topics = "Conversations:Topics";

    internal const string Limit = "Conversations:Support:Limit";

    internal const string OfflineReplyKind = "Conversations:Support:OfflineReplyKind";

    internal const string NoticeVersion = "Conversations:Support:NoticeVersion";

    internal const string VisitorStarts = "Conversations:Limits:VisitorStarts";

    internal const string VisitorMessages = "Conversations:Limits:VisitorMessages";

    internal const string SourceStarts = "Conversations:Limits:SourceStarts";

    internal const string RetentionVisitor = "Conversations:Retention:Visitor";

    private const int MostLimit = 100;

    private const int MostCount = 10_000;

    private const int MostVersionCharacters = 64;

    private static readonly TimeSpan LeastRetention = TimeSpan.FromDays(1);

    private static readonly TimeSpan MostRetention = TimeSpan.FromDays(3650);

    private static readonly string[] Days = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];

    /// <summary>The fixed keys of support.</summary>
    internal static IReadOnlyList<string> Fixed { get; } = [Limit, OfflineReplyKind, NoticeVersion, VisitorStarts, VisitorMessages, SourceStarts, RetentionVisitor];

    /// <summary>Whether <paramref name="key"/>, spelled as <c>08</c> section 4 spells keys, is one of a topic's keys.</summary>
    internal static bool IsTopicKey(string key)
    {
        return TopicKey().IsMatch(key);
    }

    /// <summary>
    /// Reads support's keys from <paramref name="hermes"/>, the section <c>Hermes</c>, a kind
    /// named being one of <paramref name="kinds"/>; answers the settings, or the first key
    /// outside its rule.
    /// </summary>
    internal static (SupportSettings? Settings, string? Invalid) Read(IConfiguration hermes, IReadOnlyCollection<string> kinds)
    {
        ArgumentNullException.ThrowIfNull(hermes);
        Dictionary<string, SupportTopic> topics = new(StringComparer.Ordinal);
        foreach (IConfigurationSection section in hermes.GetSection(Topics).GetChildren().OrderBy(child => child.Key, StringComparer.Ordinal))
        {
            (SupportTopic? topic, string? invalid) = ReadTopic(section);
            if (invalid is not null)
            {
                return (null, invalid);
            }

            topics.Add(topic!.Name, topic);
        }

        if (!Count(hermes, Limit, 3, MostLimit, out int limit))
        {
            return (null, Limit);
        }

        string? offline = hermes[OfflineReplyKind];
        if ((offline is null && topics.Count > 0) || (offline is not null && !kinds.Contains(offline, StringComparer.Ordinal)))
        {
            return (null, OfflineReplyKind);
        }

        string? notice = hermes[NoticeVersion];
        if ((notice is null && topics.Count > 0) || notice is { Length: 0 or > MostVersionCharacters })
        {
            return (null, NoticeVersion);
        }

        if (!Count(hermes, VisitorStarts, 5, MostCount, out int visitorStarts))
        {
            return (null, VisitorStarts);
        }

        if (!Count(hermes, VisitorMessages, 20, MostCount, out int visitorMessages))
        {
            return (null, VisitorMessages);
        }

        if (!Count(hermes, SourceStarts, 20, MostCount, out int sourceStarts))
        {
            return (null, SourceStarts);
        }

        TimeSpan retention = TimeSpan.FromDays(365);
        if (hermes[RetentionVisitor] is { } text
            && (!TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out retention) || retention < LeastRetention || retention > MostRetention))
        {
            return (null, RetentionVisitor);
        }

        return (new SupportSettings
        {
            Topics = topics,
            Limit = limit,
            OfflineReplyKind = offline,
            NoticeVersion = notice,
            VisitorStarts = visitorStarts,
            VisitorMessages = visitorMessages,
            SourceStarts = sourceStarts,
            RetentionVisitor = retention,
        }, null);
    }

    private static (SupportTopic? Topic, string? Invalid) ReadTopic(IConfigurationSection section)
    {
        string key = Topics + ConfigurationPath.KeyDelimiter + section.Key;
        if (!DeclarationCheck.IsName(section.Key))
        {
            return (null, key);
        }

        string zoneKey = key + ":TimeZone";
        if (section["TimeZone"] is not { } zoneName
            || !ConfigurationCheck.IsTimeZone(zoneName, TimeZoneInfo.GetSystemTimeZones())
            || !TimeZoneInfo.TryFindSystemTimeZoneById(zoneName, out TimeZoneInfo? zone))
        {
            return (null, zoneKey);
        }

        Dictionary<DayOfWeek, IReadOnlyList<SupportHours>> hours = [];
        foreach (IConfigurationSection day in section.GetSection("Hours").GetChildren())
        {
            int index = Array.IndexOf(Days, day.Key);
            string dayKey = key + ":Hours:" + day.Key;
            if (index < 0 || Ranges(day) is not { } ranges)
            {
                return (null, dayKey);
            }

            hours[(DayOfWeek)((index + 1) % 7)] = ranges;
        }

        Dictionary<DateOnly, IReadOnlyList<SupportHours>> exceptions = [];
        foreach (IConfigurationSection date in section.GetSection("Exceptions").GetChildren())
        {
            string dateKey = key + ":Exceptions:" + date.Key;
            if (!DateOnly.TryParseExact(date.Key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly day))
            {
                return (null, dateKey);
            }

            if (!date.GetChildren().Any() && string.Equals(date.Value, "Closed", StringComparison.Ordinal))
            {
                exceptions[day] = [];
            }
            else if (Ranges(date) is { } ranges)
            {
                exceptions[day] = ranges;
            }
            else
            {
                return (null, dateKey);
            }
        }

        ScopeKind? kind = section["Scope:Kind"] switch
        {
            "Organization" => ScopeKind.Organization,
            "Group" => ScopeKind.Group,
            _ => null,
        };
        if (kind is null)
        {
            return (null, key + ":Scope:Kind");
        }

        if (section["Scope:Reference"] is not { Length: >= 1 and <= 128 } reference)
        {
            return (null, key + ":Scope:Reference");
        }

        return (new SupportTopic(section.Key, zone, hours, exceptions, new ConversationScope { Kind = kind.Value, Reference = reference }), null);
    }

    /// <summary>A list of <c>HH:mm-HH:mm</c>, sorted, not overlapping, each starting before it ends; none where it is not.</summary>
    private static List<SupportHours>? Ranges(IConfigurationSection list)
    {
        IConfigurationSection[] items = [.. list.GetChildren()];
        if (items.Length == 0 || list.Value is { Length: > 0 })
        {
            return null;
        }

        List<SupportHours> ranges = [];
        for (int index = 0; index < items.Length; index++)
        {
            IConfigurationSection item = items.FirstOrDefault(each => string.Equals(each.Key, index.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))!;
            if (item is null || item.GetChildren().Any() || item.Value is not { } text || Range().Match(text) is not { Success: true } match)
            {
                return null;
            }

            int from = Minutes(match.Groups[1].Value, match.Groups[2].Value);
            int to = Minutes(match.Groups[3].Value, match.Groups[4].Value);
            if (from >= 24 * 60 || to > 24 * 60 || from >= to || (ranges.Count > 0 && from < ranges[^1].To))
            {
                return null;
            }

            ranges.Add(new SupportHours(from, to));
        }

        return ranges;
    }

    private static int Minutes(string hours, string minutes)
    {
        return (int.Parse(hours, CultureInfo.InvariantCulture) * 60) + int.Parse(minutes, CultureInfo.InvariantCulture);
    }

    private static bool Count(IConfiguration hermes, string key, int fallback, int most, out int count)
    {
        count = fallback;
        return hermes[key] is not { } text
            || (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out count) && count >= 1 && count <= most);
    }

    [GeneratedRegex(@"^([01][0-9]|2[0-4]):([0-5][0-9])-([01][0-9]|2[0-4]):([0-5][0-9])\z", RegexOptions.CultureInvariant)]
    private static partial Regex Range();

    [GeneratedRegex(@"^Conversations:Topics:[^:]+:(TimeZone|Scope:Kind|Scope:Reference|Hours:[^:]+(:[^:]+)?|Exceptions:[^:]+(:[^:]+)?)\z", RegexOptions.CultureInvariant)]
    private static partial Regex TopicKey();
}

/// <summary>Support's settings read at start.</summary>
internal sealed class SupportSettings
{
    internal IReadOnlyDictionary<string, SupportTopic> Topics { get; init; } = new Dictionary<string, SupportTopic>(StringComparer.Ordinal);

    internal int Limit { get; init; } = 3;

    internal string? OfflineReplyKind { get; init; }

    internal string? NoticeVersion { get; init; }

    internal int VisitorStarts { get; init; } = 5;

    internal int VisitorMessages { get; init; } = 20;

    internal int SourceStarts { get; init; } = 20;

    internal TimeSpan RetentionVisitor { get; init; } = TimeSpan.FromDays(365);
}