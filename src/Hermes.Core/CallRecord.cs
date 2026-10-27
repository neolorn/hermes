namespace Hermes.Core;

/// <summary>One provider call of an attempt; it holds no text the provider answered (<c>01</c> MSG-SEND-007).</summary>
public sealed record CallRecord
{
    /// <summary>The class of its answer, or none while it has no answer.</summary>
    public AttemptClass? AnswerClass { get; init; }

    /// <summary>The reason of its answer, or none.</summary>
    public AttemptReason? Reason { get; init; }

    /// <summary>The provider's numeric status, where it gave one.</summary>
    public int? Status { get; init; }

    /// <summary>For push, the subscription's push service; none for another channel.</summary>
    public PushService? Service { get; init; }
}