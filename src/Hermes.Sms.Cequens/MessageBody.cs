namespace Hermes.Sms.Cequens;

/// <summary>The body of <c>POST /sms/v1/messages</c>, its members and nothing else (<c>02</c> MSG-SMS-007).</summary>
/// <param name="MessageText">The text.</param>
/// <param name="SenderName">The sender.</param>
/// <param name="MessageType"><c>text</c> for a GSM 7-bit text, <c>unicode</c> for a UCS-2 one.</param>
/// <param name="Recipients">The number without its <c>+</c>.</param>
/// <param name="DlrUrl">The message's report address, or none, which is left out.</param>
internal sealed record MessageBody(string MessageText, string SenderName, string MessageType, string Recipients, string? DlrUrl);