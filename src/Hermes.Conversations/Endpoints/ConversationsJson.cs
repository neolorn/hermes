using System.Text.Json.Serialization;

namespace Hermes.Conversations.Endpoints;

/// <summary>
/// The package's own serializer context for the staff conversations group's bodies (<c>05</c>
/// MSG-API-001, MSG-API-009), under the options of the engine's: camelCase members read
/// case-sensitively, an unknown member ignored, numbers only as JSON numbers, never the
/// application's.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ConversationsBody))]
[JsonSerializable(typeof(ConversationBody))]
[JsonSerializable(typeof(MessagesBody))]
[JsonSerializable(typeof(MessageBody))]
[JsonSerializable(typeof(ParticipantsRequest))]
[JsonSerializable(typeof(MessageRequest))]
[JsonSerializable(typeof(EditRequest))]
[JsonSerializable(typeof(ReadRequest))]
[JsonSerializable(typeof(MuteRequest))]
internal sealed partial class ConversationsJson : JsonSerializerContext;