using System;
using System.Collections.Generic;
using System.Linq;

using Hermes.Core;

namespace Hermes.Conversations.Support;

/// <summary>
/// A support topic as <c>Hermes:Conversations:Topics:&lt;name&gt;</c> configures it (<c>09</c>
/// MSG-CONV-002): its time zone, its weekly hours and dated exceptions, each a sorted list of
/// ranges in minutes from midnight, and the scope whose members staff it.
/// </summary>
internal sealed class SupportTopic(
    string name,
    TimeZoneInfo zone,
    IReadOnlyDictionary<DayOfWeek, IReadOnlyList<SupportHours>> hours,
    IReadOnlyDictionary<DateOnly, IReadOnlyList<SupportHours>> exceptions,
    ConversationScope scope)
{
    internal string Name { get; } = name;

    internal TimeZoneInfo Zone { get; } = zone;

    internal IReadOnlyDictionary<DayOfWeek, IReadOnlyList<SupportHours>> Hours { get; } = hours;

    internal IReadOnlyDictionary<DateOnly, IReadOnlyList<SupportHours>> Exceptions { get; } = exceptions;

    internal ConversationScope Scope { get; } = scope;

    /// <summary>Whether the topic is within its hours at <paramref name="now"/>, in its time zone: a date's exception first, its weekday's hours otherwise; a day not configured is closed.</summary>
    internal bool IsWithinHours(DateTimeOffset now)
    {
        DateTime local = TimeZoneInfo.ConvertTime(now, Zone).DateTime;
        DateOnly date = DateOnly.FromDateTime(local);
        IReadOnlyList<SupportHours> ranges = Exceptions.TryGetValue(date, out IReadOnlyList<SupportHours>? exception)
            ? exception
            : Hours.TryGetValue(local.DayOfWeek, out IReadOnlyList<SupportHours>? weekly) ? weekly : [];
        int minute = (local.Hour * 60) + local.Minute;
        return ranges.Any(range => minute >= range.From && minute < range.To);
    }
}

/// <summary>A range of a day's hours, <c>HH:mm-HH:mm</c>, in minutes from midnight, its end after its start and at most <c>24:00</c>.</summary>
internal readonly record struct SupportHours(int From, int To);