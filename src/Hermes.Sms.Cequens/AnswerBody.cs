namespace Hermes.Sms.Cequens;

/// <summary>The gateway's answer to a send or a balance, the members the provider reads (<c>02</c> MSG-SMS-007).</summary>
/// <param name="ReplyCode">The gateway's reply code, <c>0</c> where the request was handled.</param>
/// <param name="Data">What a handled request answers.</param>
/// <param name="Error">What a refused request answers.</param>
internal sealed record AnswerBody(int? ReplyCode, AnswerData? Data, AnswerError? Error);