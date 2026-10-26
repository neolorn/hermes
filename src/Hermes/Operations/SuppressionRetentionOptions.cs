using System;
using System.ComponentModel.DataAnnotations;

namespace Hermes.Operations;

/// <summary><c>Hermes:Retention:Suppression</c> (<c>08</c> section 4); none keeps a suppression indefinitely.</summary>
internal sealed class SuppressionRetentionOptions
{
    [Range(typeof(TimeSpan), "30.00:00:00", "3650.00:00:00")]
    public TimeSpan? Email { get; set; }

    [Range(typeof(TimeSpan), "30.00:00:00", "3650.00:00:00")]
    public TimeSpan? Sms { get; set; }
}