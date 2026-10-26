using System;
using System.ComponentModel.DataAnnotations;

namespace Hermes.Operations;

/// <summary><c>Hermes:Idempotency</c> (<c>08</c> section 4).</summary>
internal sealed class IdempotencyOptions
{
    [Range(typeof(TimeSpan), "00:01:00", "30.00:00:00")]
    public TimeSpan Window { get; set; } = TimeSpan.FromDays(1);
}