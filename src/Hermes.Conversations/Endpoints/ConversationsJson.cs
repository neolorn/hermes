using System.Text.Json.Serialization;

namespace Hermes.Conversations.Endpoints;

/// <summary>
/// The package's own serializer context for the conversations groups' bodies (<c>05</c>
/// MSG-API-001, MSG-API-009, MSG-API-010), under the options of the engine's: camelCase members read
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
[JsonSerializable(typeof(QueueBody))]
[JsonSerializable(typeof(AvailabilityRequest))]
[JsonSerializable(typeof(TransferRequest))]
[JsonSerializable(typeof(BlockRequest))]
[JsonSerializable(typeof(TopicsBody))]
[JsonSerializable(typeof(NoticeRequest))]
[JsonSerializable(typeof(StartRequest))]
[JsonSerializable(typeof(OfflineRequest))]
[JsonSerializable(typeof(PublicConversationBody))]
[JsonSerializable(typeof(RepliesBody))]
[JsonSerializable(typeof(ReplyBody))]
[JsonSerializable(typeof(ReplyRequest))]
[JsonSerializable(typeof(ReplyChange))]
[JsonSerializable(typeof(TagsRequest))]
[JsonSerializable(typeof(SnoozeRequest))]
[JsonSerializable(typeof(LivesBody))]
[JsonSerializable(typeof(ReportBody))]
internal sealed partial class ConversationsJson : JsonSerializerContext;