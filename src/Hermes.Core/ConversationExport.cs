using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Hermes.Core;

/// <summary>
/// A conversation of a person's export (<c>01</c> MSG-SEND-020; <c>09</c> MSG-CONV-010): a
/// support conversation of theirs as the customer, with every message but notes, or a staff
/// chat they wrote in, with their own messages only. It prints neither text nor topic.
/// </summary>
public sealed record ConversationExport
{
    /// <summary>The conversation.</summary>
    public required ConversationId Conversation { get; init; }

    /// <summary>Its type.</summary>
    public required ConversationType Type { get; init; }

    /// <summary>A support conversation's topic, none for a staff chat.</summary>
    public string? Topic { get; init; }

    /// <summary>The instant it was opened.</summary>
    public required DateTimeOffset OpenedAt { get; init; }

    /// <summary>The instant a support conversation closed, none while it is open and for a staff chat.</summary>
    public DateTimeOffset? ClosedAt { get; init; }

    /// <summary>Its messages as the export holds them, oldest first.</summary>
    public required IReadOnlyList<ConversationMessageExport> Messages { get; init; }

    /// <summary>The person's survey answer, none where they gave none.</summary>
    public SurveyExport? Survey { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Conversation = {Conversation.Value}, Type = {Type}, OpenedAt = {OpenedAt:O}, ClosedAt = {ClosedAt:O}, Messages = {Messages.Count}");
        return true;
    }
}