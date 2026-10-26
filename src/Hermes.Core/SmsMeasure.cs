namespace Hermes.Core;

/// <summary>How an SMS text goes out: its encoding and the segments it takes (<c>02</c> MSG-SMS-001).</summary>
public sealed record SmsMeasure
{
    /// <summary>The text's encoding.</summary>
    public required SmsEncoding Encoding { get; init; }

    /// <summary>The segments the text takes, at least one.</summary>
    public required int Segments { get; init; }
}