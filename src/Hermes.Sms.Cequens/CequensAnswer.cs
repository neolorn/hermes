using System.Collections.Generic;
using System.Linq;

using Hermes.Core;

namespace Hermes.Sms.Cequens;

/// <summary>The class of the gateway's answers (<c>02</c> MSG-SMS-007).</summary>
internal static class CequensAnswer
{
    /// <summary>The most segments the gateway takes in one message (its code -41).</summary>
    internal const int MaxSegments = 20;

    /// <summary>
    /// The attempt a send's answer gives, the rows of the answer table tried from the top:
    /// <paramref name="answer"/> is the body read, none where it does not read; a 200 that
    /// does not read, and a status the table does not name, are an answer that does not read.
    /// </summary>
    internal static Attempt Send(int status, AnswerBody? answer)
    {
        return status switch
        {
            200 => Handled(answer),
            400 => Refused(answer),
            401 or 403 => new Attempt.Transient(AttemptReason.AuthFailed, status, null),
            429 => new Attempt.Transient(AttemptReason.RateLimited, status, null),
            >= 402 and <= 499 => new Attempt.Permanent(AttemptReason.Permanent, status),
            >= 500 and <= 599 => new Attempt.Transient(AttemptReason.Temporary, status, null),
            _ => new Attempt.Transient(AttemptReason.ConnectionFailed, status, null),
        };
    }

    /// <summary>
    /// The reading a balance's answer gives: the account's credit for a handled request, the
    /// reason of the send's table for 401, 403, 429 and a 200 that does not read, and
    /// <c>temporary</c> for anything else.
    /// </summary>
    internal static BalanceReading Balance(int status, AnswerBody? answer)
    {
        return status switch
        {
            200 when answer is { ReplyCode: 0, Data.Account.Credit: { } credit } => new BalanceReading.Balance(credit),
            200 => new BalanceReading.Failed(AttemptReason.ConnectionFailed),
            401 or 403 => new BalanceReading.Failed(AttemptReason.AuthFailed),
            429 => new BalanceReading.Failed(AttemptReason.RateLimited),
            _ => new BalanceReading.Failed(AttemptReason.Temporary),
        };
    }

    /// <summary>
    /// A 200: handled with the message's identifier, or refused for its number where the
    /// gateway lists any invalid recipient, since a send carries one number.
    /// </summary>
    private static Attempt Handled(AnswerBody? answer)
    {
        if (answer is not { ReplyCode: 0 })
        {
            return new Attempt.Transient(AttemptReason.ConnectionFailed, 200, null);
        }

        if (!string.IsNullOrWhiteSpace(answer.Data?.InvalidRecipients))
        {
            return new Attempt.Permanent(AttemptReason.RecipientRefused, 200);
        }

        return answer.Data?.SentSmsIds is [{ SmsId: { Length: > 0 } identifier }, ..]
            ? new Attempt.Accepted(identifier)
            : new Attempt.Permanent(AttemptReason.Permanent, 200);
    }

    /// <summary>A 400, classed by the first row whose code any of the gateway's reasons carries.</summary>
    private static Attempt Refused(AnswerBody? answer)
    {
        HashSet<int> codes = [.. (answer?.Error?.InternalErrors ?? []).Select(error => error?.Code).OfType<int>()];
        if (codes.Contains(-6))
        {
            return new Attempt.Permanent(AttemptReason.RecipientRefused, 400);
        }

        if (codes.Contains(-12))
        {
            return new Attempt.Permanent(AttemptReason.SenderRefused, 400);
        }

        if (codes.Overlaps([-8, -9, -41]))
        {
            return new Attempt.Permanent(AttemptReason.MessageRefused, 400);
        }

        return codes.Contains(-17)
            ? new Attempt.Transient(AttemptReason.Temporary, 400, null)
            : new Attempt.Permanent(AttemptReason.Permanent, 400);
    }
}