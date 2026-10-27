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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hermes.Sending;

/// <summary>
/// The <c>balance</c> job (<c>06</c> MSG-OPS-004; <c>02</c> MSG-SMS-006): where
/// <c>Hermes:Dispatch:Enabled</c> is on and SMS is used, whatever <c>Hermes:Jobs:Enabled</c>
/// says, every <c>Hermes:Sms:BalancePoll</c> it takes the job's claim, reads the balance and
/// records the reading with its instant, then judges the readings for <c>sms-balance</c> and
/// <c>sms-balance-unreadable</c>, renewing the claim after the reading and each alert call.
/// A provider that reports no balance records nothing, so it has no floor and raises neither.
/// </summary>
internal sealed partial class BalanceJob(
    MessagingDatabase database,
    Declarations declarations,
    IServiceScopeFactory scopes,
    Alerts alerts,
    IOptionsMonitor<MessagingOptions> options,
    IOptionsMonitor<ChannelsOptions> channels,
    TimeProvider clock,
    ILogger<BalanceJob> logger) : BackgroundService
{
    internal const string Name = "balance";

    internal const string Balance = "sms-balance";

    internal const string Unreadable = "sms-balance-unreadable";

    private PeriodicTimer? timer;

    /// <summary>Starts the timer before the loop, so the first run falls one poll after the start; nothing runs where the application does not dispatch or uses no SMS.</summary>
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.CurrentValue.Dispatch.Enabled || !declarations.Kinds.Any(kind => kind.Sms is not null))
        {
            return Task.CompletedTask;
        }

        timer = new PeriodicTimer(channels.CurrentValue.Sms.BalancePoll, clock);
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

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        MessagingOptions current = options.CurrentValue;
        SmsChannelOptions sms = channels.CurrentValue.Sms;
        Guid holder = Guid.CreateVersion7();
        if (!await JobClaims.ClaimAsync(database, Name, holder, sms.BalancePoll, current.Jobs.ClaimTimeout, clock.GetUtcNow(), stoppingToken).ConfigureAwait(false))
        {
            return;
        }

        BalanceReading reading = await ReadAsync(current.Dispatch.Timeout, stoppingToken).ConfigureAwait(false);
        if (reading is not BalanceReading.NotSupported)
        {
            DateTimeOffset now = clock.GetUtcNow();
            decimal? units = reading is BalanceReading.Balance balance ? balance.Units : null;
            if (!await RecordAsync(now, units, sms, current, stoppingToken).ConfigureAwait(false)
                || !await RenewAsync(holder, stoppingToken).ConfigureAwait(false)
                || !await JudgeAsync(holder, now, units, sms, current, stoppingToken).ConfigureAwait(false))
            {
                return;
            }
        }

        await JobClaims.ReleaseAsync(database, Name, holder, clock.GetUtcNow(), stoppingToken).ConfigureAwait(false);
    }

    /// <summary>Reads the balance within <paramref name="timeout"/>, a provider that throws or does not answer in time read as failed.</summary>
    private async Task<BalanceReading> ReadAsync(TimeSpan timeout, CancellationToken stoppingToken)
    {
        AsyncServiceScope scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            bound.CancelAfter(timeout);
            try
            {
                return await scope.ServiceProvider.GetRequiredService<ISmsProvider>().BalanceAsync(bound.Token).ConfigureAwait(false);
            }
            catch (Exception failure) when (!stoppingToken.IsCancellationRequested)
            {
                ReadFailed(logger, failure.GetType().Name);
                return new BalanceReading.Failed(failure is OperationCanceledException ? AttemptReason.Timeout : AttemptReason.ProviderFault);
            }
        }
    }

    /// <summary>
    /// Records the reading and drops those older than the drain's baseline and window, keeping
    /// the last balance read whatever its age, since the floor reads it.
    /// </summary>
    private Task<bool> RecordAsync(DateTimeOffset now, decimal? units, SmsChannelOptions sms, MessagingOptions current, CancellationToken cancellationToken)
    {
        DateTimeOffset kept = now - sms.DrainBaseline - sms.DrainWindow;
        return LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                context.BalanceReadings.Add(new BalanceRow { ReadAt = now, Units = units });
                await context.SaveChangesAsync(token).ConfigureAwait(false);
                await context.BalanceReadings
                    .Where(row => row.ReadAt < kept && context.BalanceReadings.Any(later => later.Units != null && later.ReadAt > row.ReadAt))
                    .ExecuteDeleteAsync(token).ConfigureAwait(false);
                return true;
            },
            cancellationToken);
    }

    /// <summary>Raises or clears each condition, renewing the claim after each call; answers false where a renewal finds the claim no longer its own.</summary>
    private async Task<bool> JudgeAsync(Guid holder, DateTimeOffset now, decimal? units, SmsChannelOptions sms, MessagingOptions current, CancellationToken cancellationToken)
    {
        MessagingContext context = database.Context();
        List<(DateTimeOffset At, decimal Units)> readings;
        DateTimeOffset? since;
        await using (context.ConfigureAwait(false))
        {
            readings = [.. (await context.BalanceReadings.AsNoTracking()
                .Where(row => row.Units != null && row.ReadAt > now - sms.DrainBaseline - sms.DrainWindow)
                .OrderBy(row => row.ReadAt)
                .Select(row => new { row.ReadAt, row.Units })
                .ToListAsync(cancellationToken).ConfigureAwait(false)).Select(row => (row.ReadAt, row.Units!.Value))];
            DateTimeOffset? read = await context.BalanceReadings.AsNoTracking().Where(row => row.Units != null).MaxAsync(row => (DateTimeOffset?)row.ReadAt, cancellationToken).ConfigureAwait(false);
            since = read ?? await context.BalanceReadings.AsNoTracking().MinAsync(row => (DateTimeOffset?)row.ReadAt, cancellationToken).ConfigureAwait(false);
        }

        if (units is { } balance)
        {
            if (BalanceJudgement.Which(readings, now, BalanceRules.Of(sms)) is { } which)
            {
                await alerts.HoldingAsync(Balance, null, Details(("which", which), ("balance", balance.ToString(CultureInfo.InvariantCulture))), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await alerts.NotHoldingAsync(Balance, null, cancellationToken).ConfigureAwait(false);
            }

            if (!await RenewAsync(holder, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            await alerts.NotHoldingAsync(Unreadable, null, cancellationToken).ConfigureAwait(false);
            return await RenewAsync(holder, cancellationToken).ConfigureAwait(false);
        }

        if (since is { } from && now - from >= sms.BalanceUnreadableAfter)
        {
            await alerts.HoldingAsync(Unreadable, null, Details(("since", from.ToString("O", CultureInfo.InvariantCulture))), cancellationToken).ConfigureAwait(false);
            return await RenewAsync(holder, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    private Task<bool> RenewAsync(Guid holder, CancellationToken cancellationToken)
    {
        return JobClaims.RenewAsync(database, Name, holder, options.CurrentValue.Jobs.ClaimTimeout, clock.GetUtcNow(), cancellationToken);
    }

    private static Dictionary<string, string> Details(params (string Name, string Value)[] details)
    {
        return details.ToDictionary(detail => detail.Name, detail => detail.Value, StringComparer.Ordinal);
    }

    [LoggerMessage(EventId = 52, EventName = "BalanceReadFailed", Level = LogLevel.Warning, Message = "Reading the SMS balance failed with {Exception}.")]
    private static partial void ReadFailed(ILogger logger, string exception);

    [LoggerMessage(EventId = 53, EventName = "BalanceJobFailed", Level = LogLevel.Error, Message = "A run of the balance job failed with {Exception}; it runs again at its next poll.")]
    private static partial void RunFailed(ILogger logger, string exception);
}