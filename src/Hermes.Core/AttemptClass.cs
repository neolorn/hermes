using System.Text.Json.Serialization;

namespace Hermes.Core;

/// <summary>The class of a provider's answer (<c>08</c> section 2; <c>02</c> MSG-CHAN-001).</summary>
public enum AttemptClass
{
    /// <summary>The provider took the message.</summary>
    [JsonStringEnumMemberName("accepted")]
    Accepted,

    /// <summary>The provider did not take the message, and a later attempt may.</summary>
    [JsonStringEnumMemberName("transient")]
    Transient,

    /// <summary>The provider refused the message for good.</summary>
    [JsonStringEnumMemberName("permanent")]
    Permanent,

    /// <summary>The push subscription no longer exists.</summary>
    [JsonStringEnumMemberName("gone")]
    Gone,
}