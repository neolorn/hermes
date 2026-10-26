using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.Configuration;

namespace Hermes.Channels;

/// <summary>
/// The channels' checks of their configuration on the raw values, before binding (<c>06</c>
/// MSG-OPS-001): of the keys an application's use reads, each key's own rule, a required
/// key's presence, a sender against the registered SMS provider's rule too (<c>02</c>
/// MSG-SMS-002), then the rules that bound one key by others.
/// </summary>
internal static class ChannelsCheck
{
    private static readonly Dictionary<string, ConfigurationKey> Single = ChannelKeys.Keys.ToDictionary(key => key.Path, StringComparer.Ordinal);

    private static readonly Dictionary<string, ListKey> Listed = ChannelKeys.Lists.ToDictionary(key => key.Path, StringComparer.Ordinal);

    /// <summary>The first key of <c>08</c> section 4's order outside its rule in <paramref name="hermes"/>, the section <c>Hermes</c>, or none.</summary>
    /// <param name="hermes">The section <c>Hermes</c>.</param>
    /// <param name="use">The channels and shipped providers the application uses.</param>
    /// <param name="isSender">The registered SMS provider's rule for a sender, which narrows the base rule.</param>
    internal static string? InvalidKey(IConfiguration hermes, ChannelUse use, Func<string, bool> isSender)
    {
        return InvalidKeys(Raw(hermes), use, isSender).FirstOrDefault();
    }

    /// <summary>
    /// Answers each key of <paramref name="use"/> outside its own rule, absent where required,
    /// then each whose rule over others does not hold, in <c>08</c> section 4's order.
    /// </summary>
    internal static IEnumerable<string> InvalidKeys(IReadOnlyDictionary<string, string> raw, ChannelUse use, Func<string, bool> isSender)
    {
        IReadOnlyList<string> required = ChannelKeys.Required(use);
        List<string> invalid = [.. ChannelKeys.Read(use).Where(path => !Holds(raw, path, required.Contains(path, StringComparer.Ordinal), isSender))];
        if (invalid.Count > 0)
        {
            return invalid;
        }

        Func<string, object> value = Values(path => raw.GetValueOrDefault(path));
        return ChannelKeys.Rules.Where(rule => ChannelKeys.Read(use).Contains(rule.Path, StringComparer.Ordinal) && !rule.Holds(value)).Select(rule => rule.Path);
    }

    /// <summary>Whether the key at <paramref name="path"/> holds in <paramref name="raw"/>: present and within its rule, or absent and not required.</summary>
    internal static bool Holds(IReadOnlyDictionary<string, string> raw, string path, bool required, Func<string, bool> isSender)
    {
        if (Listed.TryGetValue(path, out ListKey? list))
        {
            string[] items = Items(raw, path);
            return items.Length == 0 ? !required : items.All(list.Item);
        }

        if (!raw.TryGetValue(path, out string? text))
        {
            return !required;
        }

        ConfigurationKey key = Single[path];
        return key.Read(text) is { } read && key.Within(read) && (!string.Equals(path, ChannelKeys.Sender, StringComparison.Ordinal) || isSender(text));
    }

    /// <summary>The items of the list at <paramref name="path"/> in <paramref name="raw"/>.</summary>
    internal static string[] Items(IReadOnlyDictionary<string, string> raw, string path)
    {
        string prefix = path + ":";
        return [.. raw.Where(entry => entry.Key.StartsWith(prefix, StringComparison.Ordinal)).Select(entry => entry.Value)];
    }

    /// <summary>Each single key's value, read from its raw text, or its default where it is absent.</summary>
    internal static Func<string, object> Values(Func<string, string?> raw)
    {
        return path => (raw(path) is string text ? Single[path].Read(text) : Single[path].Default)!;
    }

    /// <summary>
    /// The section's values of the channels' keys, by path under the section, a list's items
    /// under their own paths.
    /// </summary>
    internal static Dictionary<string, string> Raw(IConfiguration hermes)
    {
        Dictionary<string, string> raw = new(StringComparer.Ordinal);
        foreach (ConfigurationKey key in ChannelKeys.Keys)
        {
            if (hermes[key.Path] is string text)
            {
                raw[key.Path] = text;
            }
        }

        foreach (ListKey list in ChannelKeys.Lists)
        {
            foreach (IConfigurationSection item in hermes.GetSection(list.Path).GetChildren())
            {
                if (item.Value is string text)
                {
                    raw[$"{list.Path}:{item.Key}"] = text;
                }
            }
        }

        return raw;
    }

    /// <summary>The values of <paramref name="raw"/> that read, so binding them never throws.</summary>
    internal static Dictionary<string, string> Readable(IReadOnlyDictionary<string, string> raw)
    {
        return raw.Where(entry => !Single.TryGetValue(entry.Key, out ConfigurationKey? key) || key.Read(entry.Value) is not null)
            .Where(entry => !string.Equals(entry.Key, "Sms:ReportBaseAddress", StringComparison.Ordinal) || ChannelKeys.IsAddress(entry.Value))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
    }
}