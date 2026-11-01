using Hermes.Operations;
using Hermes.Sending;

namespace Hermes.Conversations;

/// <summary>
/// The conversations' tables as key retirement, re-protection and erasure know them (<c>06</c>
/// MSG-OPS-002, M-031): a message's text protected under its author's key, its author, text
/// and version emptied at the author's erasure; a person's participant and mention rows
/// deleted at theirs (<c>09</c> MSG-CONV-009), and their connections to the staff hub.
/// </summary>
internal static class ConversationTables
{
    /// <summary>The table a message's values are bound to, whatever the table is called.</summary>
    internal const string Messages = ContentContext.Messages;

    /// <summary>The field of a message's text.</summary>
    internal const string Text = "text";

    internal static KeyedTable[] All { get; } =
    [
        new KeyedTable("messages", Messages, person: "author", values: [new KeyedValue("text", Text)], contentKeyVersion: "content_key_version", erasure: KeyedErasure.Clear),
        new KeyedTable("participants", "participants", person: "person"),
        new KeyedTable("mentions", "mentions", person: "person"),
        new KeyedTable("connections", "connections", person: "person"),
    ];
}