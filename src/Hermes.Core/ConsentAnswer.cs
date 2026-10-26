using System.Text.Json.Serialization;

namespace Hermes.Core;

/// <summary>A consent source's answer (<c>01</c> MSG-SEND-011).</summary>
public enum ConsentAnswer
{
    /// <summary>The recipient consents.</summary>
    [JsonStringEnumMemberName("given")]
    Given,

    /// <summary>The recipient does not consent.</summary>
    [JsonStringEnumMemberName("notgiven")]
    NotGiven,

    /// <summary>The source cannot answer now.</summary>
    [JsonStringEnumMemberName("unavailable")]
    Unavailable,
}