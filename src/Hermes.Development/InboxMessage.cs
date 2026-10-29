using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using Hermes.Core;

namespace Hermes.Development;

/// <summary>
/// One message the development inbox captured, as its person would receive it (<c>02</c>
/// MSG-DEV-003; <c>05</c> MSG-API-007); of <see cref="Email"/>, <see cref="Sms"/> and
/// <see cref="Push"/>, the one of its channel alone is set.
/// </summary>
public sealed record InboxMessage
{
    /// <summary>The record's 32 lower-case hexadecimal digits.</summary>
    public required string Id { get; init; }

    /// <summary>The message's channel.</summary>
    public required Channel Channel { get; init; }

    /// <summary>The name of the application that sent the message.</summary>
    public required string App { get; init; }

    /// <summary>The instant the message was captured.</summary>
    public required DateTimeOffset At { get; init; }

    /// <summary>The email address or the phone number; none for a push.</summary>
    public string? To { get; init; }

    /// <summary>The person the engine's delivery named; none for a message handed to the providers directly.</summary>
    public string? Person { get; init; }

    /// <summary>The engine's delivery's kind; none for a message handed to the providers directly.</summary>
    public string? Kind { get; init; }

    /// <summary>The engine's delivery's category; none for a message handed to the providers directly.</summary>
    public string? Category { get; init; }

    /// <summary>The engine's delivery's language, or a directly handed email's.</summary>
    public string? Language { get; init; }

    /// <summary>An email's subject, an SMS's first 100 characters or a push's title.</summary>
    public required string Summary { get; init; }

    /// <summary>An email's plain text, an SMS's text or a push's body.</summary>
    public required string Text { get; init; }

    /// <summary>Every absolute <c>http</c> and <c>https</c> address the message carries, each once, in the order met.</summary>
    public required IReadOnlyList<string> Links { get; init; }

    /// <summary>The email, for a message of the email channel.</summary>
    public InboxEmail? Email { get; init; }

    /// <summary>The SMS, for a message of the SMS channel.</summary>
    public InboxSms? Sms { get; init; }

    /// <summary>The push, for a message of the push channel.</summary>
    public InboxPush? Push { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Id = {Id}, Channel = {Channel}");
        return true;
    }
}