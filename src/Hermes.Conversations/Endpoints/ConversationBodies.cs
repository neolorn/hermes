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
/// <param name="Participants">A direct conversation's participants who have not left; none for a group or support conversation.</param>
/// <param name="Unread">The messages after the person's read position that are neither theirs nor deleted.</param>
/// <param name="Muted">Whether the person muted it.</param>
/// <param name="LastAt">The instant of its last message, or of its creation where it has none.</param>
/// <param name="Topic">A support conversation's topic, none for another.</param>
/// <param name="Status">A support conversation's status, as <c>08</c> section 3 spells it, none for another.</param>
/// <param name="Place">A queued support conversation's place in its topic's queue, none once assigned and for another.</param>
/// <param name="Offline">Whether a support conversation came through the offline form, none for another.</param>
/// <param name="Tags">A support conversation's tags, empty for another.</param>
/// <param name="SnoozedUntil">The instant a snoozed conversation wakes, none otherwise.</param>
internal sealed record ConversationBody(string Id, string Type, ScopeBody? Scope, IReadOnlyList<ParticipantBody> Participants, int Unread, bool Muted, string LastAt, string? Topic, string? Status, int? Place, bool? Offline, IReadOnlyList<string> Tags, string? SnoozedUntil);

/// <summary>A conversation on the public side (<c>05</c> MSG-API-010): the staff group's shape without its tags and waking instant, which are on staff bodies only.</summary>
/// <param name="Id">The conversation's identifier, lower-case and hyphenated.</param>
/// <param name="Type">Its type, <c>support</c>.</param>
/// <param name="Scope">None.</param>
/// <param name="Participants">None.</param>
/// <param name="Unread">The messages after the caller's read position that are neither theirs, nor deleted, nor notes.</param>
/// <param name="Muted">Whether the caller muted it.</param>
/// <param name="LastAt">The instant of its last message, or of its creation where it has none.</param>
/// <param name="Topic">Its topic.</param>
/// <param name="Status">Its status, as <c>08</c> section 3 spells it.</param>
/// <param name="Place">The visitor's place in the queue, none once assigned.</param>
/// <param name="Offline">Whether it came through the offline form.</param>
internal sealed record PublicConversationBody(string Id, string Type, ScopeBody? Scope, IReadOnlyList<ParticipantBody> Participants, int Unread, bool Muted, string LastAt, string? Topic, string? Status, int? Place, bool? Offline);

/// <summary>A group conversation's scope (<c>05</c> MSG-API-009).</summary>
/// <param name="Kind">The scope's kind, as <c>08</c> section 2 spells it.</param>
/// <param name="Reference">The scope's reference.</param>
internal sealed record ScopeBody(string Kind, string Reference);

/// <summary>A participant, <c>&lt;participant&gt;</c> of <c>05</c> MSG-API-009.</summary>
/// <param name="Id">The participant's identifier, lower-case and hyphenated.</param>
/// <param name="Person">The participant's person reference.</param>
/// <param name="Online">Whether the participant holds a staff-hub connection.</param>
internal sealed record ParticipantBody(string Id, string Person, bool Online);

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

/// <summary>The answer of <c>GET /support/queue</c> (<c>05</c> MSG-API-009).</summary>
/// <param name="Conversations">The topic's queued conversations, oldest first.</param>
internal sealed record QueueBody(IReadOnlyList<ConversationBody> Conversations);

/// <summary>The body of <c>PUT /support/availability</c> (<c>05</c> MSG-API-009).</summary>
/// <param name="Available">Whether the staff member is available.</param>
internal sealed record AvailabilityRequest(bool? Available);

/// <summary>The body of <c>POST /support/conversations/{id}/transfer</c> (<c>05</c> MSG-API-009), one of its members given.</summary>
/// <param name="Person">The member it goes to.</param>
/// <param name="Topic">The topic whose queue it goes to.</param>
internal sealed record TransferRequest(string? Person, string? Topic);

/// <summary>The body of <c>POST /support/conversations/{id}/block</c> (<c>05</c> MSG-API-009).</summary>
/// <param name="Until">The instant the block ends, or none until it is lifted.</param>
internal sealed record BlockRequest(string? Until);

/// <summary>The answer of <c>GET /support/topics</c> (<c>05</c> MSG-API-010).</summary>
/// <param name="Topics">Each topic.</param>
internal sealed record TopicsBody(IReadOnlyList<TopicBody> Topics);

/// <summary>A topic of the public side (<c>05</c> MSG-API-010).</summary>
/// <param name="Name">The topic's name.</param>
/// <param name="Open">Whether it is within its hours with a member available.</param>
internal sealed record TopicBody(string Name, bool Open);

/// <summary>The body of <c>POST /support/notice</c> (<c>05</c> MSG-API-010).</summary>
/// <param name="Version">The version of the notice acknowledged.</param>
internal sealed record NoticeRequest(string? Version);

/// <summary>The body of <c>POST /support/conversations</c> (<c>05</c> MSG-API-010).</summary>
/// <param name="Topic">The topic.</param>
/// <param name="Text">The first message's text.</param>
internal sealed record StartRequest(string? Topic, string? Text);

/// <summary>The body of <c>POST /support/offline</c> (<c>05</c> MSG-API-010).</summary>
/// <param name="Topic">The topic.</param>
/// <param name="Email">The address replies are sent to.</param>
/// <param name="Text">The message's text.</param>
internal sealed record OfflineRequest(string? Topic, string? Email, string? Text);

/// <summary>The answer of <c>GET /support/replies</c> (<c>05</c> MSG-API-009).</summary>
/// <param name="Replies">The replies, by name.</param>
internal sealed record RepliesBody(IReadOnlyList<ReplyBody> Replies);

/// <summary>A saved reply, <c>&lt;reply&gt;</c> of <c>05</c> MSG-API-009.</summary>
/// <param name="Id">The reply's identifier, lower-case and hyphenated.</param>
/// <param name="Name">Its name.</param>
/// <param name="Text">Its text.</param>
/// <param name="Topic">The topic that shares it, none for one's own.</param>
internal sealed record ReplyBody(string Id, string Name, string Text, string? Topic);

/// <summary>The body of <c>POST /support/replies</c> (<c>05</c> MSG-API-009).</summary>
/// <param name="Name">The name.</param>
/// <param name="Text">The text.</param>
/// <param name="Topic">The topic that shares it, none for one's own.</param>
internal sealed record ReplyRequest(string? Name, string? Text, string? Topic);

/// <summary>The body of <c>PUT /support/replies/{id}</c> (<c>05</c> MSG-API-009).</summary>
/// <param name="Name">The name.</param>
/// <param name="Text">The text.</param>
internal sealed record ReplyChange(string? Name, string? Text);

/// <summary>The body of <c>PUT /support/conversations/{id}/tags</c> (<c>05</c> MSG-API-009).</summary>
/// <param name="Tags">The tags, the whole set.</param>
internal sealed record TagsRequest(IReadOnlyList<string>? Tags);

/// <summary>The body of <c>POST /support/conversations/{id}/snooze</c> (<c>05</c> MSG-API-009).</summary>
/// <param name="Until">The instant it wakes.</param>
internal sealed record SnoozeRequest(string? Until);

/// <summary>The answer of <c>GET /support/live</c> (<c>05</c> MSG-API-009).</summary>
/// <param name="Conversations">The topic's open and snoozed conversations, oldest queued first.</param>
internal sealed record LivesBody(IReadOnlyList<LiveBody> Conversations);

/// <summary>A conversation of a topic's live view, <c>&lt;live&gt;</c> of <c>05</c> MSG-API-009.</summary>
/// <param name="Id">The conversation's identifier, lower-case and hyphenated.</param>
/// <param name="Status">Its status, <c>open</c> or <c>snoozed</c>.</param>
/// <param name="Assignee">The staff member assigned, a person reference, none while it is queued.</param>
/// <param name="Place">Its place in the queue, none once assigned.</param>
/// <param name="Waited">The whole minutes since it was queued.</param>
internal sealed record LiveBody(string Id, string Status, string? Assignee, int? Place, int Waited);

/// <summary>The answer of <c>GET /support/reports</c> (<c>05</c> MSG-API-009).</summary>
/// <param name="Topic">The topic's figures as a whole.</param>
/// <param name="Staff">Each staff member's figures.</param>
internal sealed record ReportBody(FiguresBody Topic, IReadOnlyList<FiguresBody> Staff);

/// <summary>A report's figures, <c>&lt;figures&gt;</c> of <c>05</c> MSG-API-009, the times in seconds and a figure with no data none.</summary>
/// <param name="Person">The staff member, none for the topic as a whole.</param>
/// <param name="Closed">The conversations closed within the period.</param>
/// <param name="WaitMedian">The median waiting time.</param>
/// <param name="WaitP90">The 90th percentile of the waiting time.</param>
/// <param name="HandleMedian">The median handling time.</param>
/// <param name="HandleP90">The 90th percentile of the handling time.</param>
/// <param name="Surveys">The surveys answered.</param>
/// <param name="ResolvedShare">The share of them that say resolved.</param>
/// <param name="MeanRating">Their mean rating.</param>
internal sealed record FiguresBody(string? Person, int Closed, double? WaitMedian, double? WaitP90, double? HandleMedian, double? HandleP90, int Surveys, decimal? ResolvedShare, decimal? MeanRating);