using System;
using System.ComponentModel.DataAnnotations;

using Microsoft.Extensions.Options;

namespace Hermes.Operations;

/// <summary><c>Hermes:Retention</c> (<c>08</c> section 4), a year counted as 365 days.</summary>
internal sealed class RetentionOptions
{
    [Range(typeof(TimeSpan), "1.00:00:00", "1095.00:00:00")]
    public TimeSpan Content { get; set; } = TimeSpan.FromDays(30);

    [Range(typeof(TimeSpan), "1.00:00:00", "3650.00:00:00")]
    public TimeSpan Record { get; set; } = TimeSpan.FromDays(365);

    [Range(typeof(TimeSpan), "1.00:00:00", "365.00:00:00")]
    public TimeSpan Places { get; set; } = TimeSpan.FromDays(31);

    [ValidateObjectMembers]
    public SuppressionRetentionOptions Suppression { get; set; } = new();

    [Range(typeof(TimeSpan), "1.00:00:00", "1095.00:00:00")]
    public TimeSpan InApp { get; set; } = TimeSpan.FromDays(180);
}