using System;
using System.Globalization;
using System.Text;

namespace Hermes.Core;

/// <summary>
/// A topic's figures over a period (<c>09</c> MSG-CONV-005; <c>05</c> MSG-API-009), for one
/// staff member or for the topic as a whole: the conversations closed, the median and 90th
/// percentile of waiting and handling times, and the surveys answered; a figure with no data
/// is none. It prints no person reference.
/// </summary>
public sealed record ReportFigures
{
    /// <summary>The staff member the figures are for, or none for the topic as a whole.</summary>
    public string? Person { get; init; }

    /// <summary>The conversations closed within the period.</summary>
    public required int Closed { get; init; }

    /// <summary>The median time from queued to assigned, or none.</summary>
    public TimeSpan? WaitMedian { get; init; }

    /// <summary>The 90th percentile of the time from queued to assigned, or none.</summary>
    public TimeSpan? WaitP90 { get; init; }

    /// <summary>The median time from assigned to closed, or none.</summary>
    public TimeSpan? HandleMedian { get; init; }

    /// <summary>The 90th percentile of the time from assigned to closed, or none.</summary>
    public TimeSpan? HandleP90 { get; init; }

    /// <summary>The surveys answered.</summary>
    public required int Surveys { get; init; }

    /// <summary>The share of the surveys answered that say resolved, from 0 to 1, or none.</summary>
    public decimal? ResolvedShare { get; init; }

    /// <summary>The mean rating of the surveys answered, from 1 to 5, or none.</summary>
    public decimal? MeanRating { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Closed = {Closed}, WaitMedian = {WaitMedian}, WaitP90 = {WaitP90}, HandleMedian = {HandleMedian}, HandleP90 = {HandleP90}, Surveys = {Surveys}, ResolvedShare = {ResolvedShare}, MeanRating = {MeanRating}");
        return true;
    }
}