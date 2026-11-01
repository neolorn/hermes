using System;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Operations;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace Hermes.Conversations.Live;

/// <summary>
/// The notification a conversation's write tells every process on (<c>09</c> MSG-CONV-008;
/// <c>08</c> section 8): <c>NOTIFY messaging_conversations</c> with
/// <c>&lt;event&gt;:&lt;conversationId&gt;:&lt;id&gt;</c>, the id a message's or a participant's,
/// never a person reference or a text.
/// </summary>
internal static class LiveNotices
{
    /// <summary>The channel the processes listen on.</summary>
    internal const string Channel = "messaging_conversations";

    /// <summary>A new message; the id is the message's.</summary>
    internal const string Message = "message";

    /// <summary>An edit or a delete; the id is the message's.</summary>
    internal const string Changed = "changed";

    /// <summary>A read position moved; the id is the participant's.</summary>
    internal const string Moved = "read";

    /// <summary>A participant typing; the id is the participant's.</summary>
    internal const string Typing = "typing";

    /// <summary>A participant's presence changed; the id is the participant's.</summary>
    internal const string Presence = "presence";

    private const string Notify = "SELECT pg_notify('messaging_conversations', @payload)";

    /// <summary>The payload of <paramref name="kind"/> in <paramref name="conversation"/> for <paramref name="id"/>.</summary>
    internal static string Payload(string kind, Guid conversation, Guid id)
    {
        return $"{kind}:{conversation:D}:{id:D}";
    }

    /// <summary>The event, conversation and id <paramref name="payload"/> holds, or none where it does not read.</summary>
    internal static (string Kind, Guid Conversation, Guid Id)? Parse(string payload)
    {
        string[] parts = payload.Split(':');
        return parts.Length == 3
            && parts[0] is Message or Changed or Moved or Typing or Presence
            && Guid.TryParseExact(parts[1], "D", out Guid conversation)
            && Guid.TryParseExact(parts[2], "D", out Guid id)
            ? (parts[0], conversation, id)
            : null;
    }

    /// <summary>Tells the processes of <paramref name="kind"/> when <paramref name="context"/>'s transaction commits, or at once outside one.</summary>
    internal static async Task TellAsync(MessagingContext context, string kind, Guid conversation, Guid id, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlRawAsync(Notify, [new NpgsqlParameter("payload", Payload(kind, conversation, id))], cancellationToken).ConfigureAwait(false);
    }
}