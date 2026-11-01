using System.Text.Json.Serialization;

namespace Hermes.Core;

/// <summary>A conversation's type (<c>09</c> MSG-CONV-001; <c>08</c> section 2).</summary>
public enum ConversationType
{
    /// <summary>A support conversation, between a visitor or customer and the support desk.</summary>
    [JsonStringEnumMemberName("support")]
    Support,

    /// <summary>A group conversation, one per scope of the membership seam.</summary>
    [JsonStringEnumMemberName("group")]
    Group,

    /// <summary>A direct conversation, between the participants named.</summary>
    [JsonStringEnumMemberName("direct")]
    Direct,
}