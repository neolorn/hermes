using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Hermes.Sms.Cequens;

/// <summary>The <c>data</c> of the gateway's answer.</summary>
/// <param name="SentSmsIds">A send's messages, each with its identifier.</param>
/// <param name="InvalidRecipients">The send's numbers the gateway refused, as text, empty where it refused none.</param>
/// <param name="Account">The balance's account.</param>
internal sealed record AnswerData(
    [property: JsonPropertyName("SentSMSIDs")] IReadOnlyList<SentMessage?>? SentSmsIds,
    [property: JsonPropertyName("InvalidRecipients")] string? InvalidRecipients,
    [property: JsonPropertyName("Account")] AnswerAccount? Account);