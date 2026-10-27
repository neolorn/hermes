using System;
using System.Collections.Generic;
using System.Linq;

using Hermes.Sending;

namespace Hermes.Operations;

/// <summary>
/// The deliveries <c>dispatch-lag</c>, the health check and the gauge judge (<c>06</c>
/// MSG-OPS-005): <c>waiting</c> or <c>inflight</c> whose became-due instant has passed, a row
/// this release does not know left out (<c>01</c> MSG-SEND-007).
/// </summary>
internal static class DueDeliveries
{
    /// <summary>The kinds this release knows: those declared and those retired.</summary>
    internal static string[] Kinds(Declarations declarations)
    {
        return [.. declarations.Kinds.Select(kind => kind.Name).Concat(declarations.Retired).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>The rows of <paramref name="deliveries"/> due at <paramref name="now"/> of the <paramref name="kinds"/> known.</summary>
    internal static IQueryable<DeliveryRow> Due(IQueryable<DeliveryRow> deliveries, IReadOnlyCollection<string> kinds, DateTimeOffset now)
    {
        string[] known = [.. kinds];
        string[] channels = [.. Spellings.AllChannels];
        string[] reasons = [.. Spellings.AllReasons];
        return deliveries.Where(delivery => (delivery.Status == "waiting" || delivery.Status == "inflight")
            && delivery.BecameDueAt <= now
            && known.Contains(delivery.Kind)
            && channels.Contains(delivery.Channel)
            && (delivery.Reason == null || reasons.Contains(delivery.Reason)));
    }
}