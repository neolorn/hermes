using Hermes.Operations;
using Hermes.Sending;

namespace Hermes.Conversations;

/// <summary>
/// The conversations' tables as key retirement, re-protection and erasure know them (<c>06</c>
/// MSG-OPS-002, M-031): a message's text protected under its owner's key, its owner, text
/// and version emptied at the owner's erasure, and its author reference at the author's; a
/// person's participant and mention rows deleted at theirs (<c>09</c> MSG-CONV-009), and their
/// connections to the staff hub. In support (M-034): a customer's support conversations
/// deleted at their erasure with every row of them, its offline address protected under
/// their key; an assignee's and a closer's reference emptied at theirs; a staff member's
/// availability and a person's blocks deleted at theirs; and the visitors' and source
/// addresses' keyed hashes, each recording its <c>hashKeys</c> version.
/// </summary>
internal static class ConversationTables
{
    /// <summary>The table a message's values are bound to, whatever the table is called.</summary>
    internal const string Messages = ContentContext.Messages;

    /// <summary>The table a support conversation's values are bound to, whatever the table is called.</summary>
    internal const string Conversations = ContentContext.Conversations;

    /// <summary>The field of a message's text.</summary>
    internal const string Text = "text";

    /// <summary>The field of a support conversation's offline address.</summary>
    internal const string Email = "email";

    internal static KeyedTable[] All { get; } =
    [
        new KeyedTable("messages", Messages, person: "owner", values: [new KeyedValue("text", Text)], contentKeyVersion: "content_key_version", erasure: KeyedErasure.Clear),
        new KeyedTable("messages", Messages, person: "author", erasure: KeyedErasure.Clear),
        new KeyedTable("conversations", Conversations, person: "customer", values: [new KeyedValue("email", Email)], contentKeyVersion: "email_key_version"),
        new KeyedTable("conversations", Conversations, person: "assignee", erasure: KeyedErasure.Clear),
        new KeyedTable("conversations", Conversations, person: "closed_by", erasure: KeyedErasure.Clear),
        new KeyedTable("participants", "participants", person: "person", hashVersion: "hash_version"),
        new KeyedTable("mentions", "mentions", person: "person"),
        new KeyedTable("connections", "connections", person: "person"),
        new KeyedTable("support_agents", "support_agents", person: "person"),
        new KeyedTable("support_blocks", "support_blocks", person: "person", hashVersion: "hash_version"),
        new KeyedTable("support_acknowledgements", "support_acknowledgements", hashVersion: "hash_version"),
        new KeyedTable("support_counters", "support_counters", hashVersion: "hash_version"),
    ];
}