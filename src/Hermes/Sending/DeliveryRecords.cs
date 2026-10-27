using System;
using System.Collections.Generic;
using System.Linq;

using Hermes.Core;

namespace Hermes.Sending;

/// <summary>
/// A delivery as <c>IMessages.ReadAsync</c> answers it (<c>01</c> MSG-SEND-017): a delivery
/// whose channel, status or reason this release does not know is left out, and so is an
/// attempt or a call whose class or reason it does not know (<c>01</c> MSG-SEND-007).
/// </summary>
internal static class DeliveryRecords
{
    /// <summary>The record of <paramref name="delivery"/> with its attempts and their calls, or none for a row this release does not know.</summary>
    internal static DeliveryRecord? Of(DeliveryRow delivery, IEnumerable<AttemptRow> attempts, IEnumerable<CallRow> calls)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        if (Spellings.Channel(delivery.Channel) is not { } channel
            || Spellings.Status(delivery.Status) is not { } status
            || !Known(delivery.Reason, out AttemptReason? reason))
        {
            return null;
        }

        List<AttemptRecord> records = [];
        foreach (AttemptRow attempt in attempts.Where(row => row.DeliveryId == delivery.Id).OrderBy(row => row.Number))
        {
            if (!KnownClass(attempt.Class, out AttemptClass? answer) || !Known(attempt.Reason, out AttemptReason? attemptReason))
            {
                continue;
            }

            List<CallRecord> made = [];
            foreach (CallRow call in calls.Where(row => row.DeliveryId == delivery.Id && row.Attempt == attempt.Number).OrderBy(row => row.Number))
            {
                if (Spellings.Class(call.Class) is { } callClass
                    && Known(call.Reason, out AttemptReason? callReason)
                    && (call.PushService is null || Spellings.Service(call.PushService) is not null))
                {
                    made.Add(new CallRecord
                    {
                        AnswerClass = callClass,
                        Reason = callReason,
                        Status = call.Status,
                        Service = call.PushService is null ? null : Spellings.Service(call.PushService),
                    });
                }
            }

            records.Add(new AttemptRecord { Number = attempt.Number, At = attempt.At, AnswerClass = answer, Reason = attemptReason, Calls = made });
        }

        return new DeliveryRecord
        {
            Delivery = new DeliveryId(delivery.Id),
            Channel = channel,
            Status = status,
            Reason = reason,
            RecordedAt = delivery.RecordedAt,
            LastMovedAt = delivery.LastMovedAt,
            Attempts = records,
        };
    }

    private static bool Known(string? spelling, out AttemptReason? reason)
    {
        reason = spelling is null ? null : Spellings.Reason(spelling);
        return spelling is null || reason is not null;
    }

    private static bool KnownClass(string? spelling, out AttemptClass? answer)
    {
        answer = spelling is null ? null : Spellings.Class(spelling);
        return spelling is null || answer is not null;
    }
}