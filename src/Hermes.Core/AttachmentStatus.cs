using System.Text.Json.Serialization;

namespace Hermes.Core;

/// <summary>An attachment's status (<c>09</c> MSG-CONV-007; <c>08</c> section 2).</summary>
public enum AttachmentStatus
{
    /// <summary>Not yet scanned; it does not open.</summary>
    [JsonStringEnumMemberName("pending")]
    Pending,

    /// <summary>Scanned clean; it opens.</summary>
    [JsonStringEnumMemberName("clean")]
    Clean,

    /// <summary>Found infected; its bytes are deleted.</summary>
    [JsonStringEnumMemberName("infected")]
    Infected,

    /// <summary>Its scan failed five times.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,
}