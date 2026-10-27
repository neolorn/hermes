using System;
using System.Collections.Generic;
using System.Linq;

using Hermes.Core;

namespace Hermes.Sending;

/// <summary>
/// A push delivery's outcome over its subscriptions (<c>02</c> MSG-PUSH-005): which of the
/// person's live subscriptions an attempt goes to, and how the delivery ends or retries from
/// their answers.
/// </summary>
internal static class PushOutcomes
{
    /// <summary>
    /// The subscriptions the attempt after <paramref name="lastAttempt"/> goes to, of the
    /// person's <paramref name="live"/> ones, judged over the delivery's recorded
    /// <paramref name="calls"/>, in order; where none is left, the status the delivery ends in
    /// and its reason. An attempt <paramref name="lost"/>, its claim lost or its pass stopped
    /// between its calls, is judged as one that made no call.
    /// </summary>
    internal static (IReadOnlyList<Guid> Targets, string? Ends, AttemptReason? Reason) Targets(IReadOnlyList<PushCall> calls, int lastAttempt, IReadOnlyList<Guid> live, bool lost = false)
    {
        if (lastAttempt == 0)
        {
            return live.Count > 0 ? (live, null, null) : ([], "nosubscription", null);
        }

        List<PushCall> last = [.. calls.Where(call => call.Attempt == lastAttempt)];
        if (last.Count == 0 || lost)
        {
            // A lost claim, or a transient answer at a check: every live subscription not yet refused.
            HashSet<Guid> refused = [.. calls.Where(call => call.Class is AttemptClass.Permanent or AttemptClass.Gone && call.Subscription is not null).Select(call => call.Subscription!.Value)];
            List<Guid> left = [.. live.Where(subscription => !refused.Contains(subscription))];
            if (left.Count > 0)
            {
                return (left, null, null);
            }

            return calls.LastOrDefault(call => call.Class == AttemptClass.Permanent) is { } permanent
                ? ([], "failed", permanent.Reason)
                : ([], "nosubscription", null);
        }

        HashSet<Guid> transient = [.. last.Where(call => call.Class == AttemptClass.Transient && call.Subscription is not null).Select(call => call.Subscription!.Value)];
        List<Guid> again = [.. live.Where(transient.Contains)];
        return again.Count > 0 ? (again, null, null) : ([], "nosubscription", null);
    }

    /// <summary>
    /// The attempt's class over its subscriptions' <paramref name="answers"/>, none for a call
    /// skipped since its subscription moved or went, which counts as gone: accepted where one
    /// was; transient where one was, honouring the longest <c>Retry-After</c>; gone where every
    /// one was; otherwise permanent, with the reason of the last permanent answer.
    /// </summary>
    internal static (AttemptClass Class, AttemptReason? Reason, TimeSpan? RetryAfter) Of(IReadOnlyList<Attempt?> answers)
    {
        if (answers.Any(answer => answer is Attempt.Accepted))
        {
            return (AttemptClass.Accepted, null, null);
        }

        List<Attempt.Transient> transient = [.. answers.OfType<Attempt.Transient>()];
        if (transient.Count > 0)
        {
            return (AttemptClass.Transient, transient[^1].Reason, transient.Max(answer => answer.RetryAfter));
        }

        if (answers.All(answer => answer is null or Attempt.Gone))
        {
            return (AttemptClass.Gone, AttemptReason.Gone, null);
        }

        return (AttemptClass.Permanent, answers.OfType<Attempt.Permanent>().Last().Reason, null);
    }
}