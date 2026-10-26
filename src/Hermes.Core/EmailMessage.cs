using System;
using System.Globalization;
using System.Text;

namespace Hermes.Core;

/// <summary>
/// One email a provider submits (<c>02</c> MSG-EMAIL-002, MSG-EMAIL-003); it never prints its
/// destination, its content or its unsubscribe address.
/// </summary>
public sealed record EmailMessage
{
    /// <summary>The sender, which is the envelope sender too.</summary>
    public required EmailAddress From { get; init; }

    /// <summary>The address replies go to, or none.</summary>
    public EmailAddress? ReplyTo { get; init; }

    /// <summary>The destination address.</summary>
    public required string ToAddress { get; init; }

    /// <summary>The rendered subject.</summary>
    public required string Subject { get; init; }

    /// <summary>The rendered plain-text part.</summary>
    public required string Text { get; init; }

    /// <summary>The rendered HTML part, the plain text's alternative, or none.</summary>
    public string? Html { get; init; }

    /// <summary>The message's <c>Message-ID</c>: the delivery's identifier at the From address's domain.</summary>
    public required string MessageId { get; init; }

    /// <summary>The one-click unsubscribe address, or none for an essential category's email (<c>02</c> MSG-EMAIL-006).</summary>
    public Uri? Unsubscribe { get; init; }

    /// <summary>The send's language, a BCP 47 tag.</summary>
    public required string Language { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"MessageId = {MessageId}, Language = {Language}");
        return true;
    }
}