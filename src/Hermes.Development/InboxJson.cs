using System.Text.Json.Serialization;

namespace Hermes.Development;

/// <summary>
/// The inbox's own serializer context, for its records and its group's answers (<c>05</c>
/// MSG-API-007): camelCase members, the enumerations by their <c>08</c> spellings, every
/// member written, a <c>null</c> one included.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(MessageBody))]
[JsonSerializable(typeof(ListBody))]
[JsonSerializable(typeof(InboxError))]
internal sealed partial class InboxJson : JsonSerializerContext;