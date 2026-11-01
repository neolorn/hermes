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

    /// <summary>A direct conversation's participants who have not left; none for a group conversation.</summary>
    public required IReadOnlyList<ParticipantRecord> Participants { get; init; }

    /// <summary>The messages after the person's read position that are neither theirs nor deleted.</summary>
    public required int Unread { get; init; }

    /// <summary>Whether the person muted the conversation.</summary>
    public required bool Muted { get; init; }

    /// <summary>The instant of its last message, or of its creation where it has none.</summary>
    public required DateTimeOffset LastAt { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Conversation = {Conversation.Value}, Type = {Type}, Unread = {Unread}, Muted = {Muted}, LastAt = {LastAt:O}");
        return true;
    }
}