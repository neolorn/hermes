using System;
using System.ComponentModel.DataAnnotations;

namespace Hermes.Operations;

/// <summary><c>Hermes:Alerts</c> (<c>08</c> section 4).</summary>
internal sealed class AlertsOptions
{
    [Range(typeof(TimeSpan), "00:05:00", "1.00:00:00")]
    public TimeSpan Repeat { get; set; } = TimeSpan.FromHours(1);
}