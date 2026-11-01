using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Conversations.Support;
using Hermes.Core;
using Hermes.Operations;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace Hermes.Conversations.Chats;

/// <summary>
/// A topic's supervision (<c>09</c> MSG-CONV-005): its supervisors, the members of its
/// <c>Supervisors</c> scope asked on every call, read its conversations, notes included, its
/// live view and its reports; anyone else is answered as for a conversation unknown. The
/// reports are computed when read, from the conversations closed within the period.
/// </summary>
internal sealed partial class StaffChats
{
    /// <summary>The longest period of a report, in days, both ends included.</summary>
    internal const int MostReportDays = 92;

    // percentile_cont answers double precision; the times are in seconds.
    private const string Figures = """
        SELECT GROUPING(c.assignee) = 1 AS "Total", c.assignee AS "Person", count(*)::integer AS "Closed",
               percentile_cont(0.5) WITHIN GROUP (ORDER BY extract(epoch FROM c.assigned_at - c.queued_at)) FILTER (WHERE c.assigned_at IS NOT NULL) AS "WaitMedian",
               percentile_cont(0.9) WITHIN GROUP (ORDER BY extract(epoch FROM c.assigned_at - c.queued_at)) FILTER (WHERE c.assigned_at IS NOT NULL) AS "WaitP90",
               percentile_cont(0.5) WITHIN GROUP (ORDER BY extract(epoch FROM c.closed_at - c.assigned_at)) FILTER (WHERE c.assigned_at IS NOT NULL) AS "HandleMedian",
               percentile_cont(0.9) WITHIN GROUP (ORDER BY extract(epoch FROM c.closed_at - c.assigned_at)) FILTER (WHERE c.assigned_at IS NOT NULL) AS "HandleP90",
               count(c.survey_at)::integer AS "Surveys",
               avg(CASE WHEN c.survey_resolved THEN 1.0 ELSE 0.0 END) FILTER (WHERE c.survey_at IS NOT NULL) AS "ResolvedShare",
               avg(c.survey_rating::numeric) FILTER (WHERE c.survey_at IS NOT NULL) AS "MeanRating"
        FROM messaging.conversations AS c
        WHERE c.type = 'support' AND c.topic = @topic AND c.status = 'closed' AND c.closed_at >= @start AND c.closed_at < @end
        GROUP BY GROUPING SETS ((), (c.assignee))
        """;

    public async ValueTask<Result<IReadOnlyList<LiveConversation>>> LiveAsync(string person, string topic, CancellationToken cancellationToken)
    {
        Begin(person);
        ArgumentNullException.ThrowIfNull(topic);
        if (await SupervisedAsync(person, topic, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return Result.Fail<IReadOnlyList<LiveConversation>>(refused);
        }

        MessagingOptions current = options.CurrentValue;
        List<ConversationRow> rows = await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockAsync(context, current, [person], token).ConfigureAwait(false);
                return await context.Conversations.AsNoTracking()
                    .Where(each => each.Type == SupportType && each.Topic == topic && (each.Status == OpenStatus || each.Status == SnoozedStatus))
                    .OrderBy(each => each.QueuedAt).ThenBy(each => each.Id)
                    .ToListAsync(token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
        DateTimeOffset now = clock.GetUtcNow();
        int queued = 0;
        List<LiveConversation> live = [];
        foreach (ConversationRow row in rows)
        {
            bool waiting = row.Assignee is null && string.Equals(row.Status, OpenStatus, StringComparison.Ordinal);
            DateTimeOffset since = row.QueuedAt ?? row.CreatedAt;
            live.Add(new LiveConversation
            {
                Conversation = new ConversationId(row.Id),
                Status = ConversationSpellings.StatusOf(row.Status),
                Assignee = row.Assignee,
                Place = waiting ? ++queued : null,
                Waited = now > since ? (int)Math.Floor((now - since).TotalMinutes) : 0,
            });
        }

        return Result.Success<IReadOnlyList<LiveConversation>>(live);
    }

    public async ValueTask<Result<IReadOnlyList<ReportFigures>>> ReportAsync(string person, string topic, DateOnly from, DateOnly through, CancellationToken cancellationToken)
    {
        Begin(person);
        ArgumentNullException.ThrowIfNull(topic);
        if (await SupervisedAsync(person, topic, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return Result.Fail<IReadOnlyList<ReportFigures>>(refused);
        }

        if (through < from || through.DayNumber - from.DayNumber + 1 > MostReportDays)
        {
            return Result.Fail<IReadOnlyList<ReportFigures>>(Malformed("to"));
        }

        SupportTopic configured = settings.Support.Topics[topic];
        DateTimeOffset start = StartOf(from, configured.Zone);
        DateTimeOffset end = StartOf(through.AddDays(1), configured.Zone);
        MessagingOptions current = options.CurrentValue;
        List<FiguresRow> rows = await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockAsync(context, current, [person], token).ConfigureAwait(false);
                return await context.Database.SqlQueryRaw<FiguresRow>(
                    Figures,
                    new NpgsqlParameter("topic", topic),
                    new NpgsqlParameter("start", start),
                    new NpgsqlParameter("end", end)).ToListAsync(token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
        FiguresRow total = rows.FirstOrDefault(row => row.Total) ?? new FiguresRow { Total = true };
        List<ReportFigures> figures = [Of(total)];
        figures.AddRange(rows.Where(row => !row.Total && row.Person is not null).OrderBy(row => row.Person, StringComparer.Ordinal).Select(Of));
        return Result.Success<IReadOnlyList<ReportFigures>>(figures);
    }

    /// <summary>The refusal of <paramref name="person"/> for <paramref name="topic"/>'s supervision: malformed for a topic not configured, unknown for one they do not supervise; none where they do.</summary>
    private async Task<Failure?> SupervisedAsync(string person, string topic, CancellationToken cancellationToken)
    {
        if (!settings.Support.Topics.TryGetValue(topic, out SupportTopic? configured))
        {
            return Malformed("topic");
        }

        return configured.Supervisors is { } supervisors && await membership.IsMemberAsync(person, supervisors, cancellationToken).ConfigureAwait(false)
            ? null
            : NotFound();
    }

    /// <summary>Whether <paramref name="person"/> supervises the topic of <paramref name="row"/>, asked now.</summary>
    private async Task<bool> IsSupervisorAsync(string person, ConversationRow row, CancellationToken cancellationToken)
    {
        return row.Topic is { } name
            && settings.Support.Topics.TryGetValue(name, out SupportTopic? topic)
            && topic.Supervisors is { } supervisors
            && await membership.IsMemberAsync(person, supervisors, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The instant <paramref name="date"/> starts in <paramref name="zone"/>, a start a clock change skips being the first minute after it.</summary>
    private static DateTimeOffset StartOf(DateOnly date, TimeZoneInfo zone)
    {
        DateTime local = date.ToDateTime(TimeOnly.MinValue);
        while (zone.IsInvalidTime(local))
        {
            local = local.AddMinutes(1);
        }

        return new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
    }

    private static ReportFigures Of(FiguresRow row)
    {
        return new ReportFigures
        {
            Person = row.Total ? null : row.Person,
            Closed = row.Closed,
            WaitMedian = Seconds(row.WaitMedian),
            WaitP90 = Seconds(row.WaitP90),
            HandleMedian = Seconds(row.HandleMedian),
            HandleP90 = Seconds(row.HandleP90),
            Surveys = row.Surveys,
            ResolvedShare = row.ResolvedShare,
            MeanRating = row.MeanRating,
        };
    }

    private static TimeSpan? Seconds(double? seconds)
    {
        return seconds is { } held ? TimeSpan.FromSeconds(held) : null;
    }

    /// <summary>A row of the report's query: the topic's whole, or one assignee's.</summary>
    private sealed class FiguresRow
    {
        public bool Total { get; set; }

        public string? Person { get; set; }

        public int Closed { get; set; }

        public double? WaitMedian { get; set; }

        public double? WaitP90 { get; set; }

        public double? HandleMedian { get; set; }

        public double? HandleP90 { get; set; }

        public int Surveys { get; set; }

        public decimal? ResolvedShare { get; set; }

        public decimal? MeanRating { get; set; }
    }
}