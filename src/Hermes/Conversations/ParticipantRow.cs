using System;

namespace Hermes.Conversations;

/// <summary>
/// A person, or in support a visitor, in a conversation (<c>09</c> MSG-CONV-001): their read
/// position, the last message they read, their mute, and the instants they joined and left. A
/// person reference is held as an in-app entry's is; a visitor only as the keyed hash of their
/// reference, under the <c>hashKeys</c> version it records. A row holds exactly one of the two.
/// </summary>
internal sealed class ParticipantRow
{
    public required Guid Id { get; set; }

    public required Guid ConversationId { get; set; }

    /// <summary>The person, or none for a visitor.</summary>
    public string? Person { get; set; }

    /// <summary>The keyed hash of a visitor's reference, or none for a person.</summary>
    public byte[]? Visitor { get; set; }

    /// <summary>The <c>hashKeys</c> version of <see cref="Visitor"/>, or none for a person.</summary>
    public int? HashVersion { get; set; }

    /// <summary>The last message read, or none.</summary>
    public Guid? ReadPosition { get; set; }

    public bool Muted { get; set; }

    /// <summary>The message the person was last told of while away, until their read position passes it (<c>09</c> MSG-CONV-008).</summary>
    public Guid? AwayTold { get; set; }

    public required DateTimeOffset JoinedAt { get; set; }

    /// <summary>The instant they left a direct conversation, or none while they are in it.</summary>
    public DateTimeOffset? LeftAt { get; set; }
}