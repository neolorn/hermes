using System.Text.Json.Serialization;

namespace Hermes.Core;

/// <summary>A channel a delivery goes out on.</summary>
public enum Channel
{
    /// <summary>Email, through SMTP submission.</summary>
    [JsonStringEnumMemberName("email")]
    Email,

    /// <summary>SMS, through the SMS provider.</summary>
    [JsonStringEnumMemberName("sms")]
    Sms,

    /// <summary>Browser push, through Web Push.</summary>
    [JsonStringEnumMemberName("push")]
    Push,
}