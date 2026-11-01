using System;
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
    /// <exception cref="ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A size outside 1 to 100.</exception>
    /// <exception cref="InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result<ConversationPage>> ListAsync(string person, string? cursor, int? size, CancellationToken cancellationToken);

    /// <summary>Starts a direct conversation of the person and 1 to 15 others.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="participants">The others' person references, distinct and not the person's.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The conversation; or a failure: <c>messaging.conversation.invalid</c> (<c>field</c> <c>participants</c>).</returns>
    /// <exception cref="ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result<ConversationRecord>> StartDirectAsync(string person, IReadOnlyList<string> participants, CancellationToken cancellationToken);

    /// <summary>Opens the group conversation of <paramref name="scope"/>, created the first time a member opens it.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="scope">The scope.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The conversation; or a failure: <c>messaging.conversation.notfound</c> for a person who is not a member; <c>messaging.request.malformed</c> (<c>member</c> <c>reference</c>) for a scope reference outside 1 to 128 characters.</returns>
    /// <exception cref="ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result<ConversationRecord>> OpenGroupAsync(string person, ConversationScope scope, CancellationToken cancellationToken);

    /// <summary>Reads a conversation.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="conversation">The conversation.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The conversation; or a failure: <c>messaging.conversation.notfound</c>.</returns>
    /// <exception cref="ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result<ConversationRecord>> ReadAsync(string person, ConversationId conversation, CancellationToken cancellationToken);

    /// <summary>Reads a page of a conversation's messages, newest first, or oldest first from <paramref name="after"/>.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="conversation">The conversation.</param>
    /// <param name="before">The message the page ends before, or none for the newest.</param>
    /// <param name="after">The message the page starts after, oldest first, or none; a client that reconnects reads what it missed from its last message.</param>
    /// <param name="size">The most messages the page holds, 1 to 100, or none for 50.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The page; or a failure: <c>messaging.conversation.notfound</c>; <c>messaging.request.malformed</c> (<c>member</c> <c>before</c> or <c>after</c>) for a message the conversation does not hold, or both given; <c>messaging.key.unavailable</c>.</returns>
    /// <exception cref="ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A size outside 1 to 100.</exception>
    /// <exception cref="InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result<MessagePage>> MessagesAsync(string person, ConversationId conversation, MessageId? before, MessageId? after, int? size, CancellationToken cancellationToken);

    /// <summary>Writes a message, its text protected under the person's key.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="conversation">The conversation.</param>
    /// <param name="text">The text, 1 to 4,000 characters.</param>
    /// <param name="type">The type; a person writes <see cref="MessageType.Text"/> alone.</param>
    /// <param name="replyTo">The message of the same conversation it replies to, or none.</param>
    /// <param name="mentions">The persons it mentions, at most 32 distinct, each a participant or member of the conversation; or none.</param>
    /// <param name="attachments">Up to 10 of the person's attachments of the conversation that no message names yet; or none.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The message; or a failure: <c>messaging.conversation.notfound</c>; <c>messaging.message.invalid</c> (<c>field</c> <c>text</c>, <c>mentions</c>, <c>replyTo</c>, <c>type</c> or <c>attachments</c>); <c>messaging.key.unavailable</c>.</returns>
    /// <exception cref="ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result<MessageRecord>> WriteAsync(string person, ConversationId conversation, string text, MessageType type, MessageId? replyTo, IReadOnlyList<string>? mentions, IReadOnlyList<AttachmentId>? attachments, CancellationToken cancellationToken);

    /// <summary>Replaces the text of one of the person's own <c>text</c> messages, keeping no history.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="conversation">The conversation.</param>
    /// <param name="message">The message.</param>
    /// <param name="text">The new text, 1 to 4,000 characters.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success; or a failure: <c>messaging.conversation.notfound</c>; <c>messaging.message.notfound</c>; <c>messaging.message.notauthor</c>; <c>messaging.message.deleted</c>; <c>messaging.message.invalid</c> (<c>field</c> <c>text</c>); <c>messaging.key.unavailable</c>.</returns>
    /// <exception cref="ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result> EditAsync(string person, ConversationId conversation, MessageId message, string text, CancellationToken cancellationToken);

    /// <summary>Deletes one of the person's own <c>text</c> messages: its text and mentions cleared, its row kept; a message already deleted changes nothing.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="conversation">The conversation.</param>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success; or a failure: <c>messaging.conversation.notfound</c>; <c>messaging.message.notfound</c>; <c>messaging.message.notauthor</c>.</returns>
    /// <exception cref="ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result> DeleteAsync(string person, ConversationId conversation, MessageId message, CancellationToken cancellationToken);

    /// <summary>Moves the person's read position forward to <paramref name="message"/>; a message at or before it changes nothing.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="conversation">The conversation.</param>
    /// <param name="message">The last message read.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success; or a failure: <c>messaging.conversation.notfound</c>; <c>messaging.message.notfound</c>.</returns>
    /// <exception cref="ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result> MarkReadAsync(string person, ConversationId conversation, MessageId message, CancellationToken cancellationToken);

    /// <summary>Mutes or unmutes the conversation for the person.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="conversation">The conversation.</param>
    /// <param name="muted">Whether it is muted.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success; or a failure: <c>messaging.conversation.notfound</c>.</returns>
    /// <exception cref="ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result> MuteAsync(string person, ConversationId conversation, bool muted, CancellationToken cancellationToken);

    /// <summary>Adds others to a direct conversation, up to 16 participants in all; one already in it changes nothing, and one who left comes back.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="conversation">The conversation.</param>
    /// <param name="participants">The others' person references.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success; or a failure: <c>messaging.conversation.notfound</c>; <c>messaging.conversation.invalid</c> (<c>field</c> <c>type</c> for a group conversation, <c>participants</c> for a reference outside its bounds); <c>messaging.conversation.full</c>.</returns>
    /// <exception cref="ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result> AddParticipantsAsync(string person, ConversationId conversation, IReadOnlyList<string> participants, CancellationToken cancellationToken);

    /// <summary>Leaves a direct conversation, ending the person's access at once; their messages stay.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="conversation">The conversation.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success; or a failure: <c>messaging.conversation.notfound</c>; <c>messaging.conversation.invalid</c> (<c>field</c> <c>type</c>) for a group conversation.</returns>
    /// <exception cref="ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result> LeaveAsync(string person, ConversationId conversation, CancellationToken cancellationToken);

    /// <summary>Sets whether the person, a staff member, is available for support; available and connected to the staff hub, they are assigned queued conversations of the topics whose scope they belong to (<c>09</c> MSG-CONV-002).</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="available">Whether they are available.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success.</returns>
    /// <exception cref="ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result> SetAvailableAsync(string person, bool available, CancellationToken cancellationToken);

    /// <summary>Reads a topic's queued support conversations, oldest first; none for a person outside the topic's scope.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="topic">The topic's name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The queue; or a failure: <c>messaging.request.malformed</c> (<c>member</c> <c>topic</c>) for a topic not configured.</returns>
    /// <exception cref="ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result<ConversationPage>> QueueAsync(string person, string topic, CancellationToken cancellationToken);

    /// <summary>Transfers an open support conversation to another member of its topic's scope, assigned at once, or to another topic's queue; exactly one of the two is given.</summary>
    /// <param name="person">The person reference, a member of the topic's scope.</param>
    /// <param name="conversation">The conversation.</param>
    /// <param name="toPerson">The member it goes to, or none.</param>
    /// <param name="topic">The topic whose queue it goes to, or none.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success; or a failure: <c>messaging.conversation.notfound</c>; <c>messaging.conversation.invalid</c> (<c>field</c> <c>person</c> for a person outside the topic's scope, <c>topic</c> for a topic not configured).</returns>
    /// <exception cref="ArgumentException">A person reference outside its bounds, or neither or both of <paramref name="toPerson"/> and <paramref name="topic"/>.</exception>
    /// <exception cref="InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result> TransferAsync(string person, ConversationId conversation, string? toPerson, string? topic, CancellationToken cancellationToken);

    /// <summary>Closes a support conversation; one already closed changes nothing.</summary>
    /// <param name="person">The person reference, a member of the topic's scope.</param>
    /// <param name="conversation">The conversation.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success; or a failure: <c>messaging.conversation.notfound</c>.</returns>
    /// <exception cref="ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result> CloseAsync(string person, ConversationId conversation, CancellationToken cancellationToken);

    /// <summary>Blocks a support conversation's visitor or customer from starting and writing, until <paramref name="until"/> or until lifted (<c>09</c> MSG-CONV-011).</summary>
    /// <param name="person">The person reference, a member of the topic's scope.</param>
    /// <param name="conversation">The conversation.</param>
    /// <param name="until">The instant the block ends, or none until it is lifted.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success; or a failure: <c>messaging.conversation.notfound</c>.</returns>
    /// <exception cref="ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result> BlockAsync(string person, ConversationId conversation, DateTimeOffset? until, CancellationToken cancellationToken);

    /// <summary>Lifts every block of a support conversation's visitor or customer.</summary>
    /// <param name="person">The person reference, a member of the topic's scope.</param>
    /// <param name="conversation">The conversation.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success; or a failure: <c>messaging.conversation.notfound</c>.</returns>
    /// <exception cref="ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result> UnblockAsync(string person, ConversationId conversation, CancellationToken cancellationToken);

    /// <summary>Puts the whole set of a support conversation's tags, each declared in configuration (<c>09</c> MSG-CONV-005).</summary>
    /// <param name="person">The person reference, a member of the topic's scope.</param>
    /// <param name="conversation">The conversation.</param>
    /// <param name="tags">The tags, the whole set.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success; or a failure: <c>messaging.conversation.notfound</c>; <c>messaging.conversation.invalid</c> (<c>field</c> <c>tags</c> for an undeclared tag, <c>type</c> for a conversation that is not support); <c>messaging.conversation.closed</c>.</returns>
    /// <exception cref="ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result> SetTagsAsync(string person, ConversationId conversation, IReadOnlyCollection<string> tags, CancellationToken cancellationToken);

    /// <summary>Snoozes a support conversation until <paramref name="until"/>, 5 minutes to 30 days ahead; it keeps its assignee, and wakes then or when its visitor or customer writes.</summary>
    /// <param name="person">The person reference, a member of the topic's scope.</param>
    /// <param name="conversation">The conversation.</param>
    /// <param name="until">The instant it wakes.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success; or a failure: <c>messaging.conversation.notfound</c>; <c>messaging.conversation.invalid</c> (<c>field</c> <c>until</c> outside its range, <c>type</c> for a conversation that is not support); <c>messaging.conversation.closed</c>.</returns>
    /// <exception cref="ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result> SnoozeAsync(string person, ConversationId conversation, DateTimeOffset until, CancellationToken cancellationToken);

    /// <summary>Reads a topic's shared saved replies, or the person's own where <paramref name="topic"/> is none, by name.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="topic">The topic, or none for the person's own.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The replies; or a failure: <c>messaging.request.malformed</c> (<c>member</c> <c>topic</c>) for a topic not configured; <c>messaging.reply.notfound</c> for a topic whose scope the person is not a member of.</returns>
    /// <exception cref="ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result<IReadOnlyList<SavedReplyRecord>>> RepliesAsync(string person, string? topic, CancellationToken cancellationToken);

    /// <summary>Adds a saved reply, shared by <paramref name="topic"/> or the person's own where it is none.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="name">The name, 1 to 64 characters, unique among its owner's.</param>
    /// <param name="text">The text, 1 to 4,000 characters.</param>
    /// <param name="topic">The topic, or none for the person's own.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The reply; or a failure: <c>messaging.reply.invalid</c> (<c>field</c> <c>name</c> or <c>text</c>); <c>messaging.reply.notfound</c> for a topic not configured or whose scope the person is not a member of; <c>messaging.reply.full</c>; <c>messaging.reply.nameexists</c>.</returns>
    /// <exception cref="ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result<SavedReplyRecord>> AddReplyAsync(string person, string name, string text, string? topic, CancellationToken cancellationToken);

    /// <summary>Changes a saved reply's name and text.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="reply">The reply.</param>
    /// <param name="name">The name, 1 to 64 characters, unique among its owner's.</param>
    /// <param name="text">The text, 1 to 4,000 characters.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success; or a failure: <c>messaging.reply.invalid</c> (<c>field</c> <c>name</c> or <c>text</c>); <c>messaging.reply.notfound</c>; <c>messaging.reply.nameexists</c>.</returns>
    /// <exception cref="ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result> ChangeReplyAsync(string person, SavedReplyId reply, string name, string text, CancellationToken cancellationToken);

    /// <summary>Removes a saved reply.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="reply">The reply.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success; or a failure: <c>messaging.reply.notfound</c>.</returns>
    /// <exception cref="ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result> RemoveReplyAsync(string person, SavedReplyId reply, CancellationToken cancellationToken);

    /// <summary>The live view of a topic for one of its supervisors: its open and snoozed conversations, oldest queued first.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="topic">The topic.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The conversations; or a failure: <c>messaging.request.malformed</c> for a topic not configured, <c>messaging.conversation.notfound</c> for a person who does not supervise it.</returns>
    /// <exception cref="ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result<IReadOnlyList<LiveConversation>>> LiveAsync(string person, string topic, CancellationToken cancellationToken);

    /// <summary>Uploads an attachment to a conversation the person may write in, protected in chunks and stored, pending its scan.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="conversation">The conversation.</param>
    /// <param name="name">The file name, 1 to 255 characters, without path separators or control characters.</param>
    /// <param name="mediaType">Its declared media type.</param>
    /// <param name="content">Its bytes, read to their end and never held whole.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The attachment, pending; or a failure: <c>messaging.conversation.notfound</c> (attachments disabled included); <c>messaging.attachment.type</c>; <c>messaging.attachment.invalid</c> (<c>field</c> <c>name</c>); <c>messaging.attachment.toolarge</c>; <c>messaging.key.unavailable</c>.</returns>
    /// <exception cref="ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result<AttachmentRecord>> UploadAttachmentAsync(string person, ConversationId conversation, string name, string mediaType, System.IO.Stream content, CancellationToken cancellationToken);

    /// <summary>Opens a clean attachment of a conversation the person may read, after the access check.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="conversation">The conversation.</param>
    /// <param name="attachment">The attachment.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The attachment and its content, which the caller disposes; or a failure: <c>messaging.conversation.notfound</c> (attachments disabled included); <c>messaging.attachment.notfound</c>; <c>messaging.attachment.pending</c>; <c>messaging.attachment.refused</c>; <c>messaging.key.unavailable</c>.</returns>
    /// <exception cref="ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result<AttachmentDownload>> OpenAttachmentAsync(string person, ConversationId conversation, AttachmentId attachment, CancellationToken cancellationToken);

    /// <summary>A topic's reports for one of its supervisors, over the conversations closed from <paramref name="from"/> through <paramref name="through"/>, both included, in the topic's time zone.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="topic">The topic.</param>
    /// <param name="from">The period's first day.</param>
    /// <param name="through">The period's last day, at most 92 days from the first, counting both.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The topic's figures first, then each staff member's; or a failure: <c>messaging.request.malformed</c> for a topic not configured or a period outside its rule, <c>messaging.conversation.notfound</c> for a person who does not supervise it.</returns>
    /// <exception cref="ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result<IReadOnlyList<ReportFigures>>> ReportAsync(string person, string topic, DateOnly from, DateOnly through, CancellationToken cancellationToken);
}