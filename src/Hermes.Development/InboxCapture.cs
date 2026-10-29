using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Channels;
using Hermes.Core;

using MimeKit;

namespace Hermes.Development;

/// <summary>
/// Turns each message a provider of the inbox is handed into its record (<c>02</c>
/// MSG-DEV-003) and writes it (MSG-DEV-002), answering <c>Accepted(id)</c>, <c>id</c> the
/// record's; no outside call is made.
/// </summary>
internal sealed class InboxCapture(InboxWriter writer, string app)
{
    /// <summary>The number of characters of an SMS's text its summary holds.</summary>
    private const int SummaryCharacters = 100;

    /// <summary>Keeps <paramref name="message"/>, and the MIME message the SMTP provider would submit for it as its <c>.eml</c>.</summary>
    internal async ValueTask<Attempt> EmailAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        DispatchedDelivery? delivery = DispatchedDelivery.Current;
        (string id, DateTimeOffset at) = writer.Next();
        using MimeMessage mime = EmailMime.Build(message, at);
        EmailBody email = new(
            Address(message.From),
            message.ReplyTo is { } replyTo ? Address(replyTo) : null,
            message.Subject,
            message.Text,
            message.Html,
            message.MessageId,
            message.Unsubscribe?.AbsoluteUri,
            [.. Headers(mime)]);
        MessageBody body = Body(id, at, Channel.Email, message.ToAddress, delivery, delivery?.Language ?? message.Language, message.Subject, message.Text, InboxLinks.Of(message.Html, message.Text, null)) with
        {
            Email = email,
        };
        await writer.WriteAsync(body, at, mime, cancellationToken).ConfigureAwait(false);
        return new Attempt.Accepted(id);
    }

    /// <summary>Keeps <paramref name="message"/>.</summary>
    internal async ValueTask<Attempt> SmsAsync(SmsMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        DispatchedDelivery? delivery = DispatchedDelivery.Current;
        (string id, DateTimeOffset at) = writer.Next();
        MessageBody body = Body(id, at, Channel.Sms, message.ToNumber, delivery, delivery?.Language, Summary(message.Text), message.Text, InboxLinks.Of(null, message.Text, null)) with
        {
            Sms = new SmsBody(message.Sender, message.Text, message.Encoding, message.Segments, message.DeliveryReference),
        };
        await writer.WriteAsync(body, at, null, cancellationToken).ConfigureAwait(false);
        return new Attempt.Accepted(id);
    }

    /// <summary>Keeps <paramref name="request"/>, naming its push service in place of its endpoint.</summary>
    internal async ValueTask<Attempt> PushAsync(PushRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request is not PushRequest.WebPush push)
        {
            return new Attempt.Permanent(AttemptReason.BadRequest, null);
        }

        DispatchedDelivery? delivery = DispatchedDelivery.Current;
        (string id, DateTimeOffset at) = writer.Next();
        NotificationBody notification = Notification(push.Payload.Span);
        PushService service = Uri.TryCreate(push.Endpoint, UriKind.Absolute, out Uri? endpoint) ? PushServiceRule.Of(endpoint.IdnHost) : PushService.Other;
        MessageBody body = Body(id, at, Channel.Push, null, delivery, delivery?.Language, notification.Title, notification.Body, InboxLinks.Of(null, null, notification.Url)) with
        {
            Push = new PushBody(service, notification, (long)push.Ttl.TotalSeconds, push.Urgency, push.Topic),
        };
        await writer.WriteAsync(body, at, null, cancellationToken).ConfigureAwait(false);
        return new Attempt.Accepted(id);
    }

    private MessageBody Body(string id, DateTimeOffset at, Channel channel, string? to, DispatchedDelivery? delivery, string? language, string summary, string text, IReadOnlyList<string> links)
    {
        return new MessageBody(id, channel, app, InboxFiles.Written(at).Instant, to, delivery?.Person, delivery?.Kind, delivery?.Category, language, summary, text, links, null, null, null);
    }

    private static AddressBody Address(EmailAddress address)
    {
        return new AddressBody(address.Address, address.DisplayName);
    }

    /// <summary>The headers of <paramref name="mime"/> in their order in its <c>.eml</c>: the message's, then its body's.</summary>
    private static IEnumerable<HeaderBody> Headers(MimeMessage mime)
    {
        IEnumerable<Header> headers = mime.Body is { } body ? mime.Headers.Concat(body.Headers) : mime.Headers;
        return headers.Select(header => new HeaderBody(header.Field, header.Value));
    }

    /// <summary>The first 100 characters of <paramref name="text"/>, counted as <c>00</c> section 1.5 counts them.</summary>
    private static string Summary(string text)
    {
        StringBuilder summary = new();
        foreach (Rune rune in text.EnumerateRunes().Take(SummaryCharacters))
        {
            summary.Append(rune.ToString());
        }

        return summary.ToString();
    }

    /// <summary>The notification <paramref name="payload"/> holds (<c>02</c> MSG-PUSH-002); an empty one where it does not read.</summary>
    private static NotificationBody Notification(ReadOnlySpan<byte> payload)
    {
        try
        {
            Utf8JsonReader reader = new(payload);
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            JsonElement root = document.RootElement;
            return new NotificationBody(
                Member(root, "title") ?? string.Empty,
                Member(root, "body") ?? string.Empty,
                Member(root, "url"),
                Member(root, "tag"),
                Member(root, "lang"),
                Member(root, "dir"),
                Member(root, "icon"),
                Member(root, "badge"));
        }
        catch (JsonException)
        {
            return new NotificationBody(string.Empty, string.Empty, null, null, null, null, null, null);
        }
    }

    private static string? Member(JsonElement root, string name)
    {
        return root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }
}