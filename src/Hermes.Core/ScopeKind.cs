using System.Text.Json.Serialization;

namespace Hermes.Core;

/// <summary>The kind of a scope of the membership seam (<c>09</c> MSG-CONV-006; <c>08</c> section 2).</summary>
public enum ScopeKind
{
    /// <summary>An organization.</summary>
    [JsonStringEnumMemberName("organization")]
    Organization,

    /// <summary>A group.</summary>
    [JsonStringEnumMemberName("group")]
    Group,
}