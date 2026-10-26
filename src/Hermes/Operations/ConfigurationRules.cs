using System;
using System.Collections.Generic;
using System.Globalization;

namespace Hermes.Operations;

/// <summary>
/// The engine's keys of <c>08</c> section 4 that hold one value, in that section's order, and
/// the rules that bound one key by others. A year in a rule is 365 days, as <c>08</c> gives
/// <c>Unsubscribe:TokenLifetime</c>'s default of 2 years (<c>05</c> MSG-API-005) as 730 days.
/// </summary>
internal static class ConfigurationRules
{
    private static readonly TimeSpan Year = TimeSpan.FromDays(365);

    internal static IReadOnlyList<ConfigurationKey> Keys { get; } =
    [
        Whole("Templates:MaxSteps", startOnly: true, 1_000, 10_000_000, options => options.Templates.MaxSteps),
        Whole("Templates:MaxRecursion", startOnly: true, 1, 100, options => options.Templates.MaxRecursion),
        Flag("Messages:Enabled", options => options.Messages.Enabled),
        Flag("Dispatch:Enabled", options => options.Dispatch.Enabled),
        Flag("Dispatch:Hosted", options => options.Dispatch.Hosted),
        Flag("Dispatch:Notify", options => options.Dispatch.Notify),
        Duration("Dispatch:Interval", startOnly: false, TimeSpan.FromSeconds(3), TimeSpan.FromMinutes(5), options => options.Dispatch.Interval),
        Whole("Dispatch:BatchSize", startOnly: false, 1, 1_000, options => options.Dispatch.BatchSize),
        Whole("Dispatch:MaxPasses", startOnly: true, 1, 32, options => options.Dispatch.MaxPasses),
        Duration("Dispatch:Timeout", startOnly: false, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(2), options => options.Dispatch.Timeout),
        Duration("Dispatch:ClaimTimeout", startOnly: false, TimeSpan.Zero, TimeSpan.FromHours(1), options => options.Dispatch.ClaimTimeout),
        Duration("Dispatch:LagThreshold", startOnly: false, TimeSpan.Zero, TimeSpan.FromDays(1), options => options.Dispatch.LagThreshold),
        Duration("Dispatch:MaxHold", startOnly: false, TimeSpan.FromHours(1), TimeSpan.FromDays(90), options => options.Dispatch.MaxHold),
        Flag("Observer:Enabled", options => options.Observer.Enabled),
        Whole("Observer:MaxCalls", startOnly: true, 1, 64, options => options.Observer.MaxCalls),
        Duration("Observer:LagThreshold", startOnly: false, TimeSpan.Zero, TimeSpan.FromDays(1), options => options.Observer.LagThreshold),
        Duration("Retry:Initial", startOnly: false, TimeSpan.FromSeconds(1), TimeSpan.FromHours(1), options => options.Retry.Initial),
        Decimal("Retry:Factor", 1, 10, options => options.Retry.Factor),
        Duration("Retry:Max", startOnly: false, TimeSpan.Zero, TimeSpan.FromDays(1), options => options.Retry.Max),
        Decimal("Retry:Jitter", 0, 1, options => options.Retry.Jitter),
        Whole("Retry:MaxAttempts", startOnly: false, 1, 50, options => options.Retry.MaxAttempts),
        Duration("Idempotency:Window", startOnly: false, TimeSpan.FromMinutes(1), TimeSpan.FromDays(30), options => options.Idempotency.Window),
        TimeOfDay("QuietHours:Start", options => options.QuietHours.Start),
        TimeOfDay("QuietHours:End", options => options.QuietHours.End),
        Duration("QuietHours:Spread", startOnly: false, TimeSpan.Zero, TimeSpan.FromHours(4), options => options.QuietHours.Spread),
        Duration("Retention:Content", startOnly: false, TimeSpan.FromDays(1), 3 * Year, options => options.Retention.Content),
        Duration("Retention:Record", startOnly: false, TimeSpan.Zero, 10 * Year, options => options.Retention.Record),
        Duration("Retention:Places", startOnly: true, TimeSpan.FromDays(1), Year, options => options.Retention.Places),
        Duration("Retention:Suppression:Email", startOnly: false, TimeSpan.FromDays(30), 10 * Year, options => options.Retention.Suppression.Email),
        Duration("Retention:Suppression:Sms", startOnly: false, TimeSpan.FromDays(30), 10 * Year, options => options.Retention.Suppression.Sms),
        Flag("Jobs:Enabled", options => options.Jobs.Enabled),
        TimeOfDay("Jobs:RetentionAt", options => options.Jobs.RetentionAt),
        Duration("Jobs:SubscriptionsInterval", startOnly: false, TimeSpan.FromMinutes(1), TimeSpan.FromDays(1), options => options.Jobs.SubscriptionsInterval),
        Whole("Jobs:BatchSize", startOnly: false, 100, 100_000, options => options.Jobs.BatchSize),
        Decimal("Jobs:StallFactor", 2, 100, options => options.Jobs.StallFactor),
        Duration("Jobs:ClaimTimeout", startOnly: false, TimeSpan.Zero, TimeSpan.FromDays(1), options => options.Jobs.ClaimTimeout),
        Whole("Database:Retries", startOnly: false, 0, 10, options => options.Database.Retries),
        Duration("Alerts:Repeat", startOnly: false, TimeSpan.FromMinutes(5), TimeSpan.FromDays(1), options => options.Alerts.Repeat),
        Duration("Unsubscribe:TokenLifetime", startOnly: false, TimeSpan.FromDays(180), 10 * Year, options => options.Unsubscribe.TokenLifetime),
        Duration("Secrets:RetireAfter", startOnly: false, TimeSpan.FromDays(1), Year, options => options.Secrets.RetireAfter),
        Count("Secrets:ContentKeys:MaxValues", 1, 4_294_967_296L, options => options.Secrets.ContentKeys.MaxValues),
        Whole("Secrets:ContentKeys:ReserveBlock", startOnly: false, 100, 1_000_000, options => options.Secrets.ContentKeys.ReserveBlock),
    ];

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

    private static ConfigurationKey Whole(string path, bool startOnly, int least, int most, Func<MessagingOptions, int> value)
    {
        return new(
            path,
            startOnly,
            text => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int read) ? read : null,
            read => (int)read >= least && (int)read <= most,
            options => value(options));
    }

    private static ConfigurationKey Count(string path, long least, long most, Func<MessagingOptions, long> value)
    {
        return new(
            path,
            false,
            text => long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long read) ? read : null,
            read => (long)read >= least && (long)read <= most,
            options => value(options));
    }

    /// <summary>A key whose rule says a decimal number, read in the invariant culture as the binder reads it (<c>08</c> section 4).</summary>
    private static ConfigurationKey Decimal(string path, double least, double most, Func<MessagingOptions, double> value)
    {
        return new(
            path,
            false,
            text => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double read) ? read : null,
            read => (double)read >= least && (double)read <= most,
            options => value(options));
    }

    private static ConfigurationKey Flag(string path, Func<MessagingOptions, bool> value)
    {
        // Every flag of 08 section 4 is start only.
        return new(path, true, text => bool.TryParse(text, out bool read) ? read : null, _ => true, options => value(options));
    }

    private static ConfigurationKey TimeOfDay(string path, Func<MessagingOptions, TimeOnly> value)
    {
        return new(
            path,
            false,
            text => TimeOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly read) ? read : null,
            _ => true,
            options => value(options));
    }

    private static ConfigurationKey Duration(string path, bool startOnly, TimeSpan least, TimeSpan most, Func<MessagingOptions, TimeSpan?> value)
    {
        return new(
            path,
            startOnly,
            text => TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out TimeSpan read) ? read : null,
            read => (TimeSpan)read >= least && (TimeSpan)read <= most,
            options => value(options));
    }
}