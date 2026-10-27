using System.Text.Json.Serialization;

namespace Hermes.Endpoints;

/// <summary>
/// The library's own serializer context for its endpoints' bodies (<c>05</c> MSG-API-001):
/// camelCase members read case-sensitively, an unknown member ignored, numbers only as JSON
/// numbers, under its own options and never the application's.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ErrorBody))]
[JsonSerializable(typeof(PreferencesBody))]
[JsonSerializable(typeof(PreferenceChange))]
[JsonSerializable(typeof(PublicKeyBody))]
[JsonSerializable(typeof(SubscriptionBody))]
[JsonSerializable(typeof(RemovalBody))]
internal sealed partial class EndpointJson : JsonSerializerContext;