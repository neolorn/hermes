using System;

namespace Hermes.Sending;

/// <summary>
/// A delivery report held for a reference no delivery holds yet, or for a delivery in a status
/// this release does not know (<c>02</c> MSG-SMS-004), applied when an outcome records that
/// reference and dropped by the watch job once it lapses.
/// </summary>
internal sealed class HeldReportRow
{
    internal const string ByDelivery = "delivery";

    internal const string ByProvider = "provider";

    public long Id { get; set; }

    /// <summary>The reference the report names.</summary>
    public required string Reference { get; set; }

    /// <summary>Whether the reference is a delivery reference or the provider's own.</summary>
    public required string Matched { get; set; }

    /// <summary>The report's final outcome, <c>delivered</c> or <c>failed</c>.</summary>
    public required string Outcome { get; set; }

    /// <summary>The address the report came from.</summary>
    public required string Source { get; set; }

    public required DateTimeOffset HeldAt { get; set; }

    /// <summary><c>@now</c> plus <c>Hermes:Sms:ReportHold</c>.</summary>
    public required DateTimeOffset LapsesAt { get; set; }
}