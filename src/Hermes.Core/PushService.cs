using System.Text.Json.Serialization;

namespace Hermes.Core;

/// <summary>A push service, read from a subscription's endpoint's host (<c>08</c> section 2).</summary>
public enum PushService
{
    /// <summary><c>fcm.googleapis.com</c>.</summary>
    [JsonStringEnumMemberName("fcm")]
    Fcm,

    /// <summary><c>*.push.services.mozilla.com</c>.</summary>
    [JsonStringEnumMemberName("mozilla")]
    Mozilla,

    /// <summary><c>*.push.apple.com</c>.</summary>
    [JsonStringEnumMemberName("apple")]
    Apple,

    /// <summary><c>*.notify.windows.com</c>.</summary>
    [JsonStringEnumMemberName("windows")]
    Windows,

    /// <summary>Any other allowed host.</summary>
    [JsonStringEnumMemberName("other")]
    Other,
}