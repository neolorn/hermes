using System.Text.Json.Serialization;

namespace Hermes.Core;

/// <summary>The platform a push provider serves and a push request goes to (<c>02</c> MSG-PUSH-008).</summary>
public enum PushPlatform
{
    /// <summary>Browser push, through Web Push (RFC 8030, RFC 8291, RFC 8292).</summary>
    [JsonStringEnumMemberName("webpush")]
    WebPush,
}