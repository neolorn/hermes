using System;
using System.Collections.Generic;

using Hermes.Operations;

namespace Hermes.Sending;

/// <summary>
/// The notices of <c>01</c> MSG-SEND-017, written in the transaction that makes a final status
/// or a move from <c>sent</c>, under the sequence number the delivery's row advanced, and the
/// meter's count of each (<c>06</c> MSG-OPS-005).
/// </summary>
internal sealed class Notices(MessagingMetrics metrics)
{
    /// <summary>Adds the notice of <paramref name="delivery"/>'s <paramref name="status"/> to <paramref name="context"/>, saved with its transaction.</summary>
    internal static void Add(MessagingContext context, Guid delivery, int sequence, string status, string? reason, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Notices.Add(new NoticeRow { DeliveryId = delivery, Sequence = sequence, Status = status, Reason = reason, At = at });
    }

    /// <summary>Counts a final status reached, or a move from <c>sent</c>, once its transaction committed.</summary>
    internal void Count(string channel, string status)
    {
        metrics.Deliveries.Add(1, new KeyValuePair<string, object?>("channel", channel), new KeyValuePair<string, object?>("status", status));
    }
}