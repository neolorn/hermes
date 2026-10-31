using System.Text.Json.Serialization;

namespace Hermes.Core;

/// <summary>A delivery's status (<c>08</c> section 3).</summary>
public enum DeliveryStatus
{
    /// <summary>Recorded; not yet due, or waiting for its next attempt, quiet hours or a provider's rate.</summary>
    [JsonStringEnumMemberName("waiting")]
    Waiting,

    /// <summary>Claimed by a dispatcher.</summary>
    [JsonStringEnumMemberName("inflight")]
    InFlight,

    /// <summary>Accepted by the provider, or, in-app, its entry written; final, moving once by a report or, in-app, a read.</summary>
    [JsonStringEnumMemberName("sent")]
    Sent,

    /// <summary>Reported delivered by the provider.</summary>
    [JsonStringEnumMemberName("delivered")]
    Delivered,

    /// <summary>Failed; its reason says why.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,

    /// <summary>Cancelled while waiting, its recipient erased, a value whose key was erased, or its kind retired.</summary>
    [JsonStringEnumMemberName("cancelled")]
    Cancelled,

    /// <summary>Not sent within the kind's <c>ExpiresAfter</c>.</summary>
    [JsonStringEnumMemberName("expired")]
    Expired,

    /// <summary>The destination is suppressed for the category.</summary>
    [JsonStringEnumMemberName("suppressed")]
    Suppressed,

    /// <summary>The person turned the category off on the channel.</summary>
    [JsonStringEnumMemberName("optedout")]
    OptedOut,

    /// <summary>The consent source answered that the recipient does not consent to the category's purpose.</summary>
    [JsonStringEnumMemberName("noconsent")]
    NoConsent,

    /// <summary>A limit of the category is reached.</summary>
    [JsonStringEnumMemberName("limited")]
    Limited,

    /// <summary>An SMS of an optional category, checked while the balance is below the floor.</summary>
    [JsonStringEnumMemberName("balancefloor")]
    BalanceFloor,

    /// <summary>A push to a person with no live subscription, or whose every subscription is gone.</summary>
    [JsonStringEnumMemberName("nosubscription")]
    NoSubscription,

    /// <summary>An in-app delivery whose entry the person marked read; final.</summary>
    [JsonStringEnumMemberName("read")]
    Read,
}