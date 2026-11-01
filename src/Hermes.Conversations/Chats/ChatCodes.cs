namespace Hermes.Conversations.Chats;

/// <summary>The codes of conversations' refusals (<c>08</c> section 1; <c>09</c> MSG-CONV-006).</summary>
internal static class ChatCodes
{
    /// <summary>A conversation unknown, or one the person may not read; never 403, so nobody learns it exists.</summary>
    internal const string ConversationNotFound = "messaging.conversation.notfound";

    /// <summary>A conversation's request outside its rule, <c>field</c> naming what.</summary>
    internal const string ConversationInvalid = "messaging.conversation.invalid";

    /// <summary>A direct conversation's participants past 16.</summary>
    internal const string ConversationFull = "messaging.conversation.full";

    /// <summary>A message the conversation does not hold.</summary>
    internal const string MessageNotFound = "messaging.message.notfound";

    /// <summary>A message's text, mentions, reply or type outside its rule, <c>field</c> naming what.</summary>
    internal const string MessageInvalid = "messaging.message.invalid";

    /// <summary>Editing or deleting another person's message.</summary>
    internal const string MessageNotAuthor = "messaging.message.notauthor";

    /// <summary>Editing a deleted message.</summary>
    internal const string MessageDeleted = "messaging.message.deleted";

    /// <summary>A tag or snooze on a closed conversation.</summary>
    internal const string ConversationClosed = "messaging.conversation.closed";

    /// <summary>A saved reply unknown, another person's own, or a topic's for a person outside its scope.</summary>
    internal const string ReplyNotFound = "messaging.reply.notfound";

    /// <summary>A saved reply's name or text outside its bounds, <c>field</c> naming which.</summary>
    internal const string ReplyInvalid = "messaging.reply.invalid";

    /// <summary>A saved reply past its owner's count.</summary>
    internal const string ReplyFull = "messaging.reply.full";

    /// <summary>A saved reply's name its owner already has.</summary>
    internal const string ReplyNameExists = "messaging.reply.nameexists";
}