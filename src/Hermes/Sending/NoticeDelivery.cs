using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Operations;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hermes.Sending;

/// <summary>
/// Tells the observer of the pending notices (<c>01</c> MSG-SEND-017): each delivery's lowest
/// pending notice alone, up to <c>Hermes:Observer:MaxCalls</c> of different deliveries at once,
/// each claimed immediately before its <c>ObserveAsync</c>, which <c>Hermes:Dispatch:Timeout</c>
/// bounds, and deleted under that claim once the observer took it. A delivery whose notice was
/// not taken is left for the next run, so its later notices wait behind it.
/// </summary>
internal sealed partial class NoticeDelivery(
    MessagingDatabase database,
    IOptionsMonitor<MessagingOptions> options,
    IServiceProvider services,
    TimeProvider clock,
    ILogger<NoticeDelivery> logger)
{
    /// <summary>
    /// Delivers the pending notices until none is left that this run may deliver, calling
    /// <paramref name="renewed"/> after each and stopping before the next round where it answers
    /// false; answers the number the observer took.
    /// </summary>
    internal async Task<int> DeliverAsync(Func<CancellationToken, Task<bool>>? renewed, CancellationToken cancellationToken)
    {
        IDeliveryObserver observer = services.GetRequiredService<IDeliveryObserver>();
        Guid holder = Guid.CreateVersion7();
        HashSet<Guid> left = [];
        int taken = 0;
        bool held = true;
        while (held)
        {
            MessagingOptions current = options.CurrentValue;
            List<Pending> ready = await ReadyAsync(left, current.Observer.MaxCalls, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
            if (ready.Count == 0)
            {
                break;
            }

            bool[] outcomes = await Task.WhenAll(ready.Select(async pending =>
            {
                bool took = await DeliverAsync(observer, pending, holder, current, cancellationToken).ConfigureAwait(false);
                if (renewed is not null && !await renewed(cancellationToken).ConfigureAwait(false))
                {
                    held = false;
                }

                return took;
            })).ConfigureAwait(false);
            for (int index = 0; index < ready.Count; index++)
            {
                if (outcomes[index])
                {
                    taken++;
                }
                else
                {
                    left.Add(ready[index].Delivery);
                }
            }
        }

        return taken;
    }

    private async Task<List<Pending>> ReadyAsync(HashSet<Guid> left, int limit, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Guid[] skipped = [.. left];
        MessagingContext context = database.Context();
        await using (context.ConfigureAwait(false))
        {
            return await context.Notices
                .AsNoTracking()
                .Where(notice => (notice.ClaimedBy == null || notice.ClaimExpiresAt <= now)
                    && !skipped.Contains(notice.DeliveryId)
                    && !context.Notices.Any(lower => lower.DeliveryId == notice.DeliveryId && lower.Sequence < notice.Sequence))
                .OrderBy(notice => notice.At)
                .ThenBy(notice => notice.DeliveryId)
                .Take(limit)
                .Join(
                    context.Deliveries,
                    notice => notice.DeliveryId,
                    delivery => delivery.Id,
                    (notice, delivery) => new Pending(notice.DeliveryId, notice.Sequence, notice.Status, notice.Reason, notice.At, delivery.SendId, delivery.Kind, delivery.Category, delivery.Channel, delivery.Person))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>Claims, tells and deletes one notice, answering whether the observer took it.</summary>
    private async Task<bool> DeliverAsync(IDeliveryObserver observer, Pending pending, Guid holder, MessagingOptions current, CancellationToken cancellationToken)
    {
        // A notice in a channel, status or reason this release does not know waits for a release that does.
        if (Spellings.Channel(pending.Channel) is not { } channel
            || Spellings.Status(pending.Status) is not { } status
            || (pending.Reason is { } spelling && Spellings.Reason(spelling) is null))
        {
            return false;
        }

        if (!await ClaimAsync(pending, holder, current.Dispatch.ClaimTimeout, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        DeliveryNotice notice = new()
        {
            Send = new SendId(pending.Send),
            Delivery = new DeliveryId(pending.Delivery),
            Kind = pending.Kind,
            Category = pending.Category,
            Channel = channel,
            Person = pending.Person,
            Status = status,
            Reason = pending.Reason is { } reason ? Spellings.Reason(reason) : null,
            At = pending.At,
        };
        try
        {
            using CancellationTokenSource bound = new(current.Dispatch.Timeout, clock);
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(bound.Token, cancellationToken);
            await observer.ObserveAsync(notice, linked.Token).AsTask().WaitAsync(current.Dispatch.Timeout, clock, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception failure) when (failure is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            string exception = failure is TimeoutException ? "timeout" : failure.GetType().Name;
            NotTaken(logger, notice.Delivery.Value, exception);
            await ReleaseAsync(pending, holder, cancellationToken).ConfigureAwait(false);
            return false;
        }

        await DeleteAsync(pending, holder, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task<bool> ClaimAsync(Pending pending, Guid holder, TimeSpan claimTimeout, CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.GetUtcNow();
        MessagingContext context = database.Context();
        await using (context.ConfigureAwait(false))
        {
            int claimed = await context.Notices
                .Where(notice => notice.DeliveryId == pending.Delivery && notice.Sequence == pending.Sequence
                    && (notice.ClaimedBy == null || notice.ClaimExpiresAt <= now))
                .ExecuteUpdateAsync(set => set.SetProperty(notice => notice.ClaimedBy, holder).SetProperty(notice => notice.ClaimExpiresAt, now + claimTimeout), cancellationToken)
                .ConfigureAwait(false);
            return claimed == 1;
        }
    }

    private async Task ReleaseAsync(Pending pending, Guid holder, CancellationToken cancellationToken)
    {
        MessagingContext context = database.Context();
        await using (context.ConfigureAwait(false))
        {
            await context.Notices
                .Where(notice => notice.DeliveryId == pending.Delivery && notice.Sequence == pending.Sequence && notice.ClaimedBy == holder)
                .ExecuteUpdateAsync(set => set.SetProperty(notice => notice.ClaimedBy, (Guid?)null).SetProperty(notice => notice.ClaimExpiresAt, (DateTimeOffset?)null), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task DeleteAsync(Pending pending, Guid holder, CancellationToken cancellationToken)
    {
        MessagingContext context = database.Context();
        await using (context.ConfigureAwait(false))
        {
            await context.Notices
                .Where(notice => notice.DeliveryId == pending.Delivery && notice.Sequence == pending.Sequence && notice.ClaimedBy == holder)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    [LoggerMessage(EventId = 43, EventName = "NoticeNotTaken", Level = LogLevel.Warning, Message = "The observer did not take the notice of delivery {Delivery}: {Exception}; the next run tells it again.")]
    private static partial void NotTaken(ILogger logger, Guid delivery, string exception);

    private sealed record Pending(Guid Delivery, int Sequence, string Status, string? Reason, DateTimeOffset At, Guid Send, string Kind, string Category, string Channel, string? Person);
}