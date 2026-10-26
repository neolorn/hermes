namespace Hermes.Sending;

/// <summary>A kind's SMS options (<c>02</c> MSG-SMS-001, MSG-SMS-002).</summary>
/// <param name="MaxSegments">The most segments one of its messages may take.</param>
/// <param name="Sender">The sender, or none for <c>Hermes:Sms:Sender</c>.</param>
internal sealed record KindSms(int MaxSegments, string? Sender);