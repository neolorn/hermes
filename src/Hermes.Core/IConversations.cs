using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hermes.Core;

/// <summary>
/// A person's direct and group conversations (<c>09</c> MSG-CONV-001, MSG-CONV-006), one
/// member for each route of the staff conversations group (<c>05</c> MSG-API-009). A
/// conversation the person may not read answers <c>messaging.conversation.notfound</c>, never
/// a refusal that says it exists. Every call takes the person's locks shared, waiting for
/// them no longer than <c>Hermes:Dispatch:Timeout</c>; a value that cannot open yet, or a key
/// a write needs that is not held, answers <c>messaging.key.unavailable</c>.
/// </summary>
public interface IConversations
{
    /// <summary>Reads a page of the person's direct conversations they have not left and the existing group conversations of their scopes, newest activity first.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="cursor">The cursor of the page to read, as an earlier page answered it, or none for the first.</param>
    /// <param name="size">The most conversations the page holds, 1 to 100, or none for 20.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The page; or a failure: <c>messaging.request.malformed</c> (<c>member</c> <c>cursor</c>) for a cursor that does not read.</returns>
    /// <exception cref="System.ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="System.ArgumentOutOfRangeException">A size outside 1 to 100.</exception>
    /// <exception cref="System.InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result<ConversationPage>> ListAsync(string person, string? cursor, int? size, CancellationToken cancellationToken);

    /// <summary>Starts a direct conversation of the person and 1 to 15 others.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="participants">The others' person references, distinct and not the person's.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The conversation; or a failure: <c>messaging.conversation.invalid</c> (<c>field</c> <c>participants</c>).</returns>
    /// <exception cref="System.ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="System.InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result<ConversationRecord>> StartDirectAsync(string person, IReadOnlyList<string> participants, CancellationToken cancellationToken);

    /// <summary>Opens the group conversation of <paramref name="scope"/>, created the first time a member opens it.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="scope">The scope.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The conversation; or a failure: <c>messaging.conversation.notfound</c> for a person who is not a member.</returns>
    /// <exception cref="System.ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="System.InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result<ConversationRecord>> OpenGroupAsync(string person, ConversationScope scope, CancellationToken cancellationToken);

    /// <summary>Reads a conversation.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="conversation">The conversation.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The conversation; or a failure: <c>messaging.conversation.notfound</c>.</returns>
    /// <exception cref="System.ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="System.InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result<ConversationRecord>> ReadAsync(string person, ConversationId conversation, CancellationToken cancellationToken);

    /// <summary>Reads a page of a conversation's messages, newest first.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="conversation">The conversation.</param>
    /// <param name="before">The message the page ends before, or none for the newest.</param>
    /// <param name="size">The most messages the page holds, 1 to 100, or none for 50.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The page; or a failure: <c>messaging.conversation.notfound</c>; <c>messaging.request.malformed</c> (<c>member</c> <c>before</c>) for a message the conversation does not hold; <c>messaging.key.unavailable</c>.</returns>
    /// <exception cref="System.ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="System.ArgumentOutOfRangeException">A size outside 1 to 100.</exception>
    /// <exception cref="System.InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result<MessagePage>> MessagesAsync(string person, ConversationId conversation, MessageId? before, int? size, CancellationToken cancellationToken);

    /// <summary>Writes a message, its text protected under the person's key.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="conversation">The conversation.</param>
    /// <param name="text">The text, 1 to 4,000 characters.</param>
    /// <param name="type">The type; a person writes <see cref="MessageType.Text"/> alone.</param>
    /// <param name="replyTo">The message of the same conversation it replies to, or none.</param>
    /// <param name="mentions">The persons it mentions, at most 32 distinct, each a participant or member of the conversation; or none.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The message; or a failure: <c>messaging.conversation.notfound</c>; <c>messaging.message.invalid</c> (<c>field</c> <c>text</c>, <c>mentions</c>, <c>replyTo</c> or <c>type</c>); <c>messaging.key.unavailable</c>.</returns>
    /// <exception cref="System.ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="System.InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result<MessageRecord>> WriteAsync(string person, ConversationId conversation, string text, MessageType type, MessageId? replyTo, IReadOnlyList<string>? mentions, CancellationToken cancellationToken);

    /// <summary>Replaces the text of one of the person's own <c>text</c> messages, keeping no history.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="conversation">The conversation.</param>
    /// <param name="message">The message.</param>
    /// <param name="text">The new text, 1 to 4,000 characters.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success; or a failure: <c>messaging.conversation.notfound</c>; <c>messaging.message.notfound</c>; <c>messaging.message.notauthor</c>; <c>messaging.message.deleted</c>; <c>messaging.message.invalid</c> (<c>field</c> <c>text</c>); <c>messaging.key.unavailable</c>.</returns>
    /// <exception cref="System.ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="System.InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result> EditAsync(string person, ConversationId conversation, MessageId message, string text, CancellationToken cancellationToken);

    /// <summary>Deletes one of the person's own <c>text</c> messages: its text and mentions cleared, its row kept; a message already deleted changes nothing.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="conversation">The conversation.</param>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success; or a failure: <c>messaging.conversation.notfound</c>; <c>messaging.message.notfound</c>; <c>messaging.message.notauthor</c>.</returns>
    /// <exception cref="System.ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="System.InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result> DeleteAsync(string person, ConversationId conversation, MessageId message, CancellationToken cancellationToken);

    /// <summary>Moves the person's read position forward to <paramref name="message"/>; a message at or before it changes nothing.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="conversation">The conversation.</param>
    /// <param name="message">The last message read.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success; or a failure: <c>messaging.conversation.notfound</c>; <c>messaging.message.notfound</c>.</returns>
    /// <exception cref="System.ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="System.InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result> MarkReadAsync(string person, ConversationId conversation, MessageId message, CancellationToken cancellationToken);

    /// <summary>Mutes or unmutes the conversation for the person.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="conversation">The conversation.</param>
    /// <param name="muted">Whether it is muted.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success; or a failure: <c>messaging.conversation.notfound</c>.</returns>
    /// <exception cref="System.ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="System.InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result> MuteAsync(string person, ConversationId conversation, bool muted, CancellationToken cancellationToken);

    /// <summary>Adds others to a direct conversation, up to 16 participants in all; one already in it changes nothing, and one who left comes back.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="conversation">The conversation.</param>
    /// <param name="participants">The others' person references.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success; or a failure: <c>messaging.conversation.notfound</c>; <c>messaging.conversation.invalid</c> (<c>field</c> <c>type</c> for a group conversation, <c>participants</c> for a reference outside its bounds); <c>messaging.conversation.full</c>.</returns>
    /// <exception cref="System.ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="System.InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result> AddParticipantsAsync(string person, ConversationId conversation, IReadOnlyList<string> participants, CancellationToken cancellationToken);

    /// <summary>Leaves a direct conversation, ending the person's access at once; their messages stay.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="conversation">The conversation.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success; or a failure: <c>messaging.conversation.notfound</c>; <c>messaging.conversation.invalid</c> (<c>field</c> <c>type</c>) for a group conversation.</returns>
    /// <exception cref="System.ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="System.InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result> LeaveAsync(string person, ConversationId conversation, CancellationToken cancellationToken);
}