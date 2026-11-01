using System;
using System.Collections.Generic;

namespace Hermes.Operations;

/// <summary>A row of the job table: one job's claim and its instants (<c>06</c> MSG-OPS-004).</summary>
internal sealed class JobRow
{
    /// <summary>The jobs with a row, <c>dispatch</c> having none (<c>08</c> section 8).</summary>
    internal static IReadOnlyList<string> Names { get; } = ["observer", "balance", "retention", "subscriptions", "watch", ConversationTimers, AttachmentScans];

    /// <summary>The job of conversations' timers, run where conversations are registered (<c>09</c> MSG-CONV-005).</summary>
    internal const string ConversationTimers = "conversation-timers";

    /// <summary>The interval of <see cref="ConversationTimers"/>.</summary>
    internal static TimeSpan ConversationTimersInterval { get; } = TimeSpan.FromSeconds(30);

    /// <summary>The job of attachments' scans and deletions, run where attachments are enabled (<c>09</c> MSG-CONV-007).</summary>
    internal const string AttachmentScans = "attachment-scans";

    /// <summary>The interval of <see cref="AttachmentScans"/>.</summary>
    internal static TimeSpan AttachmentScansInterval { get; } = TimeSpan.FromSeconds(30);

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