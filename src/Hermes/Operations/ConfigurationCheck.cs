using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Configuration;

namespace Hermes.Operations;

/// <summary>
/// The engine's checks of its configuration on the raw values, before binding (<c>06</c>
/// MSG-OPS-001; <c>03</c> MSG-TPL-003): each key's own rule, then the rules that bound one
/// key by others, the languages and the time zone.
/// </summary>
internal static partial class ConfigurationCheck
{
    /// <summary>
    /// Answers the first key of <c>08</c> section 4's order outside its rule in
    /// <paramref name="hermes"/>, the section <c>Hermes</c>, or none.
    /// </summary>
    /// <param name="hermes">The section <c>Hermes</c>.</param>
    /// <param name="zones">The time zones the system offers.</param>
    /// <param name="publicBaseAddressRequired">Whether a declared optional category has a kind with email.</param>
    internal static string? InvalidKey(IConfiguration hermes, IReadOnlyCollection<TimeZoneInfo> zones, bool publicBaseAddressRequired = false)
    {
        ArgumentNullException.ThrowIfNull(hermes);
        ArgumentNullException.ThrowIfNull(zones);

        string? address = hermes["PublicBaseAddress"];
        if (address is null ? publicBaseAddressRequired : !IsPublicBaseAddress(address))
        {
            return "PublicBaseAddress";
        }

        string[]? languages = Languages(hermes.GetSection("Languages"));
        if (languages is null)
        {
            return "Languages";
        }

        string? defaultLanguage = hermes["DefaultLanguage"];
        if (defaultLanguage is null || !languages.Contains(defaultLanguage, StringComparer.OrdinalIgnoreCase))
        {
            return "DefaultLanguage";
        }

        if (!IsTimeZone(hermes["TimeZone"] ?? "UTC", zones))
        {
            return "TimeZone";
        }

        return InvalidKeys(path => hermes[path]).FirstOrDefault();
    }

    /// <summary>
    /// Answers each key of <see cref="ConfigurationRules.Keys"/> outside its own rule, then
    /// each key whose rule over others does not hold, given each key's raw value.
    /// </summary>
    internal static IEnumerable<string> InvalidKeys(Func<string, string?> raw)
    {
        List<string> invalid = [.. ConfigurationRules.Keys.Where(key => raw(key.Path) is string text && !Reads(key, text)).Select(key => key.Path)];
        if (invalid.Count > 0)
        {
            return invalid;
        }

        Func<string, object> value = Values(raw);
        return ConfigurationRules.Rules.Where(rule => !rule.Holds(value)).Select(rule => rule.Path);
    }

    /// <summary>Whether <paramref name="text"/> reads as a value of <paramref name="key"/> within its own rule.</summary>
    internal static bool Reads(ConfigurationKey key, string text)
    {
        return key.Read(text) is { } value && key.Within(value);
    }

    /// <summary>Each key's value, read from its raw text, or its default where it is absent.</summary>
    internal static Func<string, object> Values(Func<string, string?> raw)
    {
        MessagingOptions defaults = new();
        Dictionary<string, ConfigurationKey> keys = ConfigurationRules.Keys.ToDictionary(key => key.Path, StringComparer.Ordinal);
        return path => (raw(path) is string text ? keys[path].Read(text) : keys[path].Default(defaults))!;
    }

    /// <summary>Whether <paramref name="text"/> is an absolute <c>https</c> address.</summary>
    internal static bool IsPublicBaseAddress(string text)
    {
        return Uri.TryCreate(text, UriKind.Absolute, out Uri? address) && string.Equals(address.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal);
    }

    /// <summary>
    /// Whether <paramref name="identifier"/> is an IANA time zone identifier the system offers,
    /// on a system offering a time zone besides UTC (<c>06</c> MSG-OPS-001).
    /// </summary>
    internal static bool IsTimeZone(string identifier, IReadOnlyCollection<TimeZoneInfo> zones)
    {
        if (!zones.Any(zone => zone.BaseUtcOffset != TimeSpan.Zero || zone.SupportsDaylightSavingTime))
        {
            return false;
        }

        return identifier.Length > 0
            && TimeZoneInfo.TryFindSystemTimeZoneById(identifier, out TimeZoneInfo? zone)
            && zone.HasIanaId
            && string.Equals(zone.Id, identifier, StringComparison.Ordinal);
    }

    /// <summary>
    /// The languages of <paramref name="section"/>: one or more BCP 47 tags, each resolving to a
    /// predefined culture other than the invariant one (<c>03</c> MSG-TPL-003); none where they
    /// do not.
    /// </summary>
    internal static string[]? Languages(IConfigurationSection section)
    {
        string?[] tags = [.. section.GetChildren().Select(child => child.GetChildren().Any() ? null : child.Value)];
        if (section.Value is not null || tags.Length == 0)
        {
            return null;
        }

        foreach (string? tag in tags)
        {
            if (tag is null || !LanguageTag().IsMatch(tag) || Culture(tag) is null)
            {
                return null;
            }
        }

        return [.. tags.Cast<string>()];
    }

    /// <summary>The predefined culture <paramref name="tag"/> resolves to, other than the invariant one; none where it resolves to none.</summary>
    internal static CultureInfo? Culture(string tag)
    {
        try
        {
            CultureInfo culture = CultureInfo.GetCultureInfo(tag, predefinedOnly: true);
            return culture.Equals(CultureInfo.InvariantCulture) ? null : culture;
        }
        catch (CultureNotFoundException)
        {
            // A tag no predefined culture has is outside the rule; the caller answers it as such.
            return null;
        }
    }

    // A BCP 47 tag's shape (RFC 5646 section 2.1): a primary language subtag of 2 to 8
    // letters, then subtags of 1 to 8 letters and digits, each after a hyphen.
    [GeneratedRegex(@"^[A-Za-z]{2,8}(-[A-Za-z0-9]{1,8})*\z", RegexOptions.CultureInvariant)]
    private static partial Regex LanguageTag();
}