using System;

namespace Hermes.Conversations;

/// <summary>
/// A person in a conversation (<c>09</c> MSG-CONV-001): their read position, the last message
/// they read, their mute, and the instants they joined and left. A person reference is held as
/// an in-app entry's is.
/// </summary>
internal sealed class ParticipantRow
{
    public required Guid Id { get; set; }

    public required Guid ConversationId { get; set; }

    public required string Person { get; set; }

    /// <summary>The last message read, or none.</summary>
    public Guid? ReadPosition { get; set; }

    public bool Muted { get; set; }

    /// <summary>The message the person was last told of while away, until their read position passes it (<c>09</c> MSG-CONV-008).</summary>
    public Guid? AwayTold { get; set; }

    public required DateTimeOffset JoinedAt { get; set; }

    /// <summary>The instant they left a direct conversation, or none while they are in it.</summary>
    public DateTimeOffset? LeftAt { get; set; }
}