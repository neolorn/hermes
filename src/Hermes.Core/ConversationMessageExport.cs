using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Hermes.Core;

/// <summary>
/// A message of a conversation in a person's export (<c>09</c> MSG-CONV-010): its instant, who
/// wrote it, never naming another person, its text and its attachments. It prints no text.
/// </summary>
public sealed record ConversationMessageExport
{
    /// <summary>The instant it was sent.</summary>
    public required DateTimeOffset SentAt { get; init; }

    /// <summary>Who wrote it: <c>person</c> for the person's own, <c>staff</c> for a staff member's, <c>library</c> for an automatic or system message.</summary>
    public required string Author { get; init; }

    /// <summary>Its text, none once deleted, erased or rejected, and for a message without text.</summary>
    public string? Text { get; init; }

    /// <summary>Whether its text's key was erased, so it carries no text.</summary>
    public required bool Erased { get; init; }

    /// <summary>Whether its text did not open under its binding, so it carries no text.</summary>
    public required bool Rejected { get; init; }

    /// <summary>The attachments it names, without their bytes.</summary>
    public required IReadOnlyList<AttachmentExport> Attachments { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"SentAt = {SentAt:O}, Author = {Author}, Erased = {Erased}, Rejected = {Rejected}, Attachments = {Attachments.Count}");
        return true;
    }
}