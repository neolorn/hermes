using System;
using System.Collections.Generic;
using System.Linq;

namespace Hermes.Channels;

/// <summary>
/// The channels' keys of <c>08</c> section 4, in that section's order, and the rules that
/// bound one key by others: <c>Dispatch:Timeout</c>, which the shipped providers read where
/// the application registers the providers alone, and the keys under <c>Email:</c>,
/// <c>Sms:</c> and <c>Push:</c>, each read and checked where its part is used. A year in a
/// rule is 365 days, as the engine's rules take it.
/// </summary>
internal static class ChannelKeys
{
    internal const string Timeout = "Dispatch:Timeout";

    internal const string FromAddress = "Email:From:Address";

    internal const string Sender = "Sms:Sender";

    internal const string AllowedPrefixes = "Sms:AllowedPrefixes";

    internal const string Subject = "Push:Subject";

    internal const string AllowedHosts = "Push:AllowedHosts";

    private static readonly TimeSpan Year = TimeSpan.FromDays(365);

    private static readonly ChannelsOptions Defaults = new();

    /// <summary>The keys holding one value.</summary>
    internal static IReadOnlyList<ConfigurationKey> Keys { get; } =
    [
        ConfigurationKeys.Duration(Timeout, startOnly: false, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(2), Defaults.Dispatch.Timeout),
        ConfigurationKeys.Text(FromAddress, AddressRule.IsAddress),
        ConfigurationKeys.Text("Email:From:DisplayName", text => TextLength.ScalarValues(text) is >= 1 and <= 128),
        ConfigurationKeys.Whole("Email:Rate", startOnly: false, 1, 1_000, Defaults.Email.Rate),
        ConfigurationKeys.Whole("Email:MaxBytes", startOnly: false, 10 * 1024, 25 * 1024 * 1024, Defaults.Email.MaxBytes),
        ConfigurationKeys.Text(Sender, SenderRule.IsSender),
        ConfigurationKeys.Whole("Sms:Rate", startOnly: false, 1, 1_000, Defaults.Sms.Rate),
        ConfigurationKeys.Text("Sms:ReportBaseAddress", IsAddress),
        ConfigurationKeys.Whole("Sms:WrongKeyRate", startOnly: false, 1, 10_000, Defaults.Sms.WrongKeyRate),
        ConfigurationKeys.Whole("Sms:ReportRejectThreshold", startOnly: false, 1, 10_000, Defaults.Sms.ReportRejectThreshold),
        ConfigurationKeys.Duration("Sms:ReportRejectWindow", startOnly: false, TimeSpan.FromMinutes(1), TimeSpan.FromDays(1), Defaults.Sms.ReportRejectWindow),
        ConfigurationKeys.Duration("Sms:ReportHold", startOnly: false, TimeSpan.FromMinutes(1), TimeSpan.FromDays(1), Defaults.Sms.ReportHold),
        ConfigurationKeys.Whole("Sms:ReportHoldMax", startOnly: false, 10, 1_000_000, Defaults.Sms.ReportHoldMax),
        ConfigurationKeys.Duration("Sms:BalancePoll", startOnly: false, TimeSpan.FromMinutes(1), TimeSpan.FromDays(1), Defaults.Sms.BalancePoll),
        ConfigurationKeys.Amount("Sms:BalanceFloor", Defaults.Sms.BalanceFloor),
        ConfigurationKeys.Duration("Sms:FloorWarning", startOnly: false, TimeSpan.FromHours(1), TimeSpan.FromDays(30), Defaults.Sms.FloorWarning),
        ConfigurationKeys.Duration("Sms:DrainWindow", startOnly: false, TimeSpan.FromMinutes(5), TimeSpan.FromDays(1), Defaults.Sms.DrainWindow),
        ConfigurationKeys.Decimal("Sms:DrainFactor", 1.5, 100, Defaults.Sms.DrainFactor),
        ConfigurationKeys.Duration("Sms:DrainBaseline", startOnly: false, TimeSpan.Zero, TimeSpan.FromDays(90), Defaults.Sms.DrainBaseline),
        ConfigurationKeys.Duration("Sms:DrainMinHistory", startOnly: false, TimeSpan.Zero, TimeSpan.FromDays(90), Defaults.Sms.DrainMinHistory),
        ConfigurationKeys.Amount("Sms:DrainMinSpend", Defaults.Sms.DrainMinSpend),
        ConfigurationKeys.Duration("Sms:BalanceUnreadableAfter", startOnly: false, TimeSpan.FromMinutes(5), TimeSpan.FromDays(1), Defaults.Sms.BalanceUnreadableAfter),
        ConfigurationKeys.Text(Subject, IsSubject),
        ConfigurationKeys.Whole("Push:MaxSubscriptions", startOnly: false, 1, 100, Defaults.Push.MaxSubscriptions),
        ConfigurationKeys.Whole("Push:Rate", startOnly: false, 1, 1_000, Defaults.Push.Rate),
        ConfigurationKeys.Duration("Push:IdleRetention", startOnly: false, TimeSpan.FromDays(7), 3 * Year, Defaults.Push.IdleRetention),
        ConfigurationKeys.Whole("Push:AuthFailThreshold", startOnly: false, 1, 10_000, Defaults.Push.AuthFailThreshold),
        ConfigurationKeys.Duration("Push:AuthFailWindow", startOnly: false, TimeSpan.FromMinutes(1), TimeSpan.FromDays(1), Defaults.Push.AuthFailWindow),
    ];

    /// <summary>The keys holding a list.</summary>
    internal static IReadOnlyList<ListKey> Lists { get; } =
    [
        new(AllowedPrefixes, SenderRule.IsPrefix, []),
        new(AllowedHosts, HostRule.IsAllowedHostEntry, PushChannelOptions.DefaultAllowedHosts),
    ];

    /// <summary>The keys whose rule reads another key, each lower bound the key's own rule leaves open.</summary>
    internal static IReadOnlyList<ConfigurationRule> Rules { get; } =
    [
        new("Sms:DrainBaseline", ["Sms:DrainWindow"], value => (TimeSpan)value("Sms:DrainBaseline") >= 2 * (TimeSpan)value("Sms:DrainWindow")),
        new("Sms:DrainMinHistory", ["Sms:DrainWindow", "Sms:DrainBaseline"], value => (TimeSpan)value("Sms:DrainMinHistory") >= (TimeSpan)value("Sms:DrainWindow")
            && (TimeSpan)value("Sms:DrainMinHistory") <= (TimeSpan)value("Sms:DrainBaseline")),
    ];

    /// <summary>Every key, single or a list, in <c>08</c> section 4's order.</summary>
    internal static IReadOnlyList<string> Order { get; } = [.. Keys.Select(key => key.Path).SelectMany(path => path switch
    {
        Sender => [Sender, AllowedPrefixes],
        Subject => [Subject, AllowedHosts],
        _ => new[] { path },
    })];

    /// <summary>
    /// The keys <paramref name="use"/> reads and checks: <c>Dispatch:Timeout</c> where the
    /// providers are registered alone, each used channel's own keys, and
    /// <c>Push:Subject</c> where the shipped push provider is used.
    /// </summary>
    internal static IReadOnlyList<string> Read(ChannelUse use)
    {
        return [.. Order.Where(path => path switch
        {
            Timeout => use.ProvidersAlone,
            Subject => use.ShippedPush,
            _ when path.StartsWith("Email:", StringComparison.Ordinal) => use.Email,
            _ when path.StartsWith("Sms:", StringComparison.Ordinal) => use.Sms,
            _ when path.StartsWith("Push:", StringComparison.Ordinal) => use.Push,
            _ => false,
        })];
    }

    /// <summary>The keys <paramref name="use"/> requires: a used channel's sender, its allowed prefixes, and the shipped push provider's subject.</summary>
    internal static IReadOnlyList<string> Required(ChannelUse use)
    {
        List<string> required = [];
        if (use.Email)
        {
            required.Add(FromAddress);
        }

        if (use.Sms)
        {
            required.AddRange([Sender, AllowedPrefixes]);
        }

        if (use.ShippedPush)
        {
            required.Add(Subject);
        }

        return required;
    }

    /// <summary>
    /// Whether <paramref name="text"/> is an absolute address, as <c>Sms:ReportBaseAddress</c>
    /// takes one: the report group is an HTTP route (<c>05</c>), so its address is <c>http</c>
    /// or <c>https</c>.
    /// </summary>
    internal static bool IsAddress(string text)
    {
        return Uri.TryCreate(text, UriKind.Absolute, out Uri? address) && (address.Scheme == Uri.UriSchemeHttps || address.Scheme == Uri.UriSchemeHttp);
    }

    /// <summary>Whether <paramref name="text"/> is a VAPID subject: a <c>mailto:</c> or <c>https:</c> URI (<c>02</c> MSG-PUSH-001).</summary>
    internal static bool IsSubject(string text)
    {
        return Uri.TryCreate(text, UriKind.Absolute, out Uri? subject) && (subject.Scheme == Uri.UriSchemeMailto || subject.Scheme == Uri.UriSchemeHttps);
    }
}