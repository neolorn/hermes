using System;

namespace Hermes.Sending;

/// <summary>
/// A row of the suppression table (<c>01</c> MSG-SEND-012): the destination's keyed hash under
/// a <c>hashKeys</c> version, with its channel, category and instant, never the destination.
/// </summary>
internal sealed class SuppressionRow
{
    public required string Channel { get; set; }

    public required string Category { get; set; }

    /// <summary>The keyed hash of the destination, domain <c>email</c> or <c>sms</c>.</summary>
    public required byte[] Hash { get; set; }

    public required int HashVersion { get; set; }

    public required DateTimeOffset MadeAt { get; set; }
}