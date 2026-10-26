using System;
using System.ComponentModel.DataAnnotations;

namespace Hermes.Operations;

/// <summary><c>Hermes:QuietHours</c> (<c>08</c> section 4).</summary>
internal sealed class QuietHoursOptions
{
    public TimeOnly Start { get; set; } = new(21, 0);

    public TimeOnly End { get; set; } = new(9, 0);

    [Range(typeof(TimeSpan), "00:00:00", "04:00:00")]
    public TimeSpan Spread { get; set; } = TimeSpan.FromMinutes(30);
}