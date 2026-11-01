using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.Configuration;

namespace Hermes.Conversations;

/// <summary>
/// The package's keys under <c>Hermes:Conversations:</c>, checked raw at start where the
/// package is registered (M-031). An unknown key there fails the start; the engine's own keys
/// keep their check.
/// </summary>
internal static class ConversationsConfiguration
{
    /// <summary>The section's name under <c>Hermes</c>, as <c>08</c> section 4 spells its keys.</summary>
    internal const string Section = "Conversations";

    /// <summary>The fixed keys the package knows, as <c>08</c> section 4 spells them; a topic's keys follow its pattern.</summary>
    internal static IReadOnlySet<string> Known { get; } = new HashSet<string>([AwayKind, .. Support.SupportConfiguration.Fixed], StringComparer.Ordinal);

    /// <summary>The kind a person who is away is told of a conversation's message as (<c>09</c> MSG-CONV-008).</summary>
    internal const string AwayKind = "Conversations:AwayKind";

    /// <summary>
    /// The first key, in ordinal order, at or under <paramref name="hermes"/>'s section
    /// <c>Conversations</c> that holds a value and that the package does not know, spelled as
    /// <c>08</c> section 4 spells keys (<c>Conversations:Retention:Visitor</c>), or none.
    /// </summary>
    /// <param name="hermes">The section <c>Hermes</c>.</param>
    internal static string? UnknownKey(IConfiguration hermes)
    {
        ArgumentNullException.ThrowIfNull(hermes);
        return Keys(hermes.GetSection(Section), Section).Where(key => !Known.Contains(key) && !Support.SupportConfiguration.IsTopicKey(key)).Order(StringComparer.Ordinal).FirstOrDefault();
    }

    /// <summary>
    /// Each key at or under <paramref name="section"/> that holds a value. A section with keys
    /// beneath it and an empty value, as a command line gives the parents of the keys it sets,
    /// holds none of its own.
    /// </summary>
    private static IEnumerable<string> Keys(IConfigurationSection section, string path)
    {
        IConfigurationSection[] children = [.. section.GetChildren()];
        if (section.Value is { } value && (children.Length == 0 || value.Length > 0))
        {
            yield return path;
        }

        foreach (IConfigurationSection child in children)
        {
            foreach (string key in Keys(child, path + ConfigurationPath.KeyDelimiter + child.Key))
            {
                yield return key;
            }
        }
    }
}