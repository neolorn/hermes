using System;
using System.Collections.Generic;

namespace Hermes.Operations;

/// <summary>A row of the alert table: one condition's state in one scope (<c>06</c> MSG-OPS-006).</summary>
internal sealed class AlertState
{
    /// <summary>The conditions of <c>08</c> section 5.</summary>
    internal static IReadOnlyList<string> Conditions { get; } =
    [
        "email-submission-failing",
        "sms-balance",
        "sms-balance-unreadable",
        "sms-report-rejected",
        "push-auth-failing",
        "dispatch-lag",
        "job-stalled",
        "newer-release",
        "configuration-invalid",
        "content-key-limit",
        "key-unavailable",
        "content-rejected",
        "observer-failing",
        "attachment-scan",
    ];

    public required string Condition { get; set; }

    /// <summary>The scope, empty for a condition with none.</summary>
    public required string Scope { get; set; }

    public bool Holding { get; set; }

    public DateTimeOffset? LastRaisedAt { get; set; }

    /// <summary>For the conditions a process writes an instant for, the instant until which it holds.</summary>
    public DateTimeOffset? HoldsUntil { get; set; }
}