using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using Hermes.Core;

namespace Hermes.Development;

/// <summary>A captured email (<c>02</c> MSG-DEV-003).</summary>
public sealed record InboxEmail
{
    /// <summary>The email's sender.</summary>
    public required EmailAddress From { get; init; }

    /// <summary>The address replies go to, none where the email names none.</summary>
    public EmailAddress? ReplyTo { get; init; }

    /// <summary>The email's subject.</summary>
    public required string Subject { get; init; }

    /// <summary>The email's plain-text part.</summary>
    public required string Text { get; init; }

    /// <summary>The email's HTML part, none where it has none.</summary>
    public string? Html { get; init; }

    /// <summary>The email's <c>Message-ID</c>.</summary>
    public required string MessageId { get; init; }

    /// <summary>The email's unsubscribe address, none where it carries none.</summary>
    public Uri? Unsubscribe { get; init; }

    /// <summary>The email's headers, by name and value, in their order in the <c>.eml</c>.</summary>
    public required IReadOnlyList<KeyValuePair<string, string>> Headers { get; init; }

    /// <summary>The path of the email's <c>.eml</c>, beside its record.</summary>
    public required string EmlPath { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"MessageId = {MessageId}");
        return true;
    }
}