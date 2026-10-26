using System.Text.Json.Serialization;

namespace Hermes.Core;

/// <summary>What a delivery report says of its message (<c>02</c> MSG-SMS-004).</summary>
public enum SmsReportOutcome
{
    /// <summary>The message reached the phone.</summary>
    [JsonStringEnumMemberName("delivered")]
    Delivered,

    /// <summary>The message will not reach the phone.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,

    /// <summary>An intermediate state; nothing changes.</summary>
    [JsonStringEnumMemberName("notfinal")]
    NotFinal,
}