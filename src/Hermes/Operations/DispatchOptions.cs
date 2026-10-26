using System;
using System.ComponentModel.DataAnnotations;

namespace Hermes.Operations;

/// <summary><c>Hermes:Dispatch</c> (<c>08</c> section 4).</summary>
internal sealed class DispatchOptions
{
    public bool Enabled { get; set; } = true;

    public bool Hosted { get; set; } = true;

    public bool Notify { get; set; } = true;

    [Range(typeof(TimeSpan), "00:00:03", "00:05:00")]
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(5);

    [Range(1, 1_000)]
    public int BatchSize { get; set; } = 100;

    [Range(1, 32)]
    public int MaxPasses { get; set; } = 4;

    [Range(typeof(TimeSpan), "00:00:01", "00:02:00")]
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(20);

    [Range(typeof(TimeSpan), "00:00:32", "01:00:00")]
    public TimeSpan ClaimTimeout { get; set; } = TimeSpan.FromMinutes(2);

    [Range(typeof(TimeSpan), "00:00:03", "1.00:00:00")]
    public TimeSpan LagThreshold { get; set; } = TimeSpan.FromMinutes(5);

    [Range(typeof(TimeSpan), "01:00:00", "90.00:00:00")]
    public TimeSpan MaxHold { get; set; } = TimeSpan.FromDays(7);
}