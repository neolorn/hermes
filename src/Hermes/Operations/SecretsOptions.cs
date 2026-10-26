using System;
using System.ComponentModel.DataAnnotations;

using Microsoft.Extensions.Options;

namespace Hermes.Operations;

/// <summary><c>Hermes:Secrets</c> (<c>08</c> section 4; <c>06</c> MSG-OPS-008).</summary>
internal sealed class SecretsOptions
{
    [Range(typeof(TimeSpan), "1.00:00:00", "365.00:00:00")]
    public TimeSpan RetireAfter { get; set; } = TimeSpan.FromDays(7);

    [ValidateObjectMembers]
    public ContentKeysOptions ContentKeys { get; set; } = new();

    public KeyVersionOptions HashKeys { get; set; } = new();

    public KeyVersionOptions TokenKeys { get; set; } = new();

    public KeyVersionOptions SmsReportKey { get; set; } = new();

    public KeyVersionOptions PushVapidKeys { get; set; } = new();
}