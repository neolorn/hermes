using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Hermes.Core;

/// <summary>
/// What a host asks to send (<c>01</c> MSG-SEND-003); it prints neither its recipient, nor its
/// destinations, nor its values, nor its idempotency key or topic.
/// </summary>
public sealed record Send
{
    /// <summary>A declared kind's name.</summary>
    public required string Kind { get; init; }

    /// <summary>Who the send is for.</summary>
    public required Recipient Recipient { get; init; }

    /// <summary>The email destination, an address of RFC 5321's mailbox form of at most 254 characters, or none.</summary>
    public string? Email { get; init; }

    /// <summary>The SMS destination, a number in E.164, or none.</summary>
    public string? Phone { get; init; }

    /// <summary>A BCP 47 tag matched to a declared language, or none for <c>Hermes:DefaultLanguage</c>.</summary>
    public string? Language { get; init; }

    /// <summary>An IANA time zone identifier, or none for <c>Hermes:TimeZone</c>.</summary>
    public string? TimeZone { get; init; }

    /// <summary>The templates' values, by name (<c>03</c> MSG-TPL-002).</summary>
    public required IReadOnlyDictionary<string, SendValue> Values { get; init; }

    /// <summary>The channels the send uses, a subset of the kind's; none for every channel it has a destination for.</summary>
    public IReadOnlyList<Channel>? Channels { get; init; }

    /// <summary>The instant before which nothing is dispatched, or none for now.</summary>
    public DateTimeOffset? NotBefore { get; init; }

    /// <summary>A key that records the send once, 1 to 128 printable ASCII characters, or none (<c>01</c> MSG-SEND-009).</summary>
    public string? IdempotencyKey { get; init; }

    /// <summary>A topic a push service replaces a pending message by, 1 to 128 characters, or none (<c>02</c> MSG-PUSH-002).</summary>
    public string? PushTopic { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        string channels = Channels is null ? string.Empty : string.Join(" ", Channels);
        builder.Append(CultureInfo.InvariantCulture, $"Kind = {Kind}, Language = {Language}, TimeZone = {TimeZone}, Channels = {channels}, NotBefore = {NotBefore:O}");
        return true;
    }
}