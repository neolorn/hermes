using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Operations;

using Microsoft.EntityFrameworkCore;

namespace Hermes.Sending;

/// <summary>
/// What a final delivery report does to the SMS delivery it names (<c>02</c> MSG-SMS-004),
/// its row locked and its status one this release knows: the rows of the table from the
/// second, tried from the top, the first that fits applying. A report can only move the
/// delivery to <c>delivered</c> or <c>failed</c>.
/// </summary>
internal static class ReportOutcomes
{
    internal const string Delivered = "delivered";

    internal const string Failed = "failed";

    /// <summary>Applies <paramref name="outcome"/> of the attempt <paramref name="attempt"/> to <paramref name="row"/>, answering what it did.</summary>
    internal static async Task<ReportEffect> ApplyAsync(MessagingContext context, DeliveryRow row, int attempt, string outcome, DateTimeOffset now, bool observe, CancellationToken cancellationToken)
    {
        int last = row.Attempts;
        bool delivered = string.Equals(outcome, Delivered, StringComparison.Ordinal);
        switch (row.Status)
        {
            case "inflight":
                AttemptRow? kept = await context.Attempts.FindAsync([row.Id, attempt], cancellationToken).ConfigureAwait(false);
                if (kept is not null && !string.Equals(kept.Reported, Delivered, StringComparison.Ordinal))
                {
                    kept.Reported = outcome;
                }

                return ReportEffect.Kept;
            case "sent" when attempt == last:
                if (delivered)
                {
                    DeliveryEnding.End(context, row, Delivered, reason: null, now, observe);
                    return ReportEffect.Delivered;
                }

                DeliveryEnding.End(context, row, Failed, Spellings.Of(AttemptReason.ReportedFailed), now, observe);
                await Places.ReleaseLimitsAsync(context, [row.Id], cancellationToken).ConfigureAwait(false);
                return ReportEffect.Failed;
            case "sent" or "waiting" when attempt < last && delivered:
                DeliveryEnding.End(context, row, Delivered, reason: null, now, observe);
                return ReportEffect.Delivered;
            case "waiting" when attempt == last && delivered:
                AttemptRow? answered = await context.Attempts.FindAsync([row.Id, last], cancellationToken).ConfigureAwait(false);
                if (answered is null || answered.Class is not (null or "transient"))
                {
                    return ReportEffect.None;
                }

                if (answered.Class is null)
                {
                    answered.Class = Spellings.Of(AttemptClass.Transient);
                    answered.Reason = Spellings.Of(AttemptReason.ClaimLost);
                }

                DeliveryEnding.End(context, row, Delivered, reason: null, now, observe);
                return ReportEffect.Delivered;
            default:
                return ReportEffect.None;
        }
    }

    /// <summary>
    /// Applies the outcomes kept on an SMS delivery as the update that takes it out of
    /// <c>inflight</c> is made: a kept <c>delivered</c> ends it <c>delivered</c> whatever status
    /// the update gave it, since the message arrived; a kept <c>failed</c> of its last attempt
    /// applies where the update left it <c>sent</c>; answers the status it ended in, or none.
    /// </summary>
    internal static async Task<string?> ApplyKeptAsync(MessagingContext context, DeliveryRow row, DateTimeOffset now, bool observe, CancellationToken cancellationToken)
    {
        List<AttemptRow> kept = await context.Attempts.Where(attempt => attempt.DeliveryId == row.Id && attempt.Reported != null).ToListAsync(cancellationToken).ConfigureAwait(false);
        if (kept.Any(attempt => attempt.Reported == Delivered))
        {
            DeliveryEnding.Replace(context, row, Delivered, reason: null, now, observe);
            return Delivered;
        }

        if (row.Status == "sent" && kept.Any(attempt => attempt.Number == row.Attempts && attempt.Reported == Failed))
        {
            DeliveryEnding.Replace(context, row, Failed, Spellings.Of(AttemptReason.ReportedFailed), now, observe);
            return Failed;
        }

        return null;
    }

    /// <summary>Whether an attempt of the delivery has a kept <c>delivered</c>.</summary>
    internal static Task<bool> DeliveredKeptAsync(MessagingContext context, Guid delivery, CancellationToken cancellationToken)
    {
        return context.Attempts.AnyAsync(attempt => attempt.DeliveryId == delivery && attempt.Reported == Delivered, cancellationToken);
    }

    /// <summary>
    /// Applies the reports held for <paramref name="reference"/>, the provider reference an
    /// outcome just recorded on <paramref name="row"/>, each in the order held, and removes them.
    /// </summary>
    internal static async Task<IReadOnlyList<ReportEffect>> ApplyHeldAsync(MessagingContext context, DeliveryRow row, string reference, DateTimeOffset now, bool observe, CancellationToken cancellationToken)
    {
        List<HeldReportRow> held = await context.HeldReports
            .Where(report => report.Reference == reference && report.Matched == HeldReportRow.ByProvider && report.LapsesAt > now)
            .OrderBy(report => report.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        List<ReportEffect> effects = [];
        foreach (HeldReportRow report in held)
        {
            effects.Add(await ApplyAsync(context, row, row.Attempts, report.Outcome, now, observe, cancellationToken).ConfigureAwait(false));
            context.HeldReports.Remove(report);
        }

        return effects;
    }
}