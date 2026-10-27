using System;
using System.Collections.Generic;

namespace Hermes.Operations;

/// <summary>A row of the job table: one job's claim and its instants (<c>06</c> MSG-OPS-004).</summary>
internal sealed class JobRow
{
    /// <summary>The jobs with a row, <c>dispatch</c> having none (<c>08</c> section 8).</summary>
    internal static IReadOnlyList<string> Names { get; } = ["observer", "balance", "retention", "subscriptions", "watch"];

    public required string Name { get; set; }

    public Guid? ClaimedBy { get; set; }

    public DateTimeOffset? ClaimedAt { get; set; }

    public DateTimeOffset? ClaimExpiresAt { get; set; }

    /// <summary>The first start by any process, from which <c>job-stalled</c> is judged.</summary>
    public DateTimeOffset? FirstStartedAt { get; set; }

    public DateTimeOffset? LastStartedAt { get; set; }

    public DateTimeOffset? LastRenewedAt { get; set; }

    public DateTimeOffset? LastCompletedAt { get; set; }
}