using System;
using System.ComponentModel.DataAnnotations;

namespace Hermes.Operations;

/// <summary><c>Hermes:Jobs</c> (<c>08</c> section 4).</summary>
internal sealed class JobsOptions
{
    public bool Enabled { get; set; } = true;

    public TimeOnly RetentionAt { get; set; } = new(3, 0);

    [Range(typeof(TimeSpan), "00:01:00", "1.00:00:00")]
    public TimeSpan SubscriptionsInterval { get; set; } = TimeSpan.FromHours(1);

    [Range(100, 100_000)]
    public int BatchSize { get; set; } = 1_000;

    [Range(2d, 100d)]
    public double StallFactor { get; set; } = 3;

    [Range(typeof(TimeSpan), "00:00:32", "1.00:00:00")]
    public TimeSpan ClaimTimeout { get; set; } = TimeSpan.FromMinutes(10);
}