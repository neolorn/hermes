using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Channels;
using Hermes.Core;
using Hermes.Operations;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Hermes.Sending;

/// <summary>
/// Applies the SMS delivery reports the provider reads (<c>02</c> MSG-SMS-004), each hostile
/// input that can only move an SMS delivery the library sent, or is sending, to
/// <c>delivered</c> or <c>failed</c>: matched by delivery reference, or by provider reference
/// otherwise, under the reference's lock and then the delivery's row lock; held where no
/// delivery holds the provider reference yet; counted as rejected where it reads as nothing.
/// </summary>
internal sealed partial class SmsReports(
    MessagingDatabase database,
    IOptionsMonitor<MessagingOptions> options,
    IOptionsMonitor<ChannelsOptions> channels,
    Alerts alerts,
    Notices notices,
    TimeProvider clock,
    ILogger<SmsReports> logger)
{
    private const string Count = """
        INSERT INTO messaging.rejected_reports (minute, count) VALUES (@minute, 1)
        ON CONFLICT (minute) DO UPDATE SET count = rejected_reports.count + 1
        """;

    private const string Sum = "SELECT coalesce(sum(count), 0)::integer AS \"Value\" FROM messaging.rejected_reports WHERE minute > @now - @window";

    /// <summary>The reports rejected within <paramref name="window"/> before <paramref name="now"/>, summed over the minute rows.</summary>
    internal static Task<int> RejectedWithinAsync(MessagingContext context, DateTimeOffset now, TimeSpan window, CancellationToken cancellationToken)
    {
        return context.Database
            .SqlQueryRaw<int>(Sum, new NpgsqlParameter("now", now), new NpgsqlParameter("window", window))
            .SingleAsync(cancellationToken);
    }

    /// <summary>Applies the report <paramref name="reading"/> read, which came from <paramref name="source"/>, and answers what it did.</summary>
    internal async Task<ReportEffect> ApplyAsync(SmsReportReading reading, string source, CancellationToken cancellationToken)
    {
        if (!reading.Reads || reading.Outcome is not { } outcome)
        {
            await RejectAsync(source, cancellationToken).ConfigureAwait(false);
            return ReportEffect.Rejected;
        }

        if (outcome == SmsReportOutcome.NotFinal)
        {
            return ReportEffect.None;
        }

        (string? reference, bool byDelivery) = reading.DeliveryReference is { Length: > 0 } delivery
            ? (delivery, true)
            : (reading.ProviderReference is { Length: > 0 } provider ? provider : null, false);
        if (reference is null)
        {
            await RejectAsync(source, cancellationToken).ConfigureAwait(false);
            return ReportEffect.Rejected;
        }

        string final = outcome == SmsReportOutcome.Delivered ? ReportOutcomes.Delivered : ReportOutcomes.Failed;
        MessagingOptions current = options.CurrentValue;
        string sms = Spellings.Of(Channel.Sms);
        ReportEffect effect = await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await DeliveryClaims.LockTimeoutAsync(context, current.Dispatch.Timeout, token).ConfigureAwait(false);
                await AdvisoryLocks.TakeAsync(context, [AdvisoryLocks.Report(reference)], shared: false, token).ConfigureAwait(false);
                (Guid Id, int Attempt)? match = null;
                if (byDelivery)
                {
                    var attempt = await context.Attempts.AsNoTracking()
                        .Where(attempt => attempt.Reference == reference)
                        .Join(context.Deliveries.Where(row => row.Channel == sms), attempt => attempt.DeliveryId, row => row.Id, (attempt, row) => new { attempt.DeliveryId, attempt.Number })
                        .FirstOrDefaultAsync(token).ConfigureAwait(false);
                    match = attempt is null ? null : (attempt.DeliveryId, attempt.Number);
                }
                else
                {
                    List<Guid> holders = await context.Deliveries.AsNoTracking()
                        .Where(row => row.ProviderReference == reference && row.Channel == sms)
                        .Select(row => row.Id)
                        .Take(1)
                        .ToListAsync(token).ConfigureAwait(false);
                    match = holders.Count == 0 ? null : (holders[0], 0);
                }

                if (match is not { } found)
                {
                    return byDelivery ? ReportEffect.Rejected : await HoldAsync(context, reference, HeldReportRow.ByProvider, final, source, token).ConfigureAwait(false);
                }

                DeliveryRow? row = await DeliveryClaims.LockAsync(context, found.Id, token).ConfigureAwait(false);
                if (row is null)
                {
                    return ReportEffect.Rejected;
                }

                if (!Spellings.AllStatuses.Contains(row.Status))
                {
                    return await HoldAsync(context, reference, byDelivery ? HeldReportRow.ByDelivery : HeldReportRow.ByProvider, final, source, token).ConfigureAwait(false);
                }

                ReportEffect applied = await ReportOutcomes.ApplyAsync(context, row, byDelivery ? found.Attempt : row.Attempts, final, clock.GetUtcNow(), current.Observer.Enabled, token).ConfigureAwait(false);
                await context.SaveChangesAsync(token).ConfigureAwait(false);
                return applied;
            },
            cancellationToken).ConfigureAwait(false);

        switch (effect)
        {
            case ReportEffect.Rejected:
                await RejectAsync(source, cancellationToken).ConfigureAwait(false);
                break;
            case ReportEffect.Delivered or ReportEffect.Failed:
                notices.Count(sms, effect == ReportEffect.Delivered ? ReportOutcomes.Delivered : ReportOutcomes.Failed);
                break;
            case ReportEffect.Held:
                ReportHeld(logger, source);
                break;
        }

        return effect;
    }

    /// <summary>Counts a rejected report from <paramref name="source"/> in its minute's row, logs it with nothing of its content, and raises <c>sms-report-rejected</c> past the threshold.</summary>
    internal async Task RejectAsync(string source, CancellationToken cancellationToken)
    {
        ReportRejected(logger, source);
        SmsChannelOptions sms = channels.CurrentValue.Sms;
        DateTimeOffset now = clock.GetUtcNow();
        DateTimeOffset minute = new(now.UtcTicks - (now.UtcTicks % TimeSpan.TicksPerMinute), TimeSpan.Zero);
        int count = await LibraryTransactions.RunAsync(
            database,
            options.CurrentValue,
            async (context, token) =>
            {
                await context.Database.ExecuteSqlRawAsync(Count, [new NpgsqlParameter("minute", minute)], token).ConfigureAwait(false);
                return await RejectedWithinAsync(context, now, sms.ReportRejectWindow, token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
        if (count > sms.ReportRejectThreshold)
        {
            await alerts.HoldingAsync("sms-report-rejected", null, new Dictionary<string, string>(StringComparer.Ordinal) { ["count"] = count.ToString(CultureInfo.InvariantCulture) }, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Holds the report under the hold lock, or drops it, counted as rejected, where <c>Hermes:Sms:ReportHoldMax</c> are held already.</summary>
    private async Task<ReportEffect> HoldAsync(MessagingContext context, string reference, string matched, string outcome, string source, CancellationToken cancellationToken)
    {
        SmsChannelOptions sms = channels.CurrentValue.Sms;
        await AdvisoryLocks.TakeAsync(context, [AdvisoryLocks.ReportHold], shared: false, cancellationToken).ConfigureAwait(false);
        if (await context.HeldReports.CountAsync(cancellationToken).ConfigureAwait(false) >= sms.ReportHoldMax)
        {
            return ReportEffect.Rejected;
        }

        DateTimeOffset now = clock.GetUtcNow();
        context.HeldReports.Add(new HeldReportRow
        {
            Reference = reference,
            Matched = matched,
            Outcome = outcome,
            Source = source,
            HeldAt = now,
            LapsesAt = now + sms.ReportHold,
        });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ReportEffect.Held;
    }

    [LoggerMessage(EventId = 50, EventName = "SmsReportRejected", Level = LogLevel.Warning, Message = "A delivery report from {Source} was rejected.")]
    private static partial void ReportRejected(ILogger logger, string source);

    [LoggerMessage(EventId = 51, EventName = "SmsReportHeld", Level = LogLevel.Information, Message = "A delivery report from {Source} names a reference no delivery holds yet; it is held.")]
    private static partial void ReportHeld(ILogger logger, string source);
}