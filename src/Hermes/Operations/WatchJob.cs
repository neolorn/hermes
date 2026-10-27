using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Channels;
using Hermes.Sending;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hermes.Operations;

/// <summary>
/// The <c>watch</c> job (<c>06</c> MSG-OPS-004): where <c>Hermes:Jobs:Enabled</c> is on, every
/// <c>Hermes:Dispatch:Interval</c> it takes the job's claim and judges each condition in turn,
/// renewing the claim after each: <c>dispatch-lag</c> per channel, recording the gauge;
/// <c>job-stalled</c> per job; <c>observer-failing</c>; <c>sms-report-rejected</c> and
/// <c>push-auth-failing</c> by their counts; the conditions a process writes an instant for,
/// cleared once that instant has passed; and the SMS reports held past
/// <c>Hermes:Sms:ReportHold</c>, dropped.
/// </summary>
internal sealed partial class WatchJob(
    MessagingDatabase database,
    Declarations declarations,
    Alerts alerts,
    MessagingMetrics metrics,
    IOptionsMonitor<MessagingOptions> options,
    IOptionsMonitor<ChannelsOptions> channels,
    TimeProvider clock,
    ILogger<WatchJob> logger) : BackgroundService
{
    internal const string Name = "watch";

    /// <summary>The conditions a process writes the instant it holds until for, which the job clears once it has passed.</summary>
    private static readonly string[] Lapsing = ["newer-release", "key-unavailable", "content-key-limit", "content-rejected"];

    private readonly string[] kinds = DueDeliveries.Kinds(declarations);

    private PeriodicTimer? timer;

    /// <summary>Starts the timer before the loop, so the first run falls one interval after the start.</summary>
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.CurrentValue.Jobs.Enabled)
        {
            return Task.CompletedTask;
        }

        timer = new PeriodicTimer(options.CurrentValue.Dispatch.Interval, clock);
        return base.StartAsync(cancellationToken);
    }

    public override void Dispose()
    {
        timer?.Dispose();
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        PeriodicTimer timer = this.timer!;
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await RunAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception failure) when (failure is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                RunFailed(logger, failure.GetType().Name);
            }
        }
    }

    private static Dictionary<string, string> Details(params (string Name, string Value)[] details)
    {
        return details.ToDictionary(detail => detail.Name, detail => detail.Value, StringComparer.Ordinal);
    }

    private static string Seconds(TimeSpan age)
    {
        return ((long)Math.Floor(age.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
    }

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        MessagingOptions current = options.CurrentValue;
        Guid holder = Guid.CreateVersion7();
        if (!await JobClaims.ClaimAsync(database, Name, holder, current.Dispatch.Interval, current.Jobs.ClaimTimeout, clock.GetUtcNow(), stoppingToken).ConfigureAwait(false))
        {
            return;
        }

        List<Func<CancellationToken, Task>> units =
        [
            LagAsync,
            StalledAsync,
            ObserverAsync,
            ReportsRejectedAsync,
            AuthFailingAsync,
            .. Lapsing.Select(condition => (Func<CancellationToken, Task>)(token => alerts.ClearLapsedAsync(condition, token))),
            DropHeldAsync,
        ];
        foreach (Func<CancellationToken, Task> unit in units)
        {
            await unit(stoppingToken).ConfigureAwait(false);
            if (!await JobClaims.RenewAsync(database, Name, holder, options.CurrentValue.Jobs.ClaimTimeout, clock.GetUtcNow(), stoppingToken).ConfigureAwait(false))
            {
                return;
            }
        }

        await JobClaims.ReleaseAsync(database, Name, holder, clock.GetUtcNow(), stoppingToken).ConfigureAwait(false);
    }

    /// <summary><c>dispatch-lag</c> per channel (<c>06</c> MSG-OPS-005), recording the gauge, 0 where no delivery is due.</summary>
    private async Task LagAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.GetUtcNow();
        var oldest = await LibraryTransactions.RunAsync(
            database,
            options.CurrentValue,
            (context, token) => DueDeliveries.Due(context.Deliveries, kinds, now)
                .GroupBy(delivery => delivery.Channel)
                .Select(channel => new { Channel = channel.Key, Due = channel.Min(delivery => delivery.BecameDueAt) })
                .ToListAsync(token),
            cancellationToken).ConfigureAwait(false);
        TimeSpan threshold = options.CurrentValue.Dispatch.LagThreshold;
        foreach (string channel in Spellings.AllChannels)
        {
            TimeSpan age = oldest.FirstOrDefault(row => row.Channel == channel) is { } row && now > row.Due ? now - row.Due : TimeSpan.Zero;
            metrics.OldestDueAge.Record(age.TotalSeconds, new KeyValuePair<string, object?>("channel", channel));
            if (age > threshold)
            {
                await alerts.HoldingAsync("dispatch-lag", channel, Details(("channel", channel), ("age", Seconds(age))), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await alerts.NotHoldingAsync("dispatch-lag", channel, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary><c>job-stalled</c> for each job a process has started, the watch job itself included.</summary>
    private async Task StalledAsync(CancellationToken cancellationToken)
    {
        MessagingOptions current = options.CurrentValue;
        ChannelsOptions channel = channels.CurrentValue;
        DateTimeOffset now = clock.GetUtcNow();
        List<JobRow> rows = await LibraryTransactions.RunAsync(
            database,
            current,
            (context, token) => context.Jobs.AsNoTracking().Where(row => row.FirstStartedAt != null).ToListAsync(token),
            cancellationToken).ConfigureAwait(false);
        foreach (JobRow row in rows.Where(row => JobRow.Names.Contains(row.Name)).OrderBy(row => row.Name, StringComparer.Ordinal))
        {
            if (JobStall.Stalled(row, JobStall.Interval(row.Name, current, channel), current.Jobs.StallFactor, current.Dispatch.Timeout, now))
            {
                await alerts.HoldingAsync("job-stalled", row.Name, Details(("job", row.Name)), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await alerts.NotHoldingAsync("job-stalled", row.Name, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary><c>observer-failing</c> (<c>01</c> MSG-SEND-017): the oldest pending notice older than <c>Hermes:Observer:LagThreshold</c>.</summary>
    private async Task ObserverAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.GetUtcNow();
        DateTimeOffset? oldest = await LibraryTransactions.RunAsync(
            database,
            options.CurrentValue,
            (context, token) => context.Notices.MinAsync(notice => (DateTimeOffset?)notice.At, token),
            cancellationToken).ConfigureAwait(false);
        TimeSpan age = oldest is { } at && now > at ? now - at : TimeSpan.Zero;
        if (age > options.CurrentValue.Observer.LagThreshold)
        {
            await alerts.HoldingAsync("observer-failing", null, Details(("age", Seconds(age))), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await alerts.NotHoldingAsync("observer-failing", null, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary><c>sms-report-rejected</c> by its count (<c>02</c> MSG-SMS-004), raised again while it holds.</summary>
    private async Task ReportsRejectedAsync(CancellationToken cancellationToken)
    {
        SmsChannelOptions sms = channels.CurrentValue.Sms;
        DateTimeOffset now = clock.GetUtcNow();
        int count = await LibraryTransactions.RunAsync(
            database,
            options.CurrentValue,
            (context, token) => SmsReports.RejectedWithinAsync(context, now, sms.ReportRejectWindow, token),
            cancellationToken).ConfigureAwait(false);
        if (count > sms.ReportRejectThreshold)
        {
            await alerts.HoldingAsync("sms-report-rejected", null, Details(("count", count.ToString(CultureInfo.InvariantCulture))), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await alerts.NotHoldingAsync("sms-report-rejected", null, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary><c>push-auth-failing</c> by each service's count (<c>02</c> MSG-PUSH-005), over the services counted within the window and those it holds for.</summary>
    private async Task AuthFailingAsync(CancellationToken cancellationToken)
    {
        PushChannelOptions push = channels.CurrentValue.Push;
        DateTimeOffset now = clock.GetUtcNow();
        IReadOnlyList<string?> holding = await alerts.HoldingScopesAsync("push-auth-failing", cancellationToken).ConfigureAwait(false);
        List<(string Service, int Count)> counts = await LibraryTransactions.RunAsync(
            database,
            options.CurrentValue,
            async (context, token) =>
            {
                List<string> services = await PushAuthFailures.ServicesAsync(context, now, push.AuthFailWindow, token).ConfigureAwait(false);
                List<(string Service, int Count)> counted = [];
                foreach (string service in services.Concat(holding.OfType<string>()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
                {
                    counted.Add((service, await PushAuthFailures.SumAsync(context, service, now, push.AuthFailWindow, token).ConfigureAwait(false)));
                }

                return counted;
            },
            cancellationToken).ConfigureAwait(false);
        foreach ((string service, int count) in counts)
        {
            if (count > push.AuthFailThreshold)
            {
                await alerts.HoldingAsync("push-auth-failing", service, PushAuthFailures.Details(service, count), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await alerts.NotHoldingAsync("push-auth-failing", service, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Drops the SMS reports held past <c>Hermes:Sms:ReportHold</c> (<c>02</c> MSG-SMS-004), logged and not counted as rejected.</summary>
    private async Task DropHeldAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.GetUtcNow();
        int dropped = await LibraryTransactions.RunAsync(
            database,
            options.CurrentValue,
            (context, token) => context.HeldReports.Where(report => report.LapsesAt <= now).ExecuteDeleteAsync(token),
            cancellationToken).ConfigureAwait(false);
        if (dropped > 0)
        {
            HeldDropped(logger, dropped);
        }
    }

    [LoggerMessage(EventId = 57, EventName = "HeldReportsDropped", Level = LogLevel.Warning, Message = "The watch job dropped {Count} SMS delivery reports held past Hermes:Sms:ReportHold, never applied.")]
    private static partial void HeldDropped(ILogger logger, int count);

    [LoggerMessage(EventId = 58, EventName = "WatchJobFailed", Level = LogLevel.Error, Message = "A run of the watch job failed with {Exception}; it runs again at its next interval.")]
    private static partial void RunFailed(ILogger logger, string exception);
}