using System;
using System.Globalization;
using System.Text;

namespace Hermes.Core;

/// <summary>
/// One SMS a provider sends (<c>02</c> MSG-SMS-001 to MSG-SMS-004); it never prints its number,
/// its text, its delivery reference or its report address.
/// </summary>
public sealed record SmsMessage
{
    /// <summary>The destination number, in E.164.</summary>
    public required string ToNumber { get; init; }

    /// <summary>The sender name or number.</summary>
    public required string Sender { get; init; }

    /// <summary>The rendered text.</summary>
    public required string Text { get; init; }

    /// <summary>The attempt's delivery reference: 128 random bits, base64url, which a report may echo.</summary>
    public required string DeliveryReference { get; init; }

    /// <summary>The text's encoding (<c>02</c> MSG-SMS-001).</summary>
    public required SmsEncoding Encoding { get; init; }

    /// <summary>The segments the text takes, counted as the network counts them (<c>02</c> MSG-SMS-001).</summary>
    public required int Segments { get; init; }

    /// <summary>The address the provider's report of this message goes to, or none.</summary>
    public Uri? ReportAddress { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Sender = {Sender}, Encoding = {Encoding}, Segments = {Segments}");
        return true;
    }
}