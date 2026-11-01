using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Hermes.Core;

/// <summary>
/// A conversation as a read answers it for a person (<c>09</c> MSG-CONV-001, MSG-CONV-006;
/// <c>05</c> MSG-API-009). It prints neither its scope nor its participants.
/// </summary>
public sealed record ConversationRecord
{
    /// <summary>The conversation's identifier.</summary>
    public required ConversationId Conversation { get; init; }

    /// <summary>The conversation's type.</summary>
    public required ConversationType Type { get; init; }

    /// <summary>A group conversation's scope, or none for any other.</summary>
    public ConversationScope? Scope { get; init; }

    /// <summary>A direct conversation's participants who have not left; none for a group or support conversation.</summary>
    public required IReadOnlyList<ParticipantRecord> Participants { get; init; }

    /// <summary>The messages after the person's read position that are neither theirs nor deleted.</summary>
    public required int Unread { get; init; }

    /// <summary>Whether the person muted the conversation.</summary>
    public required bool Muted { get; init; }

    /// <summary>The instant of its last message, or of its creation where it has none.</summary>
    public required DateTimeOffset LastAt { get; init; }

    /// <summary>A support conversation's topic; none for another (<c>09</c> MSG-CONV-002).</summary>
    public string? Topic { get; init; }

    /// <summary>A support conversation's status; none for another.</summary>
    public ConversationStatus? Status { get; init; }

    /// <summary>A queued support conversation's place in its topic's queue, from 1; none once it is assigned, and for another.</summary>
    public int? Place { get; init; }

    /// <summary>Whether a support conversation came through the offline form; none for another.</summary>
    public bool? Offline { get; init; }

    /// <summary>A support conversation's tags, declared in configuration; empty for another (<c>09</c> MSG-CONV-005).</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>The instant a snoozed support conversation wakes; none otherwise.</summary>
    public DateTimeOffset? SnoozedUntil { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Conversation = {Conversation.Value}, Type = {Type}, Unread = {Unread}, Muted = {Muted}, LastAt = {LastAt:O}");
        if (Topic is not null)
        {
            builder.Append(CultureInfo.InvariantCulture, $", Topic = {Topic}, Status = {Status}, Place = {Place}, Offline = {Offline}, Tags = [{string.Join(", ", Tags)}], SnoozedUntil = {SnoozedUntil:O}");
        }

        return true;
    }
}