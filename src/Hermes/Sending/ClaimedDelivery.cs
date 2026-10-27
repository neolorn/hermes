using System;

namespace Hermes.Sending;

/// <summary>A delivery a pass claimed, with where the pass is in its work (<c>01</c> MSG-SEND-007).</summary>
internal sealed class ClaimedDelivery(DeliveryRow row)
{
    internal DeliveryRow Row { get; } = row;

    /// <summary>The instant of the claim as last set, by the claim, an extension or a renewal.</summary>
    internal DateTimeOffset ClaimedAt { get; set; } = row.ClaimedAt!.Value;

    internal ClaimStage Stage { get; set; } = ClaimStage.NotReached;
}