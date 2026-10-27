using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Channels;
using Hermes.Content;
using Hermes.Core;
using Hermes.Operations;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hermes.Sending;

/// <summary>
/// One pass of one channel (<c>01</c> MSG-SEND-006, MSG-SEND-007, MSG-SEND-015, MSG-SEND-016):
/// it claims, then for each delivery in order runs the checks this release builds, renews its
/// claim, calls the provider and records the answer, every write conditional on its claim. A
/// stop hands back each delivery whose renewal has not begun, and lets a call in progress
/// finish within one <c>Hermes:Dispatch:Timeout</c> of it.
/// </summary>
internal sealed partial class DispatchPass : IDisposable
{
    private static readonly HashSet<string> Finals =
    [
        "sent", "delivered", "failed", "cancelled", "expired", "suppressed", "optedout", "noconsent", "limited", "balancefloor", "nosubscription",
    ];

    private static readonly HashSet<AttemptReason> SubmissionFailures = [AttemptReason.TlsFailed, AttemptReason.AuthFailed, AttemptReason.SenderRefused, AttemptReason.ServerRefused];

    private readonly DispatchPasses passes;

    private readonly Channel channel;

    private readonly string spelling;

    private readonly CancellationToken stopping;

    private readonly Action<int, int>? claimedNow;

    private readonly MessagingOptions current;

    private readonly Guid holder = Guid.CreateVersion7();

    private readonly Lock gate = new();

    private readonly List<ClaimedDelivery> claimed = [];

    /// <summary>Cancelled one <c>Hermes:Dispatch:Timeout</c> after a stop: the deadline of whatever is still in progress.</summary>
    private readonly CancellationTokenSource finish;

    private DateTimeOffset second;

    private DateTimeOffset? stoppedAt;

    private Task handingBack = Task.CompletedTask;

    private bool heldForKey;

    private IEmailSession? session;

    internal DispatchPass(DispatchPasses passes, Channel channel, Action<int, int>? claimed, CancellationToken stopping)
    {
        claimedNow = claimed;
        this.passes = passes;
        this.channel = channel;
        this.stopping = stopping;
        spelling = Spellings.Of(channel);
        current = passes.Options.CurrentValue;
        finish = new CancellationTokenSource(Timeout.InfiniteTimeSpan, passes.Clock);
    }

    private ILogger Logger => passes.Logger;

    private TimeProvider Clock => passes.Clock;

    internal async Task<PassOutcome> RunAsync()
    {
        DateTimeOffset started = Clock.GetUtcNow();
        (List<DeliveryRow> rows, DateTimeOffset claimSecond, int limit) = await passes.ClaimAsync(channel, holder, current, CancellationToken.None).ConfigureAwait(false);
        claimedNow?.Invoke(rows.Count, limit);
        if (rows.Count == 0)
        {
            return new PassOutcome(0, limit, HeldForKey: false, Stopped: stopping.IsCancellationRequested);
        }

        second = claimSecond;
        Claimed(Logger, spelling, rows.Count);
        lock (gate)
        {
            claimed.AddRange(rows.Select(row => new ClaimedDelivery(row)));
        }

        using CancellationTokenRegistration registration = stopping.Register(Stop);
        AsyncServiceScope scope = passes.Scopes.CreateAsyncScope();
        try
        {
            await using (scope.ConfigureAwait(false))
            {
                foreach (ClaimedDelivery delivery in claimed)
                {
                    if (!Begin(delivery))
                    {
                        continue;
                    }

                    if (Clock.GetUtcNow() - started >= current.Dispatch.Interval)
                    {
                        await HandBackAsync([delivery, .. Unreached()]).ConfigureAwait(false);
                        break;
                    }

                    await WorkAsync(delivery, scope.ServiceProvider).ConfigureAwait(false);
                    lock (gate)
                    {
                        delivery.Stage = ClaimStage.Done;
                    }
                }
            }
        }
        finally
        {
            await handingBack.ConfigureAwait(false);
            if (session is not null)
            {
                await DisposeSessionAsync(session).ConfigureAwait(false);
            }
        }

        return new PassOutcome(rows.Count, limit, heldForKey, stoppedAt is not null);
    }

    public void Dispose()
    {
        finish.Dispose();
    }

    private bool Begin(ClaimedDelivery delivery)
    {
        lock (gate)
        {
            if (delivery.Stage != ClaimStage.NotReached)
            {
                return false;
            }

            delivery.Stage = ClaimStage.Checking;
            return true;
        }
    }

    private List<ClaimedDelivery> Unreached()
    {
        lock (gate)
        {
            List<ClaimedDelivery> left = [.. claimed.Where(delivery => delivery.Stage == ClaimStage.NotReached)];
            left.ForEach(delivery => delivery.Stage = ClaimStage.Done);
            return left;
        }
    }

    /// <summary>The host stopped the pass: every delivery whose renewal has not begun goes back at once, and what is in progress has one timeout left.</summary>
    private void Stop()
    {
        List<ClaimedDelivery> back;
        lock (gate)
        {
            stoppedAt = Clock.GetUtcNow();
            back = [.. claimed.Where(delivery => delivery.Stage is ClaimStage.NotReached or ClaimStage.Checking)];
            back.ForEach(delivery => delivery.Stage = ClaimStage.Done);
        }

        finish.CancelAfter(current.Dispatch.Timeout);
        handingBack = Task.Run(() => HandBackAsync(back));
    }

    /// <summary>Whether the delivery's renewal may begin: false where a stop has handed it back.</summary>
    private bool BeginRenewal(ClaimedDelivery delivery)
    {
        lock (gate)
        {
            if (delivery.Stage != ClaimStage.Checking)
            {
                return false;
            }

            delivery.Stage = ClaimStage.Renewing;
            return true;
        }
    }

    /// <summary>The <c>lock_timeout</c> of a write now: the dispatch timeout, or what a stop left of it; none where nothing is left.</summary>
    private TimeSpan? LockTimeout()
    {
        if (stoppedAt is not { } stopped)
        {
            return current.Dispatch.Timeout;
        }

        TimeSpan left = stopped + current.Dispatch.Timeout - Clock.GetUtcNow();
        return left > TimeSpan.Zero ? left : null;
    }

    private async Task HandBackAsync(List<ClaimedDelivery> back)
    {
        if (back.Count == 0 || LockTimeout() is not { } timeout)
        {
            return;
        }

        try
        {
            List<Guid> returned = await LibraryTransactions.RunAsync(
                passes.Database,
                current,
                async (context, token) =>
                {
                    await DeliveryClaims.LockTimeoutAsync(context, timeout, token).ConfigureAwait(false);
                    List<Guid> ids = await DeliveryClaims.HandBackAsync(context, [.. back.Select(delivery => delivery.Row.Id)], holder, Clock.GetUtcNow(), token).ConfigureAwait(false);
                    if (ids.Count > 0)
                    {
                        await DeliveryClaims.CountAsync(context, spelling, second, -ids.Count, token).ConfigureAwait(false);
                    }

                    return ids;
                },
                finish.Token).ConfigureAwait(false);
            HandedBack(Logger, spelling, returned.Count);
        }
        catch (Exception failure) when (DispatchPasses.IsLockTimeout(failure) || failure is OperationCanceledException)
        {
            HandBackFailed(Logger, spelling, failure.GetType().Name);
        }
    }

    private async Task WorkAsync(ClaimedDelivery delivery, IServiceProvider services)
    {
        DeliveryRow row = delivery.Row;
        if (row.Attempts > 0)
        {
            if (!await ExtendAsync(delivery).ConfigureAwait(false))
            {
                return;
            }

            Lost lost = await LostAsync(delivery).ConfigureAwait(false);
            if (lost != Lost.NotLost)
            {
                return;
            }
        }

        KindDeclaration? kind = passes.Kind(row.Kind);
        if (kind is null)
        {
            await EndAsync(delivery, "cancelled", reason: null, giveBack: true).ConfigureAwait(false);
            return;
        }

        if (await OpenAsync(delivery, kind, services).ConfigureAwait(false) is not { } values)
        {
            return;
        }

        if (!await ExtendAsync(delivery).ConfigureAwait(false))
        {
            return;
        }

        if (Expiry(kind, row) is { } expiry && Clock.GetUtcNow() > expiry)
        {
            await EndAsync(delivery, "expired", reason: null, giveBack: true).ConfigureAwait(false);
            return;
        }

        CategoryDeclaration? category = passes.Category(row.Category);
        if (category is { Essential: false } && !await OptionalChecksAsync(delivery, kind, category, values).ConfigureAwait(false))
        {
            return;
        }

        if (channel == Channel.Sms && !SenderRule.Allows(passes.Channels.CurrentValue.Sms.AllowedPrefixes, values["destination"]!))
        {
            await EndAtCheckAsync(delivery, AttemptReason.DestinationNotAllowed).ConfigureAwait(false);
            return;
        }

        if (!BeginRenewal(delivery))
        {
            return;
        }

        string reference = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));
        if (await RenewAsync(delivery, reference).ConfigureAwait(false) is not { } number)
        {
            return;
        }

        if (await CallAsync(delivery, kind, values, reference, services).ConfigureAwait(false) is { } answer)
        {
            await RecordAsync(delivery, kind, number, answer).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Runs the checks an optional category's delivery meets and an essential one's does not,
    /// each against the state at that moment: 3, the suppression of an email or SMS destination.
    /// Answers whether the delivery goes on; a check that ends or holds it gives its place in the
    /// rate back.
    /// </summary>
    private async Task<bool> OptionalChecksAsync(ClaimedDelivery delivery, KindDeclaration kind, CategoryDeclaration category, Dictionary<string, string?> values)
    {
        if (channel is Channel.Email or Channel.Sms && Suppressions.Value(channel, values["destination"]) is { } destination)
        {
            if (!await ExtendAsync(delivery).ConfigureAwait(false))
            {
                return false;
            }

            (bool read, (bool suppressed, int? missing)) = await ReadAsync((context, token) => Suppressions.LookUpAsync(context, passes.Hashes, channel, category.Name, destination, token)).ConfigureAwait(false);
            if (!read)
            {
                return false;
            }

            if (missing is not null)
            {
                await HoldAsync(delivery, kind, KeyNeeds.HashKeys).ConfigureAwait(false);
                return false;
            }

            if (suppressed)
            {
                await EndAsync(delivery, "suppressed", reason: null, giveBack: true).ConfigureAwait(false);
                return false;
            }
        }

        return true;
    }

    /// <summary>Reads in a transaction of its own, under the pass's lock timeout; answers false where a stop left no time or a lock timed out.</summary>
    private async Task<(bool Read, T Value)> ReadAsync<T>(Func<MessagingContext, CancellationToken, Task<T>> read)
    {
        if (LockTimeout() is not { } timeout)
        {
            return (false, default!);
        }

        try
        {
            T value = await LibraryTransactions.RunAsync(
                passes.Database,
                current,
                async (context, token) =>
                {
                    await DeliveryClaims.LockTimeoutAsync(context, timeout, token).ConfigureAwait(false);
                    return await read(context, token).ConfigureAwait(false);
                },
                finish.Token).ConfigureAwait(false);
            return (true, value);
        }
        catch (Exception failure) when (DispatchPasses.IsLockTimeout(failure) || (failure is OperationCanceledException && finish.IsCancellationRequested))
        {
            return (false, default!);
        }
    }

    /// <summary>
    /// Opens the delivery's destination and content (check 1), extending the claim before
    /// each; answers none where a value ends or holds the delivery, or the claim is lost.
    /// </summary>
    private async Task<Dictionary<string, string?>?> OpenAsync(ClaimedDelivery delivery, KindDeclaration kind, IServiceProvider services)
    {
        ContentProtection protection = services.GetRequiredService<ContentProtection>();
        Dictionary<string, string?> values = new(StringComparer.Ordinal);
        foreach ((string field, byte[]? stored) in Fields(delivery.Row))
        {
            if (stored is null)
            {
                values[field] = null;
                continue;
            }

            if (!await ExtendAsync(delivery).ConfigureAwait(false))
            {
                return null;
            }

            ContentOpening opening;
            string context = ContentContext.Of(ContentContext.Deliveries, delivery.Row.Id, field);
            try
            {
                using CancellationTokenSource bound = new(current.Dispatch.Timeout, Clock);
                using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(bound.Token, finish.Token);
                opening = await protection.OpenAsync(delivery.Row.Person, stored, context, linked.Token).AsTask().WaitAsync(current.Dispatch.Timeout, Clock, finish.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (finish.IsCancellationRequested)
            {
                return null;
            }
            catch (Exception failure) when (!finish.IsCancellationRequested)
            {
                SeamFailed(Logger, "content protector", failure.GetType().Name, delivery.Row.Id);
                opening = ContentOpening.Held(ContentEnvelope.Read(stored).Id);
            }

            switch (opening.Status)
            {
                case ContentOpeningStatus.Opened:
                    values[field] = Encoding.UTF8.GetString(opening.Plaintext.Span);
                    break;
                case ContentOpeningStatus.Erased:
                    await EndAsync(delivery, "cancelled", reason: null, giveBack: true).ConfigureAwait(false);
                    return null;
                case ContentOpeningStatus.Rejected:
                    if (await EndAsync(delivery, "failed", AttemptReason.ValueRejected, giveBack: true).ConfigureAwait(false))
                    {
                        await AlertAsync(() => passes.Alerts.HoldsUntilAsync("content-rejected", ContentContext.Deliveries, new Dictionary<string, string> { ["table"] = ContentContext.Deliveries }, CancellationToken.None)).ConfigureAwait(false);
                    }

                    return null;
                default:
                    await HoldAsync(delivery, kind, opening.Key).ConfigureAwait(false);
                    return null;
            }
        }

        return values;
    }

    private IEnumerable<(string Field, byte[]? Stored)> Fields(DeliveryRow row)
    {
        yield return ("destination", row.Destination);
        if (channel == Channel.Email)
        {
            yield return ("subject", row.Subject);
        }

        yield return ("text", row.Text);
        if (channel == Channel.Email)
        {
            yield return ("html", row.Html);
        }
    }

    private static DateTimeOffset? Expiry(KindDeclaration? kind, DeliveryRow row)
    {
        return kind?.ExpiresAfter is { } after ? row.RecordedAt + after : null;
    }

    /// <summary>Extends the claim where it is older than half the dispatch timeout; false where the claim is no longer the pass's.</summary>
    private async Task<bool> ExtendAsync(ClaimedDelivery delivery)
    {
        if (Clock.GetUtcNow() - delivery.ClaimedAt <= current.Dispatch.Timeout / 2)
        {
            return true;
        }

        (bool held, DateTimeOffset at) = await UnderClaimAsync(delivery, (_, row, now) =>
        {
            row.ClaimedAt = now;
            row.ClaimExpiresAt = now + current.Dispatch.ClaimTimeout;
            return Task.FromResult(now);
        }).ConfigureAwait(false);
        if (held)
        {
            delivery.ClaimedAt = at;
        }

        return held;
    }

    /// <summary>
    /// Records, where the delivery's last counted attempt has no answer, that attempt
    /// <c>transient</c> with the reason <c>claimlost</c>, and ends or retries the delivery.
    /// </summary>
    private async Task<Lost> LostAsync(ClaimedDelivery delivery)
    {
        KindDeclaration? kind = passes.Kind(delivery.Row.Kind);
        (bool held, Lost lost) = await UnderClaimAsync(delivery, async (context, row, now) =>
        {
            AttemptRow? attempt = await context.Attempts.FindAsync([row.Id, row.Attempts], finish.Token).ConfigureAwait(false);
            if (attempt is null || attempt.Class is not null)
            {
                return Lost.NotLost;
            }

            attempt.Class = Spellings.Of(AttemptClass.Transient);
            attempt.Reason = Spellings.Of(AttemptReason.ClaimLost);
            Retry(context, row, kind, row.Attempts, AttemptReason.ClaimLost, retryAfter: null, now);
            return Finals.Contains(row.Status) ? Lost.Ended : Lost.Retried;
        }).ConfigureAwait(false);
        if (!held)
        {
            return Lost.Skipped;
        }

        if (lost != Lost.NotLost)
        {
            CountAttempt(AttemptClass.Transient, AttemptReason.ClaimLost);
            AttemptLost(Logger, delivery.Row.Id, spelling);
        }

        if (lost == Lost.Ended)
        {
            Ended(delivery.Row, "failed", AttemptReason.ClaimLost);
        }

        return lost;
    }

    /// <summary>
    /// Holds the delivery for a key: back to <c>waiting</c>, due after <c>Retry:Initial</c>, no
    /// attempt counted; or <c>expired</c> where that falls after its expiry, or <c>failed</c>
    /// where its run of holds began <c>Dispatch:MaxHold</c> or more ago.
    /// </summary>
    private async Task HoldAsync(ClaimedDelivery delivery, KindDeclaration? kind, string? key)
    {
        (bool held, string status) = await UnderClaimAsync(delivery, async (context, row, now) =>
        {
            DateTimeOffset next = now + current.Retry.Initial;
            if (Expiry(kind, row) is { } expiry && next > expiry)
            {
                End(context, row, "expired", reason: null, now);
            }
            else if (row.FirstHeldAt is { } first && now - first >= current.Dispatch.MaxHold)
            {
                End(context, row, "failed", AttemptReason.KeyUnavailable, now);
            }
            else
            {
                row.Status = "waiting";
                row.Reason = Spellings.Of(AttemptReason.KeyUnavailable);
                row.DueAt = next;
                row.FirstHeldAt ??= now;
                DeliveryClaims.Release(row);
            }

            await DeliveryClaims.CountAsync(context, spelling, second, -1, finish.Token).ConfigureAwait(false);
            return row.Status;
        }).ConfigureAwait(false);
        if (!held)
        {
            return;
        }

        if (status == "waiting")
        {
            heldForKey = true;
            Held(Logger, delivery.Row.Id, delivery.Row.Kind, spelling, key ?? "a later release's format");
        }
        else
        {
            Ended(delivery.Row, status, status == "failed" ? AttemptReason.KeyUnavailable : null);
        }

        await AlertAsync(() => passes.Alerts.HoldsUntilAsync("key-unavailable", key, new Dictionary<string, string>(), CancellationToken.None)).ConfigureAwait(false);
    }

    /// <summary>Ends the delivery at a check, giving its place in the rate back where the check is one of 1 to 5; false where the claim is no longer the pass's.</summary>
    private async Task<bool> EndAsync(ClaimedDelivery delivery, string status, AttemptReason? reason, bool giveBack)
    {
        (bool held, _) = await UnderClaimAsync(delivery, async (context, row, now) =>
        {
            End(context, row, status, reason, now);
            if (giveBack)
            {
                await DeliveryClaims.CountAsync(context, spelling, second, -1, finish.Token).ConfigureAwait(false);
            }

            return true;
        }).ConfigureAwait(false);
        if (held)
        {
            Ended(delivery.Row, status, reason);
        }

        return held;
    }

    /// <summary>Ends the delivery <c>failed</c> at the channel's own check, an attempt answered there with no call.</summary>
    private async Task EndAtCheckAsync(ClaimedDelivery delivery, AttemptReason reason)
    {
        (bool held, _) = await UnderClaimAsync(delivery, (context, row, now) =>
        {
            row.Attempts++;
            row.FirstHeldAt = null;
            context.Attempts.Add(new AttemptRow { DeliveryId = row.Id, Number = row.Attempts, At = now, Class = Spellings.Of(AttemptClass.Permanent), Reason = Spellings.Of(reason) });
            End(context, row, "failed", reason, now);
            return Task.FromResult(true);
        }).ConfigureAwait(false);
        if (held)
        {
            CountAttempt(AttemptClass.Permanent, reason);
            Ended(delivery.Row, "failed", reason);
        }
    }

    /// <summary>Renews the claim immediately before the call, counting the attempt and recording its delivery reference; answers its number, or none where the claim is lost.</summary>
    private async Task<int?> RenewAsync(ClaimedDelivery delivery, string reference)
    {
        (bool held, int number) = await UnderClaimAsync(delivery, (context, row, now) =>
        {
            row.ClaimedAt = now;
            row.ClaimExpiresAt = now + current.Dispatch.ClaimTimeout;
            row.Attempts++;
            row.FirstHeldAt = null;
            context.Attempts.Add(new AttemptRow { DeliveryId = row.Id, Number = row.Attempts, Reference = reference, At = now });
            return Task.FromResult(row.Attempts);
        }).ConfigureAwait(false);
        return held ? number : null;
    }

    /// <summary>Calls the provider, bounded by the dispatch timeout and a stop's deadline; answers none where the stop left no time to record the answer.</summary>
    private async Task<Attempt?> CallAsync(ClaimedDelivery delivery, KindDeclaration kind, Dictionary<string, string?> values, string reference, IServiceProvider services)
    {
        DeliveryRow row = delivery.Row;
        long started = Stopwatch.GetTimestamp();
        Attempt answer;
        using CancellationTokenSource bound = new(current.Dispatch.Timeout, Clock);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(bound.Token, finish.Token);
        try
        {
            Task<Attempt> call = channel == Channel.Email
                ? SendEmailAsync(row, kind, values, services, linked.Token)
                : SendSmsAsync(kind, values, reference, services, linked.Token);
            answer = await call.WaitAsync(current.Dispatch.Timeout, Clock, finish.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (finish.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception failure) when (failure is TimeoutException || (failure is OperationCanceledException && bound.IsCancellationRequested))
        {
            answer = new Attempt.Transient(AttemptReason.Timeout, null, null);
        }
        catch (Exception failure) when (!finish.IsCancellationRequested)
        {
            ProviderFaulted(Logger, spelling, failure.GetType().Name, row.Id);
            answer = new Attempt.Transient(AttemptReason.ProviderFault, null, null);
        }

        passes.Metrics.ProviderDuration.Record(
            Stopwatch.GetElapsedTime(started).TotalSeconds,
            new KeyValuePair<string, object?>("channel", spelling),
            new KeyValuePair<string, object?>("class", Spellings.Of(Class(answer))));
        return answer;
    }

    private async Task<Attempt> SendEmailAsync(DeliveryRow row, KindDeclaration kind, Dictionary<string, string?> values, IServiceProvider services, CancellationToken cancellationToken)
    {
        EmailFromOptions defaults = passes.Channels.CurrentValue.Email.From;
        EmailAddress from = HeaderText.Spaced(kind.Email?.From ?? new EmailAddress { Address = defaults.Address!, DisplayName = defaults.DisplayName })!;
        session ??= await services.GetRequiredService<IEmailProvider>().OpenAsync(cancellationToken).ConfigureAwait(false);
        EmailMessage message = new()
        {
            From = from,
            ReplyTo = HeaderText.Spaced(kind.Email?.ReplyTo),
            ToAddress = values["destination"]!,
            Subject = values["subject"]!,
            Text = values["text"]!,
            Html = values["html"],
            MessageId = string.Create(CultureInfo.InvariantCulture, $"{row.Id:D}@{from.Address[(from.Address.LastIndexOf('@') + 1)..]}"),
            Language = row.Language,
        };
        return await session.SendAsync(message, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Attempt> SendSmsAsync(KindDeclaration kind, Dictionary<string, string?> values, string reference, IServiceProvider services, CancellationToken cancellationToken)
    {
        string text = values["text"]!;
        SmsMeasure measure = SmsText.Measure(text);
        SmsMessage message = new()
        {
            ToNumber = values["destination"]!,
            Sender = kind.Sms?.Sender ?? passes.Channels.CurrentValue.Sms.Sender!,
            Text = text,
            DeliveryReference = reference,
            Encoding = measure.Encoding,
            Segments = measure.Segments,
        };
        return await services.GetRequiredService<ISmsProvider>().SendAsync(message, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Records the answer of the attempt <paramref name="number"/> and the status it leads to, in what a stop left of the timeout.</summary>
    private async Task RecordAsync(ClaimedDelivery delivery, KindDeclaration kind, int number, Attempt answer)
    {
        AttemptClass answerClass = Class(answer);
        (AttemptReason? reason, int? status, TimeSpan? retryAfter, string? providerReference) = answer switch
        {
            Attempt.Accepted accepted => ((AttemptReason?)null, (int?)null, (TimeSpan?)null, accepted.ProviderReference),
            Attempt.Transient transient => (transient.Reason, transient.Status, transient.RetryAfter, null),
            Attempt.Permanent permanent => (permanent.Reason, permanent.Status, null, null),
            Attempt.Gone gone => (gone.Reason, gone.Status, null, null),
            _ => throw new UnreachableException(),
        };
        (bool held, (string Status, DateTimeOffset Due) moved) = await UnderClaimAsync(delivery, async (context, row, now) =>
        {
            AttemptRow? attempt = await context.Attempts.FindAsync([row.Id, number], finish.Token).ConfigureAwait(false);
            attempt!.Class = Spellings.Of(answerClass);
            attempt.Reason = reason is null ? null : Spellings.Of(reason.Value);
            context.Calls.Add(new CallRow
            {
                DeliveryId = row.Id,
                Attempt = number,
                Number = 1,
                Class = Spellings.Of(answerClass),
                Reason = attempt.Reason,
                Status = status,
                At = now,
            });
            switch (answerClass)
            {
                case AttemptClass.Accepted:
                    row.ProviderReference = providerReference;
                    End(context, row, "sent", reason: null, now);
                    break;
                case AttemptClass.Transient:
                    Retry(context, row, kind, number, reason!.Value, retryAfter, now);
                    break;
                default:
                    End(context, row, "failed", reason, now);
                    break;
            }

            return (row.Status, row.DueAt);
        }).ConfigureAwait(false);
        if (!held)
        {
            return;
        }

        CountAttempt(answerClass, reason);
        if (Finals.Contains(moved.Status))
        {
            Ended(delivery.Row, moved.Status, moved.Status == "failed" ? reason : null);
        }
        else
        {
            string answered = Spellings.Of(answerClass);
            string why = reason is null ? "none" : Spellings.Of(reason.Value);
            Retried(Logger, delivery.Row.Id, spelling, answered, why, moved.Due);
        }

        if (channel == Channel.Email)
        {
            if (answerClass == AttemptClass.Accepted)
            {
                await AlertAsync(() => passes.Alerts.NotHoldingAsync("email-submission-failing", null, CancellationToken.None)).ConfigureAwait(false);
            }
            else if (answerClass == AttemptClass.Transient && SubmissionFailures.Contains(reason!.Value))
            {
                await AlertAsync(() => passes.Alerts.HoldingAsync("email-submission-failing", null, new Dictionary<string, string> { ["reason"] = Spellings.Of(reason.Value) }, CancellationToken.None)).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// The delivery answered transient at its <paramref name="attempt"/>th attempt: <c>failed</c>
    /// where its attempts are used up, <c>expired</c> where the next would fall after its expiry,
    /// and otherwise <c>waiting</c>, due when <see cref="RetrySchedule"/> gives.
    /// </summary>
    private void Retry(MessagingContext context, DeliveryRow row, KindDeclaration? kind, int attempt, AttemptReason reason, TimeSpan? retryAfter, DateTimeOffset now)
    {
        if (attempt >= current.Retry.MaxAttempts)
        {
            End(context, row, "failed", reason, now);
            return;
        }

        DateTimeOffset next = RetrySchedule.Next(current.Retry, attempt, retryAfter, now, RandomNumberGenerator.GetInt32(int.MaxValue) / (double)int.MaxValue);
        if (Expiry(kind, row) is { } expiry && next > expiry)
        {
            End(context, row, "expired", reason: null, now);
            return;
        }

        row.Status = "waiting";
        row.Reason = Spellings.Of(reason);
        row.DueAt = next;
        row.BecameDueAt = next;
        DeliveryClaims.Release(row);
    }

    /// <summary>Moves the row to the final <paramref name="status"/>, writing its notice where this application observes.</summary>
    private void End(MessagingContext context, DeliveryRow row, string status, AttemptReason? reason, DateTimeOffset now)
    {
        row.Status = status;
        row.Reason = reason is null ? null : Spellings.Of(reason.Value);
        row.LastMovedAt = now;
        DeliveryClaims.Release(row);
        if (current.Observer.Enabled)
        {
            Notices.Add(context, row.Id, row.NextSequence, status, row.Reason, now);
            row.NextSequence++;
        }
    }

    /// <summary>
    /// Runs <paramref name="change"/> on the delivery's row, locked, in one transaction under the
    /// pass's lock timeout, where the row is still under the pass's claim; answers whether it was.
    /// </summary>
    private async Task<(bool Held, T Value)> UnderClaimAsync<T>(ClaimedDelivery delivery, Func<MessagingContext, DeliveryRow, DateTimeOffset, Task<T>> change)
    {
        if (LockTimeout() is not { } timeout)
        {
            return (false, default!);
        }

        try
        {
            return await LibraryTransactions.RunAsync(
                passes.Database,
                current,
                async (context, token) =>
                {
                    await DeliveryClaims.LockTimeoutAsync(context, timeout, token).ConfigureAwait(false);
                    DeliveryRow? row = await DeliveryClaims.LockAsync(context, delivery.Row.Id, token).ConfigureAwait(false);
                    DateTimeOffset now = Clock.GetUtcNow();
                    if (row is null || !DeliveryClaims.IsHeld(row, holder, now))
                    {
                        return (false, default(T)!);
                    }

                    T value = await change(context, row, now).ConfigureAwait(false);
                    await context.SaveChangesAsync(token).ConfigureAwait(false);
                    return (true, value);
                },
                finish.Token).ConfigureAwait(false);
        }
        catch (Exception failure) when (DispatchPasses.IsLockTimeout(failure))
        {
            LockTimedOut(Logger, delivery.Row.Id, spelling);
            return (false, default!);
        }
        catch (OperationCanceledException) when (finish.IsCancellationRequested)
        {
            return (false, default!);
        }
    }

    private void Ended(DeliveryRow row, string status, AttemptReason? reason)
    {
        passes.Notices.Count(spelling, status);

        DeliveryEnded(Logger, row.Id, row.Kind, row.Category, spelling, status, reason is null ? "none" : Spellings.Of(reason.Value));
    }

    private void CountAttempt(AttemptClass answerClass, AttemptReason? reason)
    {
        passes.Metrics.Attempts.Add(
            1,
            new KeyValuePair<string, object?>("channel", spelling),
            new KeyValuePair<string, object?>("class", Spellings.Of(answerClass)),
            new KeyValuePair<string, object?>("reason", reason is null ? null : Spellings.Of(reason.Value)));
    }

    /// <summary>Tells the alerts; a failure there is logged by its type, and the pass goes on.</summary>
    private async Task AlertAsync(Func<Task> alert)
    {
        try
        {
            await alert().ConfigureAwait(false);
        }
        catch (Exception failure) when (failure is not OperationCanceledException || !finish.IsCancellationRequested)
        {
            SeamFailed(Logger, "alert", failure.GetType().Name, Guid.Empty);
        }
    }

    private async Task DisposeSessionAsync(IEmailSession open)
    {
        try
        {
            await open.DisposeAsync().AsTask().WaitAsync(current.Dispatch.Timeout, Clock).ConfigureAwait(false);
        }
        catch (Exception failure) when (failure is not OperationCanceledException || !finish.IsCancellationRequested)
        {
            ProviderFaulted(Logger, spelling, failure.GetType().Name, Guid.Empty);
        }
    }

    private static AttemptClass Class(Attempt answer)
    {
        return answer switch
        {
            Attempt.Accepted => AttemptClass.Accepted,
            Attempt.Transient => AttemptClass.Transient,
            Attempt.Permanent => AttemptClass.Permanent,
            _ => AttemptClass.Gone,
        };
    }

    [LoggerMessage(EventId = 31, EventName = "PassClaimed", Level = LogLevel.Debug, Message = "A pass of {Channel} claimed {Count} deliveries.")]
    private static partial void Claimed(ILogger logger, string channel, int count);

    [LoggerMessage(EventId = 32, EventName = "DeliveryEnded", Level = LogLevel.Information, Message = "Delivery {Delivery} of kind {Kind}, category {Category}, on {Channel} ended {Status}, reason {Reason}.")]
    private static partial void DeliveryEnded(ILogger logger, Guid delivery, string kind, string category, string channel, string status, string reason);

    [LoggerMessage(EventId = 33, EventName = "DeliveryHeld", Level = LogLevel.Warning, Message = "Delivery {Delivery} of kind {Kind} on {Channel} is held for a key: {Key}.")]
    private static partial void Held(ILogger logger, Guid delivery, string kind, string channel, string key);

    [LoggerMessage(EventId = 34, EventName = "DeliveryRetried", Level = LogLevel.Information, Message = "Delivery {Delivery} on {Channel} answered {Class}, reason {Reason}; it is next due at {Due}.")]
    private static partial void Retried(ILogger logger, Guid delivery, string channel, string @class, string reason, DateTimeOffset due);

    [LoggerMessage(EventId = 35, EventName = "AttemptLost", Level = LogLevel.Warning, Message = "Delivery {Delivery} on {Channel} had an attempt whose holder stopped before recording its answer.")]
    private static partial void AttemptLost(ILogger logger, Guid delivery, string channel);

    [LoggerMessage(EventId = 36, EventName = "SeamFailed", Level = LogLevel.Warning, Message = "The {Seam} threw {Exception} for delivery {Delivery}.")]
    private static partial void SeamFailed(ILogger logger, string seam, string exception, Guid delivery);

    [LoggerMessage(EventId = 37, EventName = "ProviderFaulted", Level = LogLevel.Warning, Message = "The {Channel} provider threw {Exception} for delivery {Delivery}.")]
    private static partial void ProviderFaulted(ILogger logger, string channel, string exception, Guid delivery);

    [LoggerMessage(EventId = 38, EventName = "DeliveryLockTimedOut", Level = LogLevel.Warning, Message = "A row lock for delivery {Delivery} on {Channel} timed out; it waits for its claim to expire.")]
    private static partial void LockTimedOut(ILogger logger, Guid delivery, string channel);

    [LoggerMessage(EventId = 39, EventName = "PassHandedBack", Level = LogLevel.Debug, Message = "A pass of {Channel} handed back {Count} deliveries.")]
    private static partial void HandedBack(ILogger logger, string channel, int count);

    [LoggerMessage(EventId = 40, EventName = "PassHandBackFailed", Level = LogLevel.Warning, Message = "A pass of {Channel} could not hand its deliveries back ({Exception}); they wait for their claims to expire.")]
    private static partial void HandBackFailed(ILogger logger, string channel, string exception);

    private enum Lost
    {
        NotLost,
        Retried,
        Ended,
        Skipped,
    }
}