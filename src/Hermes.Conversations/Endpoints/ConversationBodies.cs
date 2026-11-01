using System.Collections.Generic;

namespace Hermes.Conversations.Endpoints;

/// <summary>The answer of <c>GET /conversations</c> (<c>05</c> MSG-API-009).</summary>
/// <param name="Conversations">The page's conversations, newest activity first.</param>
/// <param name="Next">The cursor of the next page, or none on the last.</param>
internal sealed record ConversationsBody(IReadOnlyList<ConversationBody> Conversations, string? Next);

/// <summary>A conversation, <c>&lt;conversation&gt;</c> of <c>05</c> MSG-API-009, its instant in ISO 8601 UTC with milliseconds.</summary>
/// <param name="Id">The conversation's identifier, lower-case and hyphenated.</param>
/// <param name="Type">Its type, as <c>08</c> section 2 spells it.</param>
/// <param name="Scope">A group conversation's scope, or none.</param>
/// <param name="Participants">A direct conversation's participants who have not left; none for a group conversation.</param>
/// <param name="Unread">The messages after the person's read position that are neither theirs nor deleted.</param>
/// <param name="Muted">Whether the person muted it.</param>
/// <param name="LastAt">The instant of its last message, or of its creation where it has none.</param>
internal sealed record ConversationBody(string Id, string Type, ScopeBody? Scope, IReadOnlyList<ParticipantBody> Participants, int Unread, bool Muted, string LastAt);

/// <summary>A group conversation's scope (<c>05</c> MSG-API-009).</summary>
/// <param name="Kind">The scope's kind, as <c>08</c> section 2 spells it.</param>
/// <param name="Reference">The scope's reference.</param>
internal sealed record ScopeBody(string Kind, string Reference);

/// <summary>A participant, <c>&lt;participant&gt;</c> of <c>05</c> MSG-API-009.</summary>
/// <param name="Id">The participant's identifier, lower-case and hyphenated.</param>
/// <param name="Person">The participant's person reference.</param>
internal sealed record ParticipantBody(string Id, string Person);

/// <summary>The answer of <c>GET /conversations/{id}/messages</c> (<c>05</c> MSG-API-009).</summary>
/// <param name="Messages">The page's messages, newest first.</param>
internal sealed record MessagesBody(IReadOnlyList<MessageBody> Messages);

/// <summary>A message, <c>&lt;message&gt;</c> of <c>05</c> MSG-API-009, its instants in ISO 8601 UTC with milliseconds.</summary>
/// <param name="Id">The message's identifier, lower-case and hyphenated.</param>
/// <param name="AuthorKind">Who wrote it, as <c>08</c> section 2 spells it.</param>
/// <param name="Author">The author's person reference where the author is a person, none otherwise and once erased.</param>
/// <param name="Type">Its type, as <c>08</c> section 2 spells it.</param>
/// <param name="Text">Its text, none once deleted or its author erased.</param>
/// <param name="ReplyTo">The message it replies to, or none.</param>
/// <param name="Mentions">The persons it mentions.</param>
/// <param name="SentAt">The instant it was sent.</param>
/// <param name="EditedAt">The instant it was last edited, or none.</param>
/// <param name="DeletedAt">The instant it was deleted, or none.</param>
/// <param name="Erased">Whether its text was cleared by its author's erasure.</param>
internal sealed record MessageBody(
    string Id,
    string AuthorKind,
    string? Author,
    string Type,
    string? Text,
    string? ReplyTo,
    IReadOnlyList<string> Mentions,
    string SentAt,
    string? EditedAt,
    string? DeletedAt,
    bool Erased);

/// <summary>The body of <c>POST /conversations/direct</c> and <c>POST /conversations/{id}/participants</c> (<c>05</c> MSG-API-009).</summary>
/// <param name="Participants">The others' person references.</param>
internal sealed record ParticipantsRequest(IReadOnlyList<string>? Participants);

/// <summary>The body of <c>POST /conversations/{id}/messages</c> (<c>05</c> MSG-API-009).</summary>
/// <param name="Text">The text.</param>
/// <param name="Type">The type, <c>text</c> where none is given.</param>
/// <param name="ReplyTo">The message it replies to, or none.</param>
/// <param name="Mentions">The persons it mentions, or none.</param>
internal sealed record MessageRequest(string? Text, string? Type, string? ReplyTo, IReadOnlyList<string>? Mentions);

/// <summary>The body of <c>PUT /conversations/{id}/messages/{messageId}</c> (<c>05</c> MSG-API-009).</summary>
/// <param name="Text">The new text.</param>
internal sealed record EditRequest(string? Text);

/// <summary>The body of <c>POST /conversations/{id}/read</c> (<c>05</c> MSG-API-009).</summary>
/// <param name="MessageId">The last message read.</param>
internal sealed record ReadRequest(string? MessageId);

/// <summary>The body of <c>PUT /conversations/{id}/mute</c> (<c>05</c> MSG-API-009).</summary>
/// <param name="Muted">Whether the conversation is muted.</param>
internal sealed record MuteRequest(bool? Muted);