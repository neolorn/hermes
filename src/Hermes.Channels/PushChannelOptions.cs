using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Hermes.Channels;

/// <summary><c>Hermes:Push</c>, the push channel's own keys and the shipped provider's subject (<c>08</c> section 4).</summary>
internal sealed class PushChannelOptions
{
    /// <summary>The push services allowed where <c>Push:AllowedHosts</c> has no item (<c>02</c> MSG-PUSH-004).</summary>
    internal static readonly IReadOnlyList<string> DefaultAllowedHosts =
        ["fcm.googleapis.com", "*.push.services.mozilla.com", "*.push.apple.com", "*.notify.windows.com"];

    public string? Subject { get; set; }

    public List<string> AllowedHosts { get; set; } = [];

    [Range(1, 100)]
    public int MaxSubscriptions { get; set; } = 10;

    [Range(1, 1_000)]
    public int Rate { get; set; } = 50;

    [Range(typeof(TimeSpan), "7.00:00:00", "1095.00:00:00")]
    public TimeSpan IdleRetention { get; set; } = TimeSpan.FromDays(180);

    [Range(1, 10_000)]
    public int AuthFailThreshold { get; set; } = 20;

    [Range(typeof(TimeSpan), "00:01:00", "1.00:00:00")]
    public TimeSpan AuthFailWindow { get; set; } = TimeSpan.FromHours(1);

    /// <summary>The allowed hosts in force: the configured items, or the defaults where none is configured.</summary>
    internal IReadOnlyList<string> Hosts => AllowedHosts.Count > 0 ? AllowedHosts : DefaultAllowedHosts;
}