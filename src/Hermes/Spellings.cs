using System;
using System.Collections.Generic;

using Hermes.Core;

namespace Hermes;

/// <summary>
/// The wire spellings of <c>08</c> sections 2, 3 and 6, which the engine writes wherever it
/// writes a member as text (a column, a path, a detail), and reads back (<c>04</c> MSG-LIB-002).
/// Each table is in the order of its enumeration's members.
/// </summary>
internal static class Spellings
{
    private static readonly string[] Channels = ["email", "sms", "push"];

    private static readonly string[] Statuses =
    [
        "waiting", "inflight", "sent", "delivered", "failed", "cancelled", "expired",
        "suppressed", "optedout", "noconsent", "limited", "balancefloor", "nosubscription",
    ];

    private static readonly string[] Classes = ["accepted", "transient", "permanent", "gone"];

    private static readonly string[] Reasons =
    [
        "timeout", "connectionfailed", "tlsfailed", "authfailed", "senderrefused",
        "recipientrefused", "messagerefused", "badrequest", "ratelimited", "temporary",
        "permanent", "gone", "providerfault", "keyunavailable", "consentunavailable",
        "reportedfailed", "claimlost", "serverrefused", "destinationnotallowed", "valuerejected",
    ];

    private static readonly string[] Services = ["fcm", "mozilla", "apple", "windows", "other"];

    /// <summary>Every channel's spelling, in the order of <see cref="Channel"/>.</summary>
    internal static IReadOnlyList<string> AllChannels => Channels;

    /// <summary>Every status's spelling, in the order of <see cref="DeliveryStatus"/>.</summary>
    internal static IReadOnlyList<string> AllStatuses => Statuses;

    /// <summary>Every class's spelling, in the order of <see cref="AttemptClass"/>.</summary>
    internal static IReadOnlyList<string> AllClasses => Classes;

    /// <summary>Every reason's spelling, in the order of <see cref="AttemptReason"/>.</summary>
    internal static IReadOnlyList<string> AllReasons => Reasons;

    /// <summary>Every push service's spelling, in the order of <see cref="PushService"/>.</summary>
    internal static IReadOnlyList<string> AllServices => Services;

    internal static string Of(Channel channel)
    {
        return Channels[(int)channel];
    }

    internal static string Of(DeliveryStatus status)
    {
        return Statuses[(int)status];
    }

    internal static string Of(AttemptClass answer)
    {
        return Classes[(int)answer];
    }

    internal static string Of(AttemptReason reason)
    {
        return Reasons[(int)reason];
    }

    internal static string Of(PushService service)
    {
        return Services[(int)service];
    }

    /// <summary>The channel <paramref name="spelling"/> names, or none for one this release does not know.</summary>
    internal static Channel? Channel(string spelling)
    {
        int index = Array.IndexOf(Channels, spelling);
        return index < 0 ? null : (Channel)index;
    }

    /// <summary>The status <paramref name="spelling"/> names, or none for one this release does not know.</summary>
    internal static DeliveryStatus? Status(string spelling)
    {
        int index = Array.IndexOf(Statuses, spelling);
        return index < 0 ? null : (DeliveryStatus)index;
    }

    /// <summary>The class <paramref name="spelling"/> names, or none for one this release does not know.</summary>
    internal static AttemptClass? Class(string spelling)
    {
        int index = Array.IndexOf(Classes, spelling);
        return index < 0 ? null : (AttemptClass)index;
    }

    /// <summary>The reason <paramref name="spelling"/> names, or none for one this release does not know.</summary>
    internal static AttemptReason? Reason(string spelling)
    {
        int index = Array.IndexOf(Reasons, spelling);
        return index < 0 ? null : (AttemptReason)index;
    }

    /// <summary>The push service <paramref name="spelling"/> names, or none for one this release does not know.</summary>
    internal static PushService? Service(string spelling)
    {
        int index = Array.IndexOf(Services, spelling);
        return index < 0 ? null : (PushService)index;
    }
}