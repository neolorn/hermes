using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Hermes.Core;

/// <summary>
/// A conversation's message as a read answers it (<c>09</c> MSG-CONV-001, MSG-CONV-006,
/// MSG-CONV-009; <c>05</c> MSG-API-009). It prints neither its author, its text nor its
/// mentions.
/// </summary>
public sealed record MessageRecord
{
    /// <summary>The message's identifier.</summary>
    public required MessageId Message { get; init; }

    /// <summary>Who wrote it: a person, a visitor, or the library for an <c>automatic</c> or <c>system</c> message.</summary>
    public required AuthorKind AuthorKind { get; init; }

    /// <summary>The author's person reference where <see cref="AuthorKind"/> is <see cref="Core.AuthorKind.Person"/>; none otherwise and once the author is erased.</summary>
    public string? Author { get; init; }

    /// <summary>The message's type.</summary>
    public required MessageType Type { get; init; }

    /// <summary>The text, none once the message is deleted or its author erased.</summary>
    public string? Text { get; init; }

    /// <summary>The message it replies to, or none.</summary>
    public MessageId? ReplyTo { get; init; }

    /// <summary>The persons it mentions.</summary>
    public required IReadOnlyList<string> Mentions { get; init; }

    /// <summary>The instant it was sent.</summary>
    public required DateTimeOffset SentAt { get; init; }

    /// <summary>The instant it was last edited, or none.</summary>
    public DateTimeOffset? EditedAt { get; init; }

    /// <summary>The instant it was deleted, or none.</summary>
    public DateTimeOffset? DeletedAt { get; init; }

    /// <summary>Whether its text was cleared by its author's erasure.</summary>
    public required bool Erased { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(
            CultureInfo.InvariantCulture,
            $"Message = {Message.Value}, AuthorKind = {AuthorKind}, Type = {Type}, ReplyTo = {ReplyTo?.Value}, SentAt = {SentAt:O}, EditedAt = {EditedAt:O}, DeletedAt = {DeletedAt:O}, Erased = {Erased}");
        return true;
    }
}