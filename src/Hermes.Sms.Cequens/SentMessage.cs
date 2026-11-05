using System.Text.Json.Serialization;

namespace Hermes.Sms.Cequens;

/// <summary>One message a send made.</summary>
/// <param name="SmsId">The gateway's identifier of the message, which its report names as <c>msgid</c>.</param>
internal sealed record SentMessage([property: JsonPropertyName("SMSId")] string? SmsId);