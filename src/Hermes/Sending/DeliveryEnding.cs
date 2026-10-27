using System;
using System.Linq;

using Hermes.Operations;

using Microsoft.EntityFrameworkCore;

namespace Hermes.Sending;

/// <summary>Moves a delivery's locked row to a status, writing its notice where the application observes (<c>01</c> MSG-SEND-017).</summary>
internal static class DeliveryEnding
{
    internal static void End(MessagingContext context, DeliveryRow row, string status, string? reason, DateTimeOffset now, bool observe)
    {
        row.Status = status;
        row.Reason = reason;
        row.LastMovedAt = now;
        DeliveryClaims.Release(row);
        if (observe)
        {
            Notices.Add(context, row.Id, row.NextSequence, status, reason, now);
            row.NextSequence++;
        }
    }

    /// <summary>
    /// Ends the delivery <paramref name="status"/> in place of the status the same update gave
    /// it, the notice that update wrote, where it wrote one, telling the new status instead.
    /// </summary>
    internal static void Replace(MessagingContext context, DeliveryRow row, string status, string? reason, DateTimeOffset now, bool observe)
    {
        NoticeRow? pending = context.ChangeTracker.Entries<NoticeRow>()
            .Where(entry => entry.State == EntityState.Added && entry.Entity.DeliveryId == row.Id)
            .Select(entry => entry.Entity)
            .OrderByDescending(notice => notice.Sequence)
            .FirstOrDefault();
        if (pending is null)
        {
            End(context, row, status, reason, now, observe);
            return;
        }

        row.Status = status;
        row.Reason = reason;
        row.LastMovedAt = now;
        DeliveryClaims.Release(row);
        pending.Status = status;
        pending.Reason = reason;
    }
}