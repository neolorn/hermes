using System;
using System.ComponentModel.DataAnnotations;

namespace Hermes.Operations;

/// <summary><c>Hermes:Unsubscribe</c> (<c>08</c> section 4), a year counted as 365 days.</summary>
internal sealed class UnsubscribeOptions
{
    [Range(typeof(TimeSpan), "180.00:00:00", "3650.00:00:00")]
    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromDays(730);
}