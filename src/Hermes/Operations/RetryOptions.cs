using System;
using System.ComponentModel.DataAnnotations;

namespace Hermes.Operations;

/// <summary><c>Hermes:Retry</c> (<c>08</c> section 4).</summary>
internal sealed class RetryOptions
{
    [Range(typeof(TimeSpan), "00:00:01", "01:00:00")]
    public TimeSpan Initial { get; set; } = TimeSpan.FromSeconds(30);

    [Range(1d, 10d)]
    public double Factor { get; set; } = 2;

    [Range(typeof(TimeSpan), "00:00:01", "1.00:00:00")]
    public TimeSpan Max { get; set; } = TimeSpan.FromHours(1);

    [Range(0d, 1d)]
    public double Jitter { get; set; } = 0.2;

    [Range(1, 50)]
    public int MaxAttempts { get; set; } = 8;
}