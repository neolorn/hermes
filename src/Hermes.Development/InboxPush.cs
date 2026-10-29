using System;
using System.Globalization;
using System.Text;

using Hermes.Core;

namespace Hermes.Development;

/// <summary>A captured push request (<c>02</c> MSG-DEV-003), which names its push service and never its subscription's endpoint.</summary>
public sealed record InboxPush
{
    /// <summary>The push service of the subscription's endpoint.</summary>
    public required PushService Service { get; init; }

    /// <summary>The notification the payload holds.</summary>
    public required InboxNotification Notification { get; init; }

    /// <summary>How long the push service would keep the request.</summary>
    public required TimeSpan Ttl { get; init; }

    /// <summary>The request's urgency.</summary>
    public required PushUrgency Urgency { get; init; }

    /// <summary>The request's topic, none where it has none.</summary>
    public string? Topic { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Service = {Service}, Urgency = {Urgency}");
        return true;
    }
}