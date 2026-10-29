using System.Collections.Generic;

using Hermes.Core;

namespace Hermes.Development;

/// <summary>
/// A captured message as its record and <c>GET /messages/{id}</c> hold it (<c>05</c>
/// MSG-API-007; <c>02</c> MSG-DEV-002): <see cref="At"/> in ISO 8601 UTC with milliseconds,
/// every member a message lacks <c>null</c>.
/// </summary>
internal sealed record MessageBody(
    string Id,
    Channel Channel,
    string App,
    string At,
    string? To,
    string? Person,
    string? Kind,
    string? Category,
    string? Language,
    string Summary,
    string Text,
    IReadOnlyList<string> Links,
    EmailBody? Email,
    SmsBody? Sms,
    PushBody? Push);

/// <summary>A message as <c>GET /messages</c> lists it (<c>05</c> MSG-API-007).</summary>
internal sealed record SummaryBody(
    string Id,
    Channel Channel,
    string App,
    string At,
    string? To,
    string? Person,
    string? Kind,
    string? Category,
    string? Language,
    string Summary);

/// <summary>The answer of <c>GET /messages</c>.</summary>
internal sealed record ListBody(IReadOnlyList<SummaryBody> Messages);

/// <summary>A captured email's members.</summary>
internal sealed record EmailBody(
    AddressBody From,
    AddressBody? ReplyTo,
    string Subject,
    string Text,
    string? Html,
    string MessageId,
    string? Unsubscribe,
    IReadOnlyList<HeaderBody> Headers);

/// <summary>An email address and its display name.</summary>
internal sealed record AddressBody(string Address, string? DisplayName);

/// <summary>One header of an email's <c>.eml</c>.</summary>
internal sealed record HeaderBody(string Name, string Value);

/// <summary>A captured SMS's members.</summary>
internal sealed record SmsBody(string Sender, string Text, SmsEncoding Encoding, int Segments, string DeliveryReference);

/// <summary>A captured push's members, <see cref="Ttl"/> in whole seconds.</summary>
internal sealed record PushBody(PushService Service, NotificationBody Notification, long Ttl, PushUrgency Urgency, string? Topic);

/// <summary>The notification a push's payload holds.</summary>
internal sealed record NotificationBody(string Title, string Body, string? Url, string? Tag, string? Lang, string? Dir, string? Icon, string? Badge);

/// <summary>A refusal of the inbox's group, as every group of <c>05</c> answers one.</summary>
internal sealed record InboxError(string Code, IReadOnlyDictionary<string, string> Details);