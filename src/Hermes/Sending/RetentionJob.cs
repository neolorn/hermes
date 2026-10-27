using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Channels;
using Hermes.Operations;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Hermes.Sending;

/// <summary>
/// The <c>retention</c> job (<c>06</c> MSG-OPS-004; <c>01</c> MSG-SEND-019): where
/// <c>Hermes:Jobs:Enabled</c> is on, daily at <c>Hermes:Jobs:RetentionAt</c> in
/// <c>Hermes:TimeZone</c> it takes the job's claim, where no run has started since that
/// instant, and deletes what is past its period in batches of <c>Hermes:Jobs:BatchSize</c>,
/// each selecting its rows <c>FOR UPDATE SKIP LOCKED</c> and renewing the claim after it.
/// A row whose channel, status or reason this release does not know, or of a kind the
/// application neither declares nor retires, is never touched but by its own deletion.
/// </summary>
internal sealed partial class RetentionJob(
    MessagingDatabase database,
    Declarations declarations,
    IOptionsMonitor<MessagingOptions> options,
    IOptionsMonitor<ChannelsOptions> channels,
    TimeProvider clock,
    ILogger<RetentionJob> logger) : BackgroundService
{
    internal const string Name = "retention";

    private const string Known = """
        channel = ANY(@channels) AND status = ANY(@finals) AND (reason IS NULL OR reason = ANY(@reasons)) AND kind = ANY(@kinds)
        """;

    private const string DeleteContent = $"""
        UPDATE messaging.deliveries
        SET destination = NULL, unsubscribe = NULL, subject = NULL, text = NULL, html = NULL,
            title = NULL, body = NULL, url = NULL, tag = NULL, icon = NULL, badge = NULL, content_key_version = NULL
        WHERE id IN (
            SELECT id FROM messaging.deliveries
            WHERE {Known} AND last_moved_at <= @now - @period
              AND (destination IS NOT NULL OR unsubscribe IS NOT NULL OR subject IS NOT NULL OR text IS NOT NULL OR html IS NOT NULL
                   OR title IS NOT NULL OR body IS NOT NULL OR url IS NOT NULL OR tag IS NOT NULL OR icon IS NOT NULL OR badge IS NOT NULL
                   OR content_key_version IS NOT NULL)
            ORDER BY id LIMIT @batch FOR UPDATE SKIP LOCKED)
        """;

    private const string DeleteRecords = $"""
        DELETE FROM messaging.deliveries WHERE id IN (
            SELECT id FROM messaging.deliveries
            WHERE {Known} AND last_moved_at <= @now - @period
            ORDER BY id LIMIT @batch FOR UPDATE SKIP LOCKED)
        """;

    private const string DeleteUnknown = """
        DELETE FROM messaging.deliveries WHERE id IN (
            SELECT id FROM messaging.deliveries
            WHERE NOT (channel = ANY(@channels) AND status = ANY(@statuses) AND (reason IS NULL OR reason = ANY(@reasons)) AND kind = ANY(@kinds))
              AND greatest(recorded_at, due_at) <= @now - @period
            ORDER BY id LIMIT @batch FOR UPDATE SKIP LOCKED)
        """;

    private const string DeleteSends = """
        DELETE FROM messaging.sends WHERE id IN (
            SELECT s.id FROM messaging.sends s
            WHERE s.recorded_at <= @now - @period AND NOT EXISTS (SELECT 1 FROM messaging.deliveries d WHERE d.send_id = s.id)
            ORDER BY s.id LIMIT @batch FOR UPDATE SKIP LOCKED)
        """;

    private const string DeleteIdempotency = """
        DELETE FROM messaging.idempotency WHERE (kind, anonymous, scope, key) IN (
            SELECT kind, anonymous, scope, key FROM messaging.idempotency WHERE lapses_at <= @now
            ORDER BY lapses_at LIMIT @batch FOR UPDATE SKIP LOCKED)
        """;

    private const string DeletePlaces = """
        DELETE FROM messaging.places WHERE id IN (
            SELECT id FROM messaging.places WHERE lapses_at <= @now
            ORDER BY lapses_at LIMIT @batch FOR UPDATE SKIP LOCKED)
        """;

    private const string DeleteSuppressions = """
        DELETE FROM messaging.suppressions WHERE (channel, category, hash) IN (
            SELECT channel, category, hash FROM messaging.suppressions WHERE channel = @channel AND made_at <= @now - @period
            ORDER BY made_at LIMIT @batch FOR UPDATE SKIP LOCKED)
        """;

    private const string DeleteIdleSubscriptions = """
        DELETE FROM messaging.subscriptions WHERE id IN (
            SELECT id FROM messaging.subscriptions WHERE coalesce(last_accepted_at, made_at) <= @now - @period
            ORDER BY id LIMIT @batch FOR UPDATE SKIP LOCKED)
        """;

    private const string DeleteNotices = """
        DELETE FROM messaging.notices WHERE (delivery_id, sequence) IN (
            SELECT delivery_id, sequence FROM messaging.notices WHERE at <= @now - @period
            ORDER BY at LIMIT @batch FOR UPDATE SKIP LOCKED)
        """;

    private const string DeleteReadings = """
        DELETE FROM messaging.balance_readings WHERE read_at IN (
            SELECT read_at FROM messaging.balance_readings r
            WHERE r.read_at < @now - @period
              AND NOT (r.units IS NOT NULL AND r.read_at = (SELECT max(b.read_at) FROM messaging.balance_readings b WHERE b.units IS NOT NULL))
            ORDER BY r.read_at LIMIT @batch FOR UPDATE SKIP LOCKED)
        """;

    private const string DeleteRejectedReports = """
        DELETE FROM messaging.rejected_reports WHERE minute IN (
            SELECT minute FROM messaging.rejected_reports WHERE minute < @now - @period
            ORDER BY minute LIMIT @batch FOR UPDATE SKIP LOCKED)
        """;

    private const string DeleteAuthFailures = """
        DELETE FROM messaging.push_auth_failures WHERE (service, minute) IN (
            SELECT service, minute FROM messaging.push_auth_failures WHERE minute < @now - @period
            ORDER BY minute LIMIT @batch FOR UPDATE SKIP LOCKED)
        """;

    private const string DeleteRateCounters = """
        DELETE FROM messaging.rate_counters WHERE (channel, second) IN (
            SELECT channel, second FROM messaging.rate_counters WHERE second <= @now - @period
            ORDER BY second LIMIT @batch FOR UPDATE SKIP LOCKED)
        """;

    private static readonly string[] Finals = [.. Spellings.AllStatuses.Where(status => status is not ("waiting" or "inflight"))];

    /// <summary>Waits for each <c>Hermes:Jobs:RetentionAt</c> in turn and runs the job then.</summary>
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        return options.CurrentValue.Jobs.Enabled ? base.StartAsync(cancellationToken) : Task.CompletedTask;
    }

    /// <summary>The last instant at or before <paramref name="now"/> whose local time in <paramref name="zone"/> is <paramref name="at"/>, a time the zone skips taken at the first local time after it.</summary>
    internal static DateTimeOffset Last(DateTimeOffset now, TimeOnly at, TimeZoneInfo zone)
    {
        DateTime local = TimeZoneInfo.ConvertTime(now, zone).DateTime;
        DateTime day = local.Date;
        DateTimeOffset instant = Instant(day + at.ToTimeSpan(), zone);
        return instant <= now ? instant : Instant(day.AddDays(-1) + at.ToTimeSpan(), zone);
    }

    /// <summary>The first instant after <paramref name="now"/> whose local time in <paramref name="zone"/> is <paramref name="at"/>.</summary>
    internal static DateTimeOffset Next(DateTimeOffset now, TimeOnly at, TimeZoneInfo zone)
    {
        DateTime day = TimeZoneInfo.ConvertTime(now, zone).DateTime.Date;
        for (int days = 0; ; days++)
        {
            DateTimeOffset instant = Instant(day.AddDays(days) + at.ToTimeSpan(), zone);
            if (instant > now)
            {
                return instant;
            }
        }
    }

    /// <summary>
    /// Runs the job once where its claim is free and no run has started since the last
    /// <c>Hermes:Jobs:RetentionAt</c>, answering whether it ran to the end.
    /// </summary>
    internal async Task<bool> RunAsync(CancellationToken cancellationToken)
    {
        MessagingOptions current = options.CurrentValue;
        ChannelsOptions used = channels.CurrentValue;
        DateTimeOffset now = clock.GetUtcNow();
        DateTimeOffset since = Last(now, current.Jobs.RetentionAt, TimeZoneInfo.FindSystemTimeZoneById(current.TimeZone));

        // The claim's condition is a last start at or before @now - interval + 1 second; this makes it one before since.
        TimeSpan interval = now - since + TimeSpan.FromSeconds(1) + TimeSpan.FromTicks(10);
        Guid holder = Guid.CreateVersion7();
        if (!await JobClaims.ClaimAsync(database, Name, holder, interval, current.Jobs.ClaimTimeout, now, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        Run run = new(database, options, clock, Kinds(), holder, current, cancellationToken);
        RetentionOptions retention = current.Retention;
        if (!await run.DeleteAsync(DeleteContent, retention.Content).ConfigureAwait(false)
            || !await run.DeleteAsync(DeleteRecords, retention.Record).ConfigureAwait(false)
            || !await run.DeleteAsync(DeleteUnknown, retention.Record, count => UnknownDeleted(logger, count), each: false).ConfigureAwait(false)
            || !await run.DeleteAsync(DeleteSends, retention.Record).ConfigureAwait(false)
            || !await run.DeleteAsync(DeleteIdempotency, period: null).ConfigureAwait(false)
            || !await run.DeleteAsync(DeletePlaces, period: null).ConfigureAwait(false)
            || !await run.DeleteSuppressionsAsync("email", retention.Suppression.Email).ConfigureAwait(false)
            || !await run.DeleteSuppressionsAsync("sms", retention.Suppression.Sms).ConfigureAwait(false)
            || !await run.DeleteAsync(DeleteIdleSubscriptions, used.Push.IdleRetention).ConfigureAwait(false)
            || !await run.DeleteAsync(DeleteNotices, retention.Record, count => NoticesDeleted(logger, count), each: true).ConfigureAwait(false)
            || !await run.DeleteAsync(DeleteReadings, used.Sms.DrainBaseline).ConfigureAwait(false)
            || !await run.DeleteAsync(DeleteRejectedReports, used.Sms.ReportRejectWindow).ConfigureAwait(false)
            || !await run.DeleteAsync(DeleteAuthFailures, used.Push.AuthFailWindow).ConfigureAwait(false)
            || !await run.DeleteAsync(DeleteRateCounters, TimeSpan.FromSeconds(1)).ConfigureAwait(false))
        {
            return false;
        }

        await JobClaims.ReleaseAsync(database, Name, holder, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return true;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            MessagingOptions current = options.CurrentValue;
            DateTimeOffset now = clock.GetUtcNow();
            DateTimeOffset next = Next(now, current.Jobs.RetentionAt, TimeZoneInfo.FindSystemTimeZoneById(current.TimeZone));
            try
            {
                await Task.Delay(next - now, clock, stoppingToken).ConfigureAwait(false);
                await RunAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception failure) when (failure is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                RunFailed(logger, failure.GetType().Name);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>The kinds the application declares or retires.</summary>
    private string[] Kinds()
    {
        return [.. declarations.Kinds.Select(kind => kind.Name).Concat(declarations.Retired).Distinct(StringComparer.Ordinal)];
    }

    private static DateTimeOffset Instant(DateTime local, TimeZoneInfo zone)
    {
        DateTime at = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        while (zone.IsInvalidTime(at))
        {
            at = at.AddMinutes(1);
        }

        return new DateTimeOffset(at, zone.GetUtcOffset(at));
    }

    [LoggerMessage(EventId = 59, EventName = "RetentionNoticesDeleted", Level = LogLevel.Information, Message = "The retention job deleted {Count} observer notices older than the record's retention.")]
    private static partial void NoticesDeleted(ILogger logger, int count);

    [LoggerMessage(EventId = 60, EventName = "RetentionUnknownDeleted", Level = LogLevel.Information, Message = "The retention job deleted {Count} deliveries of a channel, status, reason or kind this release or application does not know.")]
    private static partial void UnknownDeleted(ILogger logger, int count);

    [LoggerMessage(EventId = 61, EventName = "RetentionJobFailed", Level = LogLevel.Error, Message = "A run of the retention job failed with {Exception}; it runs again at its next time.")]
    private static partial void RunFailed(ILogger logger, string exception);

    /// <summary>One run's batches under its claim.</summary>
    private sealed class Run(MessagingDatabase database, IOptionsMonitor<MessagingOptions> options, TimeProvider clock, string[] kinds, Guid holder, MessagingOptions current, CancellationToken cancellationToken)
    {
        /// <summary>Runs <paramref name="statement"/> in batches until one deletes fewer than a batch, renewing the claim after each; false where a renewal finds it no longer the run's.</summary>
        internal async Task<bool> DeleteAsync(string statement, TimeSpan? period, Action<int>? logged = null, bool each = false, string? channel = null)
        {
            int batch = current.Jobs.BatchSize;
            int total = 0;
            int count;
            do
            {
                count = await LibraryTransactions.RunAsync(
                    database,
                    current,
                    (context, token) => context.Database.ExecuteSqlRawAsync(statement, Parameters(period, batch, channel), token),
                    cancellationToken).ConfigureAwait(false);
                total += count;
                if (each && count > 0)
                {
                    logged?.Invoke(count);
                }

                if (!await JobClaims.RenewAsync(database, Name, holder, options.CurrentValue.Jobs.ClaimTimeout, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false))
                {
                    return false;
                }
            }
            while (count == batch);

            if (!each && total > 0)
            {
                logged?.Invoke(total);
            }

            return true;
        }

        /// <summary>Deletes the suppressions of <paramref name="channel"/> past <paramref name="lifetime"/>, none where they last indefinitely.</summary>
        internal Task<bool> DeleteSuppressionsAsync(string channel, TimeSpan? lifetime)
        {
            return lifetime is { } period ? DeleteAsync(DeleteSuppressions, period, channel: channel) : Task.FromResult(true);
        }

        private List<NpgsqlParameter> Parameters(TimeSpan? period, int batch, string? channel)
        {
            List<NpgsqlParameter> parameters =
            [
                new("now", clock.GetUtcNow()),
                new("batch", batch),
                new("channels", Spellings.AllChannels.ToArray()),
                new("statuses", Spellings.AllStatuses.ToArray()),
                new("finals", Finals),
                new("reasons", Spellings.AllReasons.ToArray()),
                new("kinds", kinds),
            ];
            if (period is { } value)
            {
                parameters.Add(new NpgsqlParameter("period", value));
            }

            if (channel is not null)
            {
                parameters.Add(new NpgsqlParameter("channel", channel));
            }

            return parameters;
        }
    }
}