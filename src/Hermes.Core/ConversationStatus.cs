using System.Text.Json.Serialization;

namespace Hermes.Core;

/// <summary>A conversation's status, one of a fixed set (<c>09</c> MSG-CONV-001; <c>08</c> section 3).</summary>
public enum ConversationStatus
{
    /// <summary>Open; a queued support conversation is open with no assignee.</summary>
    [JsonStringEnumMemberName("open")]
    Open,

    /// <summary>Snoozed.</summary>
    [JsonStringEnumMemberName("snoozed")]
    Snoozed,

    /// <summary>Closed.</summary>
    [JsonStringEnumMemberName("closed")]
    Closed,
}