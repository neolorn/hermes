using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Hermes.Channels;

/// <summary><c>Hermes:Sms</c>, the SMS channel's own keys (<c>08</c> section 4).</summary>
internal sealed class SmsChannelOptions
{
    public string? Sender { get; set; }

    public List<string> AllowedPrefixes { get; set; } = [];

    [Range(1, 1_000)]
    public int Rate { get; set; } = 5;

    public Uri? ReportBaseAddress { get; set; }

    [Range(1, 10_000)]
    public int WrongKeyRate { get; set; } = 60;

    [Range(1, 10_000)]
    public int ReportRejectThreshold { get; set; } = 20;

    [Range(typeof(TimeSpan), "00:01:00", "1.00:00:00")]
    public TimeSpan ReportRejectWindow { get; set; } = TimeSpan.FromHours(1);

    [Range(typeof(TimeSpan), "00:01:00", "1.00:00:00")]
    public TimeSpan ReportHold { get; set; } = TimeSpan.FromMinutes(10);

    [Range(10, 1_000_000)]
    public int ReportHoldMax { get; set; } = 1_000;

    [Range(typeof(TimeSpan), "00:01:00", "1.00:00:00")]
    public TimeSpan BalancePoll { get; set; } = TimeSpan.FromMinutes(15);

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal BalanceFloor { get; set; }

    [Range(typeof(TimeSpan), "01:00:00", "30.00:00:00")]
    public TimeSpan FloorWarning { get; set; } = TimeSpan.FromDays(1);

    [Range(typeof(TimeSpan), "00:05:00", "1.00:00:00")]
    public TimeSpan DrainWindow { get; set; } = TimeSpan.FromHours(1);

    [Range(1.5, 100)]
    public double DrainFactor { get; set; } = 3;

    [Range(typeof(TimeSpan), "00:10:00", "90.00:00:00")]
    public TimeSpan DrainBaseline { get; set; } = TimeSpan.FromDays(7);

    [Range(typeof(TimeSpan), "00:05:00", "90.00:00:00")]
    public TimeSpan DrainMinHistory { get; set; } = TimeSpan.FromDays(1);

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal DrainMinSpend { get; set; } = 10;

    [Range(typeof(TimeSpan), "00:05:00", "1.00:00:00")]
    public TimeSpan BalanceUnreadableAfter { get; set; } = TimeSpan.FromHours(1);
}