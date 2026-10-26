using System;

using Hermes.Core;

namespace Hermes.Channels;

/// <summary>The class of a push service's answer (<c>02</c> MSG-PUSH-005).</summary>
internal static class PushAnswer
{
    /// <summary>
    /// The attempt <paramref name="status"/> answers, the rows of MSG-PUSH-005 tried from the
    /// top; <paramref name="retryAfter"/> is honoured for a 429 alone.
    /// </summary>
    internal static Attempt Class(int status, TimeSpan? retryAfter)
    {
        return status switch
        {
            200 or 201 or 202 => new Attempt.Accepted(null),
            404 or 410 => new Attempt.Gone(AttemptReason.Gone, status),
            400 => new Attempt.Permanent(AttemptReason.BadRequest, status),
            401 or 403 => new Attempt.Transient(AttemptReason.AuthFailed, status, null),
            413 => new Attempt.Permanent(AttemptReason.MessageRefused, status),
            429 => new Attempt.Transient(AttemptReason.RateLimited, status, retryAfter),
            >= 500 and <= 599 => new Attempt.Transient(AttemptReason.Temporary, status, null),
            _ => new Attempt.Permanent(AttemptReason.BadRequest, status),
        };
    }
}