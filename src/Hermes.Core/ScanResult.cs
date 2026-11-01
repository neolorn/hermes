using System.Text.Json.Serialization;

namespace Hermes.Core;

/// <summary>What the scanning seam answers of an attachment (<c>09</c> MSG-CONV-007; <c>08</c> section 2).</summary>
public enum ScanResult
{
    /// <summary>Nothing harmful was found.</summary>
    [JsonStringEnumMemberName("clean")]
    Clean,

    /// <summary>Something harmful was found.</summary>
    [JsonStringEnumMemberName("infected")]
    Infected,

    /// <summary>The scan did not finish; the library tries again.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,
}