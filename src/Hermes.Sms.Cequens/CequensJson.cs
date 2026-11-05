using System.Text.Json.Serialization;

namespace Hermes.Sms.Cequens;

/// <summary>
/// The package's own serializer context for the gateway's bodies (<c>02</c> MSG-SMS-007):
/// camelCase members, the gateway's PascalCase ones named on their members, read
/// case-sensitively, an unknown member ignored, a <c>null</c> member left out of a request.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(MessageBody))]
[JsonSerializable(typeof(AnswerBody))]
internal sealed partial class CequensJson : JsonSerializerContext;