using System.Text.Json.Serialization;

namespace Hermes.Core;

/// <summary>A message's type, an open enumeration to which a later release may add (<c>09</c> MSG-CONV-001, MSG-CONV-012; <c>08</c> section 2).</summary>
public enum MessageType
{
    /// <summary>A text a person or a visitor writes.</summary>
    [JsonStringEnumMemberName("text")]
    Text,

    /// <summary>A note the support desk alone reads.</summary>
    [JsonStringEnumMemberName("note")]
    Note,

    /// <summary>A message the library writes automatically.</summary>
    [JsonStringEnumMemberName("automatic")]
    Automatic,

    /// <summary>A message the library writes of what happened in the conversation.</summary>
    [JsonStringEnumMemberName("system")]
    System,
}