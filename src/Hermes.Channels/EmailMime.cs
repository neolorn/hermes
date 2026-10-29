using System;
using System.Text;

using Hermes.Core;

using MimeKit;
using MimeKit.Text;

namespace Hermes.Channels;

/// <summary>
/// The MIME message of an <see cref="EmailMessage"/> (<c>02</c> MSG-EMAIL-003), which the
/// SMTP provider submits and the development inbox keeps (MSG-DEV-001), so both are built by
/// this one code.
/// </summary>
internal static class EmailMime
{
    /// <summary>The MIME message of <paramref name="message"/>, dated <paramref name="date"/>.</summary>
    internal static MimeMessage Build(EmailMessage message, DateTimeOffset date)
    {
        MimeMessage mime = new();
        mime.From.Add(Mailbox(message.From));
        if (message.ReplyTo is { } replyTo)
        {
            mime.ReplyTo.Add(Mailbox(replyTo));
        }

        mime.To.Add(new MailboxAddress(string.Empty, message.ToAddress));
        mime.Subject = message.Subject;
        mime.MessageId = message.MessageId;
        mime.Date = date;
        mime.Headers.Add("Auto-Submitted", "auto-generated");
        if (message.Unsubscribe is { } unsubscribe)
        {
            // RFC 8058: the address in angle brackets, and the one-click POST it takes.
            mime.Headers.Add("List-Unsubscribe", "<" + unsubscribe.AbsoluteUri + ">");
            mime.Headers.Add("List-Unsubscribe-Post", "List-Unsubscribe=One-Click");
        }

        TextPart text = Part(TextFormat.Plain, message.Text);
        mime.Body = message.Html is { } html ? new MultipartAlternative { text, Part(TextFormat.Html, html) } : text;
        return mime;
    }

    /// <summary>The mailbox of <paramref name="address"/>, its display name included.</summary>
    internal static MailboxAddress Mailbox(EmailAddress address)
    {
        return new MailboxAddress(address.DisplayName ?? string.Empty, address.Address);
    }

    private static TextPart Part(TextFormat format, string text)
    {
        TextPart part = new(format);
        part.SetText(Encoding.UTF8, text);
        return part;
    }
}