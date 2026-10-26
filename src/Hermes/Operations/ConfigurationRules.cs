using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Hermes.Channels;

namespace Hermes.Operations;

/// <summary>
/// The engine's keys of <c>08</c> section 4 that hold one value, in that section's order, and
/// the rules that bound one key by others. A year in a rule is 365 days, as <c>08</c> gives
/// <c>Unsubscribe:TokenLifetime</c>'s default of 2 years (<c>05</c> MSG-API-005) as 730 days.
/// </summary>
internal static class ConfigurationRules
{
    private static readonly TimeSpan Year = TimeSpan.FromDays(365);

    private static readonly MessagingOptions Defaults = new();

    internal static IReadOnlyList<ConfigurationKey> Keys { get; } =
    [
        ConfigurationKeys.Whole("Templates:MaxSteps", startOnly: true, 1_000, 10_000_000, Defaults.Templates.MaxSteps),
        ConfigurationKeys.Whole("Templates:MaxRecursion", startOnly: true, 1, 100, Defaults.Templates.MaxRecursion),
        ConfigurationKeys.Flag("Messages:Enabled", Defaults.Messages.Enabled),
        ConfigurationKeys.Flag("Dispatch:Enabled", Defaults.Dispatch.Enabled),
        ConfigurationKeys.Flag("Dispatch:Hosted", Defaults.Dispatch.Hosted),
        ConfigurationKeys.Flag("Dispatch:Notify", Defaults.Dispatch.Notify),
        ConfigurationKeys.Duration("Dispatch:Interval", startOnly: false, TimeSpan.FromSeconds(3), TimeSpan.FromMinutes(5), Defaults.Dispatch.Interval),
        ConfigurationKeys.Whole("Dispatch:BatchSize", startOnly: false, 1, 1_000, Defaults.Dispatch.BatchSize),
        ConfigurationKeys.Whole("Dispatch:MaxPasses", startOnly: true, 1, 32, Defaults.Dispatch.MaxPasses),
        ConfigurationKeys.Duration("Dispatch:Timeout", startOnly: false, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(2), Defaults.Dispatch.Timeout),
        ConfigurationKeys.Duration("Dispatch:ClaimTimeout", startOnly: false, TimeSpan.Zero, TimeSpan.FromHours(1), Defaults.Dispatch.ClaimTimeout),
        ConfigurationKeys.Duration("Dispatch:LagThreshold", startOnly: false, TimeSpan.Zero, TimeSpan.FromDays(1), Defaults.Dispatch.LagThreshold),
        ConfigurationKeys.Duration("Dispatch:MaxHold", startOnly: false, TimeSpan.FromHours(1), TimeSpan.FromDays(90), Defaults.Dispatch.MaxHold),
        ConfigurationKeys.Flag("Observer:Enabled", Defaults.Observer.Enabled),
        ConfigurationKeys.Whole("Observer:MaxCalls", startOnly: true, 1, 64, Defaults.Observer.MaxCalls),
        ConfigurationKeys.Duration("Observer:LagThreshold", startOnly: false, TimeSpan.Zero, TimeSpan.FromDays(1), Defaults.Observer.LagThreshold),
        ConfigurationKeys.Duration("Retry:Initial", startOnly: false, TimeSpan.FromSeconds(1), TimeSpan.FromHours(1), Defaults.Retry.Initial),
        ConfigurationKeys.Decimal("Retry:Factor", 1, 10, Defaults.Retry.Factor),
        ConfigurationKeys.Duration("Retry:Max", startOnly: false, TimeSpan.Zero, TimeSpan.FromDays(1), Defaults.Retry.Max),
        ConfigurationKeys.Decimal("Retry:Jitter", 0, 1, Defaults.Retry.Jitter),
        ConfigurationKeys.Whole("Retry:MaxAttempts", startOnly: false, 1, 50, Defaults.Retry.MaxAttempts),
        ConfigurationKeys.Duration("Idempotency:Window", startOnly: false, TimeSpan.FromMinutes(1), TimeSpan.FromDays(30), Defaults.Idempotency.Window),
        ConfigurationKeys.TimeOfDay("QuietHours:Start", Defaults.QuietHours.Start),
        ConfigurationKeys.TimeOfDay("QuietHours:End", Defaults.QuietHours.End),
        ConfigurationKeys.Duration("QuietHours:Spread", startOnly: false, TimeSpan.Zero, TimeSpan.FromHours(4), Defaults.QuietHours.Spread),
        ConfigurationKeys.Duration("Retention:Content", startOnly: false, TimeSpan.FromDays(1), 3 * Year, Defaults.Retention.Content),
        ConfigurationKeys.Duration("Retention:Record", startOnly: false, TimeSpan.Zero, 10 * Year, Defaults.Retention.Record),
        ConfigurationKeys.Duration("Retention:Places", startOnly: true, TimeSpan.FromDays(1), Year, Defaults.Retention.Places),
        ConfigurationKeys.Duration("Retention:Suppression:Email", startOnly: false, TimeSpan.FromDays(30), 10 * Year, Defaults.Retention.Suppression.Email),
        ConfigurationKeys.Duration("Retention:Suppression:Sms", startOnly: false, TimeSpan.FromDays(30), 10 * Year, Defaults.Retention.Suppression.Sms),
        ConfigurationKeys.Flag("Jobs:Enabled", Defaults.Jobs.Enabled),
        ConfigurationKeys.TimeOfDay("Jobs:RetentionAt", Defaults.Jobs.RetentionAt),
        ConfigurationKeys.Duration("Jobs:SubscriptionsInterval", startOnly: false, TimeSpan.FromMinutes(1), TimeSpan.FromDays(1), Defaults.Jobs.SubscriptionsInterval),
        ConfigurationKeys.Whole("Jobs:BatchSize", startOnly: false, 100, 100_000, Defaults.Jobs.BatchSize),
        ConfigurationKeys.Decimal("Jobs:StallFactor", 2, 100, Defaults.Jobs.StallFactor),
        ConfigurationKeys.Duration("Jobs:ClaimTimeout", startOnly: false, TimeSpan.Zero, TimeSpan.FromDays(1), Defaults.Jobs.ClaimTimeout),
        ConfigurationKeys.Whole("Database:Retries", startOnly: false, 0, 10, Defaults.Database.Retries),
        ConfigurationKeys.Duration("Alerts:Repeat", startOnly: false, TimeSpan.FromMinutes(5), TimeSpan.FromDays(1), Defaults.Alerts.Repeat),
        ConfigurationKeys.Duration("Unsubscribe:TokenLifetime", startOnly: false, TimeSpan.FromDays(180), 10 * Year, Defaults.Unsubscribe.TokenLifetime),
        ConfigurationKeys.Duration("Secrets:RetireAfter", startOnly: false, TimeSpan.FromDays(1), Year, Defaults.Secrets.RetireAfter),
        ConfigurationKeys.Count("Secrets:ContentKeys:MaxValues", 1, 4_294_967_296L, Defaults.Secrets.ContentKeys.MaxValues),
        ConfigurationKeys.Whole("Secrets:ContentKeys:ReserveBlock", startOnly: false, 100, 1_000_000, Defaults.Secrets.ContentKeys.ReserveBlock),
    ];

    /// <summary>The paths of <see cref="Keys"/>, in order.</summary>
    internal static IReadOnlyList<string> Paths { get; } = [.. Keys.Select(key => key.Path)];

    /// <summary>The keys whose rule reads another key, each lower bound the key's own rule leaves open.</summary>
    internal static IReadOnlyList<ConfigurationRule> Rules { get; } =
    [
        new("Dispatch:ClaimTimeout", ["Dispatch:Timeout"], value => (TimeSpan)value("Dispatch:ClaimTimeout") >= (2 * (TimeSpan)value("Dispatch:Timeout")) + TimeSpan.FromSeconds(30)),
        new("Dispatch:LagThreshold", ["Dispatch:Interval"], value => (TimeSpan)value("Dispatch:LagThreshold") > (TimeSpan)value("Dispatch:Interval")),
        new("Observer:LagThreshold", ["Dispatch:Interval"], value => (TimeSpan)value("Observer:LagThreshold") > (TimeSpan)value("Dispatch:Interval")),
        new("Retry:Max", ["Retry:Initial"], value => (TimeSpan)value("Retry:Max") >= (TimeSpan)value("Retry:Initial")),
        new("Retention:Record", ["Retention:Content"], value => (TimeSpan)value("Retention:Record") >= (TimeSpan)value("Retention:Content")),
        new("Jobs:ClaimTimeout", ["Dispatch:ClaimTimeout"], value => (TimeSpan)value("Jobs:ClaimTimeout") >= (TimeSpan)value("Dispatch:ClaimTimeout")),
    ];

    /// <summary>The write versions of the five keys, start only and checked only where their key is needed (<c>06</c> MSG-OPS-008).</summary>
    internal static IReadOnlyList<string> WriteVersions { get; } =
    [
        "Secrets:ContentKeys:WriteVersion",
        "Secrets:HashKeys:WriteVersion",
        "Secrets:TokenKeys:WriteVersion",
        "Secrets:SmsReportKey:WriteVersion",
        "Secrets:PushVapidKeys:WriteVersion",
    ];

    /// <summary>Reads a write version as the binder reads an <see cref="int"/>; none where it does not read.</summary>
    internal static int? ReadVersion(string text)
    {
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int version) ? version : null;
    }
}