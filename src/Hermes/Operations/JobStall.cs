using System;

using Hermes.Channels;

namespace Hermes.Operations;

/// <summary>
/// When a job is stalled (<c>06</c> MSG-OPS-004; <c>08</c> section 5, <c>job-stalled</c>): one a
/// process has started that has neither completed nor renewed its claim for
/// <c>Hermes:Jobs:StallFactor</c> of its intervals plus <c>Hermes:Dispatch:Timeout</c>.
/// </summary>
internal static class JobStall
{
    /// <summary>The interval of the job <paramref name="name"/> as this process's configuration gives it.</summary>
    internal static TimeSpan Interval(string name, MessagingOptions options, ChannelsOptions channels)
    {
        return name switch
        {
            "balance" => channels.Sms.BalancePoll,
            "retention" => TimeSpan.FromDays(1),
            "subscriptions" => options.Jobs.SubscriptionsInterval,
            JobRow.ConversationTimers => JobRow.ConversationTimersInterval,
            _ => options.Dispatch.Interval,
        };
    }

    /// <summary>Whether the job of <paramref name="row"/>, of <paramref name="interval"/>, is stalled at <paramref name="now"/>.</summary>
    internal static bool Stalled(JobRow row, TimeSpan interval, double factor, TimeSpan timeout, DateTimeOffset now)
    {
        if (row.FirstStartedAt is not { } first)
        {
            return false;
        }

        DateTimeOffset last = first;
        foreach (DateTimeOffset? instant in new[] { row.LastCompletedAt, row.LastRenewedAt })
        {
            if (instant is { } at && at > last)
            {
                last = at;
            }
        }

        return now - last > (interval * factor) + timeout;
    }
}