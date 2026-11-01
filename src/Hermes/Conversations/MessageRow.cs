using System;

namespace Hermes.Conversations;

/// <summary>
/// A conversation's message (<c>09</c> MSG-CONV-001): its author, its type, its text protected
/// under its author's key and bound to its row (<c>messages:&lt;id&gt;:text</c>), the message it
/// replies to, and its sent, edited and deleted instants. An erasure of its author empties its
/// author, its text and its version (MSG-CONV-009).
/// </summary>
internal sealed class MessageRow
{
    public required Guid Id { get; set; }

    public required Guid ConversationId { get; set; }

    /// <summary><c>person</c>, <c>visitor</c> or <c>library</c>.</summary>
    public required string AuthorKind { get; set; }

    /// <summary>The author's person reference, or none for the library and once the author is erased.</summary>
    public string? Author { get; set; }

    /// <summary><c>text</c>, <c>note</c>, <c>automatic</c> or <c>system</c>.</summary>
    public required string Type { get; set; }

    /// <summary>The protected text, or none once deleted or its author erased.</summary>
    public byte[]? Text { get; set; }

    /// <summary>The <c>contentKeys</c> version the library's protector wrote the text under, or none for a host's protector.</summary>
    public int? ContentKeyVersion { get; set; }

    public Guid? ReplyTo { get; set; }

    public required DateTimeOffset SentAt { get; set; }

    public DateTimeOffset? EditedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}