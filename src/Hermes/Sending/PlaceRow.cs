using System;

namespace Hermes.Sending;

/// <summary>
/// A place a delivery took under one of its category's limits (<c>01</c> MSG-SEND-014): a
/// <c>Limit</c> place keyed by the destination's or person's keyed hash, or a
/// <c>DeploymentLimit</c> place keyed by its channel, category and window alone.
/// </summary>
internal sealed class PlaceRow
{
    internal const string Limit = "limit";

    internal const string Deployment = "deployment";

    public long Id { get; set; }

    /// <summary>The delivery that took it, set to null when the delivery's record is deleted.</summary>
    public Guid? DeliveryId { get; set; }

    public required string Kind { get; set; }

    public required string Channel { get; set; }

    public required string Category { get; set; }

    public required int WindowSeconds { get; set; }

    public byte[]? Hash { get; set; }

    public int? HashVersion { get; set; }

    public required DateTimeOffset TakenAt { get; set; }

    /// <summary><c>@now</c> plus the writing application's <c>Hermes:Retention:Places</c>.</summary>
    public required DateTimeOffset LapsesAt { get; set; }
}