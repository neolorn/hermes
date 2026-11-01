using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Configuration;

namespace Hermes.Conversations.Attachments;

/// <summary>
/// The attachments' keys under <c>Hermes:Conversations:Attachments:</c>, read at start (<c>09</c>
/// MSG-CONV-007; <c>08</c> section 4): the switch, the public group's switch, the largest file
/// and the allowed media types.
/// </summary>
internal static partial class AttachmentConfiguration
{
    internal const string Enabled = "Conversations:Attachments:Enabled";

    internal const string Public = "Conversations:Attachments:Public";

    internal const string MaxBytes = "Conversations:Attachments:MaxBytes";

    internal const string Types = "Conversations:Attachments:Types";

    private const long LeastBytes = 1024;

    private const long MostBytes = 50L * 1024 * 1024;

    /// <summary>The fixed keys, a type's key following its pattern.</summary>
    internal static IReadOnlyList<string> Fixed { get; } = [Enabled, Public, MaxBytes];

    /// <summary>Whether <paramref name="key"/> is one of the allowed types' list.</summary>
    internal static bool IsTypeKey(string key)
    {
        return TypeKey().IsMatch(key);
    }

    /// <summary>The settings, or the first key outside its rule.</summary>
    internal static (AttachmentSettings? Settings, string? Invalid) Read(IConfiguration hermes)
    {
        ArgumentNullException.ThrowIfNull(hermes);
        if (!Flag(hermes, Enabled, out bool enabled))
        {
            return (null, Enabled);
        }

        if (!Flag(hermes, Public, out bool open))
        {
            return (null, Public);
        }

        long most = 10L * 1024 * 1024;
        if (hermes[MaxBytes] is { } text && (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out most) || most < LeastBytes || most > MostBytes))
        {
            return (null, MaxBytes);
        }

        List<string> types = [];
        IConfigurationSection section = hermes.GetSection(Types);
        if (section.Value is { Length: > 0 })
        {
            return (null, Types);
        }

        foreach (IConfigurationSection type in section.GetChildren())
        {
            if (type.Value is not { } name || !MediaType().IsMatch(name) || types.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                return (null, Types + ConfigurationPath.KeyDelimiter + type.Key);
            }

            types.Add(AttachmentFiles.Small(name));
        }

        return (new AttachmentSettings
        {
            Enabled = enabled,
            Public = enabled && open,
            MaxBytes = most,
            Types = types.Count > 0 ? types : ["image/jpeg", "image/png", "application/pdf"],
        }, null);
    }

    private static bool Flag(IConfiguration hermes, string key, out bool value)
    {
        value = false;
        return hermes[key] is not { } text || bool.TryParse(text, out value);
    }

    [GeneratedRegex(@"^Conversations:Attachments:Types:[^:]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex TypeKey();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9!#$&^_.+-]{0,126}/[A-Za-z0-9][A-Za-z0-9!#$&^_.+-]{0,126}\z", RegexOptions.CultureInvariant)]
    private static partial Regex MediaType();
}

/// <summary>The attachments' settings (<c>09</c> MSG-CONV-007).</summary>
internal sealed class AttachmentSettings
{
    /// <summary>Whether conversations take attachments.</summary>
    internal bool Enabled { get; init; }

    /// <summary>Whether the public group takes them too.</summary>
    internal bool Public { get; init; }

    /// <summary>The largest file taken, in bytes.</summary>
    internal long MaxBytes { get; init; } = 10L * 1024 * 1024;

    /// <summary>The allowed media types, lower-case.</summary>
    internal IReadOnlyList<string> Types { get; init; } = ["image/jpeg", "image/png", "application/pdf"];
}