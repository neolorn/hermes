using System;
using System.ComponentModel.DataAnnotations;

namespace Hermes.Operations;

/// <summary><c>Hermes:Observer</c> (<c>08</c> section 4).</summary>
internal sealed class ObserverOptions
{
    public bool Enabled { get; set; }

    [Range(1, 64)]
    public int MaxCalls { get; set; } = 4;

    [Range(typeof(TimeSpan), "00:00:03", "1.00:00:00")]
    public TimeSpan LagThreshold { get; set; } = TimeSpan.FromMinutes(15);
}