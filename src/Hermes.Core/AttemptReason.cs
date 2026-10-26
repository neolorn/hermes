using System.Text.Json.Serialization;

namespace Hermes.Core;

/// <summary>Why an attempt answered as it did, or why a delivery ended (<c>08</c> section 6).</summary>
public enum AttemptReason
{
    /// <summary>No answer within <c>Dispatch:Timeout</c>.</summary>
    [JsonStringEnumMemberName("timeout")]
    Timeout,

    /// <summary>The connection was refused or broke, or a reply did not read.</summary>
    [JsonStringEnumMemberName("connectionfailed")]
    ConnectionFailed,

    /// <summary>TLS was not completed, or the certificate did not validate.</summary>
    [JsonStringEnumMemberName("tlsfailed")]
    TlsFailed,

    /// <summary>The provider refused the credential, or offered no way to give it.</summary>
    [JsonStringEnumMemberName("authfailed")]
    AuthFailed,

    /// <summary>The provider refused the sender.</summary>
    [JsonStringEnumMemberName("senderrefused")]
    SenderRefused,

    /// <summary>The provider refused the destination.</summary>
    [JsonStringEnumMemberName("recipientrefused")]
    RecipientRefused,

    /// <summary>The provider refused the message.</summary>
    [JsonStringEnumMemberName("messagerefused")]
    MessageRefused,

    /// <summary>A push service answered 400, or a status no other reason names.</summary>
    [JsonStringEnumMemberName("badrequest")]
    BadRequest,

    /// <summary>The provider answered 429.</summary>
    [JsonStringEnumMemberName("ratelimited")]
    RateLimited,

    /// <summary>The provider's temporary answer.</summary>
    [JsonStringEnumMemberName("temporary")]
    Temporary,

    /// <summary>An SMS provider's final answer with no more specific reason.</summary>
    [JsonStringEnumMemberName("permanent")]
    Permanent,

    /// <summary>The push subscription no longer exists.</summary>
    [JsonStringEnumMemberName("gone")]
    Gone,

    /// <summary>The provider threw.</summary>
    [JsonStringEnumMemberName("providerfault")]
    ProviderFault,

    /// <summary>A key, protector or envelope format this process does not hold was needed.</summary>
    [JsonStringEnumMemberName("keyunavailable")]
    KeyUnavailable,

    /// <summary>The consent source answered unavailable, did not answer in time, or threw.</summary>
    [JsonStringEnumMemberName("consentunavailable")]
    ConsentUnavailable,

    /// <summary>A provider's report showed the message failed.</summary>
    [JsonStringEnumMemberName("reportedfailed")]
    ReportedFailed,

    /// <summary>The holder of the attempt stopped before recording its answer.</summary>
    [JsonStringEnumMemberName("claimlost")]
    ClaimLost,

    /// <summary>The SMTP server refused the session.</summary>
    [JsonStringEnumMemberName("serverrefused")]
    ServerRefused,

    /// <summary>The number is no longer within <c>Sms:AllowedPrefixes</c> when the delivery is checked.</summary>
    [JsonStringEnumMemberName("destinationnotallowed")]
    DestinationNotAllowed,

    /// <summary>A protected value did not open under its binding, or its envelope's format is unknown.</summary>
    [JsonStringEnumMemberName("valuerejected")]
    ValueRejected,
}