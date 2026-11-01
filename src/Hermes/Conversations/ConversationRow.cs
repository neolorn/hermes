using System;

namespace Hermes.Conversations;

/// <summary>
/// A conversation (<c>09</c> MSG-CONV-001): its type, origin and status as <c>08</c> sections 2
/// and 3 spell them, a group conversation's scope, unique with its kind, and its instants; a
/// support conversation's topic, assignment, queue and close (MSG-CONV-002), the signed-in
/// customer it is for, and the offline form's address, protected under the customer's key or
/// as an anonymous recipient's value for a visitor (MSG-CONV-009).
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

    /// <summary>A support conversation's topic; none for any other.</summary>
    public string? Topic { get; set; }

    /// <summary>The staff member a support conversation is assigned to, or none while it is queued.</summary>
    public string? Assignee { get; set; }

    /// <summary>The instant a support conversation joined its topic's queue, the queue's order.</summary>
    public DateTimeOffset? QueuedAt { get; set; }

    /// <summary>The instant a support conversation was last assigned, or none.</summary>
    public DateTimeOffset? AssignedAt { get; set; }

    /// <summary>Whether a support conversation came through the offline form.</summary>
    public bool Offline { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    /// <summary>The staff member who closed it, or none where its visitor or customer did.</summary>
    public string? ClosedBy { get; set; }

    /// <summary>The signed-in customer a support conversation is for, or none for a visitor's.</summary>
    public string? Customer { get; set; }

    /// <summary>The offline form's protected address, bound to <c>conversations:&lt;id&gt;:email</c>.</summary>
    public byte[]? Email { get; set; }

    /// <summary>The <c>contentKeys</c> version the library's protector wrote the address under, or none.</summary>
    public int? EmailKeyVersion { get; set; }
}