using System.Text.Json.Serialization;

namespace Hermes.Core;

/// <summary>A push message's urgency (RFC 8030 section 5.3).</summary>
public enum PushUrgency
{
    /// <summary>Delivered only on power and Wi-Fi.</summary>
    [JsonStringEnumMemberName("very-low")]
    VeryLow,

    /// <summary>Delivered on power or Wi-Fi.</summary>
    [JsonStringEnumMemberName("low")]
    Low,

    /// <summary>Delivered unless the device's battery is low.</summary>
    [JsonStringEnumMemberName("normal")]
    Normal,

    /// <summary>Delivered at once, whatever the device's battery.</summary>
    [JsonStringEnumMemberName("high")]
    High,
}