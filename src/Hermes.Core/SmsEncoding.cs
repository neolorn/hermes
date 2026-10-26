using System.Text.Json.Serialization;

namespace Hermes.Core;

/// <summary>How an SMS text is encoded on the network (<c>02</c> MSG-SMS-001).</summary>
public enum SmsEncoding
{
    /// <summary>The GSM 7-bit default alphabet and its extension table, in septets (3GPP TS 23.038 section 6.2.1).</summary>
    [JsonStringEnumMemberName("gsm7")]
    Gsm7,

    /// <summary>UCS-2, in UTF-16 code units.</summary>
    [JsonStringEnumMemberName("ucs2")]
    Ucs2,
}