using System;
using System.Collections.Generic;

namespace Hermes.Core;

/// <summary>One attempt of a delivery: when it was counted, how it was answered and its provider calls (<c>01</c> MSG-SEND-007).</summary>
public sealed record AttemptRecord
{
    /// <summary>The attempt's number, from 1.</summary>
    public required int Number { get; init; }

    /// <summary>When the attempt was counted.</summary>
    public required DateTimeOffset At { get; init; }

    /// <summary>The class of its answer, or none while it has no answer.</summary>
    public AttemptClass? AnswerClass { get; init; }

    /// <summary>The reason of its answer, or none.</summary>
    public AttemptReason? Reason { get; init; }

    /// <summary>Its provider calls, in order; none for an attempt answered at a check.</summary>
    public required IReadOnlyList<CallRecord> Calls { get; init; }
}