using System;

namespace Hermes.Core;

/// <summary>
/// What a provider answers for one call (<c>02</c> MSG-CHAN-001). It carries no text the
/// provider answered: a timeout, a refused connection and every reply are answers, so a
/// provider never throws for an answer it received.
/// </summary>
public abstract record Attempt
{
    private Attempt()
    {
    }

    /// <summary>The provider took the message.</summary>
    /// <param name="ProviderReference">The provider's identifier for the message, where it answers one.</param>
    public sealed record Accepted(string? ProviderReference) : Attempt;

    /// <summary>The provider did not take the message, and a later attempt may.</summary>
    /// <param name="Reason">Why, a reason of <c>08</c> section 6.</param>
    /// <param name="Status">The provider's numeric status (an SMTP reply code, an HTTP status), where it gave one.</param>
    /// <param name="RetryAfter">How long the provider asked to wait before the next attempt, where it asked.</param>
    public sealed record Transient(AttemptReason Reason, int? Status, TimeSpan? RetryAfter) : Attempt;

    /// <summary>The provider refused the message for good.</summary>
    /// <param name="Reason">Why, a reason of <c>08</c> section 6.</param>
    /// <param name="Status">The provider's numeric status, where it gave one.</param>
    public sealed record Permanent(AttemptReason Reason, int? Status) : Attempt;

    /// <summary>The push subscription no longer exists; a push provider's answer only (<c>02</c> MSG-PUSH-005).</summary>
    /// <param name="Reason">Why, a reason of <c>08</c> section 6.</param>
    /// <param name="Status">The push service's HTTP status, where it gave one.</param>
    public sealed record Gone(AttemptReason Reason, int? Status) : Attempt;
}