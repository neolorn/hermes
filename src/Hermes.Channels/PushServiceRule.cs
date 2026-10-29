using System;

using Hermes.Core;

namespace Hermes.Channels;

/// <summary>
/// The push service of a subscription's endpoint host (<c>08</c> section 2), which the engine
/// records with each subscription and the development inbox names in place of the endpoint
/// (<c>02</c> MSG-DEV-003).
/// </summary>
internal static class PushServiceRule
{
    /// <summary>The push service of <paramref name="host"/>.</summary>
    internal static PushService Of(string host)
    {
        return host switch
        {
            _ when string.Equals(host, "fcm.googleapis.com", StringComparison.OrdinalIgnoreCase) => PushService.Fcm,
            _ when host.EndsWith(".push.services.mozilla.com", StringComparison.OrdinalIgnoreCase) => PushService.Mozilla,
            _ when host.EndsWith(".push.apple.com", StringComparison.OrdinalIgnoreCase) => PushService.Apple,
            _ when host.EndsWith(".notify.windows.com", StringComparison.OrdinalIgnoreCase) => PushService.Windows,
            _ => PushService.Other,
        };
    }
}