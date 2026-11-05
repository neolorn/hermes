using System.Text.Json.Serialization;

namespace Hermes.Sms.Cequens;

/// <summary>The account a balance answers.</summary>
/// <param name="Credit">The account's credit, in its currency.</param>
internal sealed record AnswerAccount([property: JsonPropertyName("Credit")] decimal? Credit);