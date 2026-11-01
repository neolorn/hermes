using System;

namespace Hermes.Conversations;

/// <summary>
/// A conversation (<c>09</c> MSG-CONV-001): its type, origin and status as <c>08</c> sections 2
/// and 3 spell them, a group conversation's scope, unique with its kind, and its instants.
/// </summary>
internal sealed class ConversationRow
{
    public required Guid Id { get; set; }

    /// <summary><c>support</c>, <c>group</c> or <c>direct</c>.</summary>
    public required string Type { get; set; }

    /// <summary><c>web</c>, the only origin of release 2.</summary>
    public required string Origin { get; set; }

    /// <summary><c>open</c>, <c>snoozed</c> or <c>closed</c>.</summary>
    public required string Status { get; set; }

    /// <summary>A group conversation's scope kind, <c>organization</c> or <c>group</c>; none for any other.</summary>
    public string? ScopeKind { get; set; }

    /// <summary>A group conversation's scope reference; none for any other.</summary>
    public string? ScopeReference { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }

    /// <summary>The instant of its last message, or none.</summary>
    public DateTimeOffset? LastMessageAt { get; set; }
}