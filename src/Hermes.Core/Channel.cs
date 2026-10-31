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

    /// <summary>The in-application list: an entry written to the person's list, in the library's own store.</summary>
    [JsonStringEnumMemberName("inapp")]
    InApp,
}