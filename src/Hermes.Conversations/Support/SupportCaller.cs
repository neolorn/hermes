using System;
using System.Collections.Generic;
using System.Text;

using Hermes.Core;
using Hermes.Sending;

namespace Hermes.Conversations.Support;

/// <summary>
/// Who calls the public side (<c>09</c> MSG-CONV-001): a signed-in customer, their person as
/// the identity seam answers it, or a visitor, known by the reference their cookie keeps,
/// which the database holds only as its keyed hash under the domain <c>visitor</c>.
/// </summary>
internal sealed class SupportCaller
{
    private SupportCaller(string? customer, string? visitor)
    {
        Customer = customer;
        Visitor = visitor;
    }

    /// <summary>The signed-in customer, or none for a visitor.</summary>
    internal string? Customer { get; }

    /// <summary>The visitor's reference, or none for a customer.</summary>
    internal string? Visitor { get; }

    internal static SupportCaller OfCustomer(string person)
    {
        return new SupportCaller(person, null);
    }

    internal static SupportCaller OfVisitor(string reference)
    {
        return new SupportCaller(null, reference);
    }

    /// <summary>The visitor's hash under every <c>hashKeys</c> version held, to look rows up by; none for a customer.</summary>
    internal byte[][] Hashes(KeyedHashes hashes)
    {
        if (Visitor is null)
        {
            return [];
        }

        List<byte[]> every = [];
        foreach ((int _, byte[] hash) in hashes.Every(KeyedHashes.Visitor, Encoding.UTF8.GetBytes(Visitor)))
        {
            every.Add(hash);
        }

        return [.. every];
    }

    /// <summary>The hash a limit counts this caller under, and its version: a visitor's reference, or a customer's person under <c>person</c>.</summary>
    internal (byte[] Hash, int Version) Counted(KeyedHashes hashes)
    {
        ArgumentNullException.ThrowIfNull(hashes);
        return Visitor is not null
            ? (hashes.Write(KeyedHashes.Visitor, Encoding.UTF8.GetBytes(Visitor)), hashes.WriteVersion)
            : (hashes.Write(KeyedHashes.Person, Encoding.UTF8.GetBytes(Customer!)), hashes.WriteVersion);
    }
}

/// <summary>A public operation's answer: its value, or its failure, with the instant to retry after for a limit.</summary>
/// <typeparam name="T">The value's type.</typeparam>
internal sealed record SupportAnswer<T>(T? Value, Failure? Failure, TimeSpan? RetryAfter = null)
{
    internal static SupportAnswer<T> Of(T value)
    {
        return new SupportAnswer<T>(value, null);
    }

    internal static SupportAnswer<T> Fail(Failure failure, TimeSpan? retryAfter = null)
    {
        return new SupportAnswer<T>(default, failure, retryAfter);
    }
}

/// <summary>The codes of support's refusals (<c>08</c> section 1; <c>09</c> MSG-CONV-002, MSG-CONV-010, MSG-CONV-011).</summary>
internal static class SupportCodes
{
    /// <summary>Outside the topic's hours, or within them while no member is available.</summary>
    internal const string Offline = "messaging.support.offline";

    /// <summary>A visitor's message or offline form before an acknowledgement of the current notice.</summary>
    internal const string NoticeRequired = "messaging.support.noticerequired";

    /// <summary>A visitor or a source address over a limit.</summary>
    internal const string Limited = "messaging.support.limited";

    /// <summary>A blocked visitor or customer starting or writing.</summary>
    internal const string Blocked = "messaging.support.blocked";

    /// <summary>A second start on a topic with one open, <c>conversation</c> naming it.</summary>
    internal const string AlreadyOpen = "messaging.support.alreadyopen";

    /// <summary>A transcript, a transcript email or a survey of a conversation not closed.</summary>
    internal const string NotClosed = "messaging.support.notclosed";

    /// <summary>A conversation's fourth transcript email.</summary>
    internal const string TranscriptLimit = "messaging.support.transcriptlimit";

    /// <summary>A survey's <c>resolved</c>, <c>rating</c> or <c>comment</c> outside its rule, <c>field</c> naming which.</summary>
    internal const string SurveyInvalid = "messaging.support.surveyinvalid";

    /// <summary>A survey answered a second time.</summary>
    internal const string SurveyAnswered = "messaging.support.surveyanswered";

    /// <summary>A survey answered more than seven days after the close.</summary>
    internal const string SurveyClosed = "messaging.support.surveyclosed";
}