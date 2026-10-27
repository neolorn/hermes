using System;
using System.Collections.Generic;

using Hermes.Core;

namespace Hermes.Content;

/// <summary>
/// The rule of a send's values (<c>03</c> MSG-TPL-002): a name is 1 to 64 letters, digits and
/// <c>_</c>, starting with a letter, no two equal ignoring case, none reserved at the top
/// level; a list holds maps of scalar values and no list. A value outside it is a fault of
/// the calling code (<c>07</c> MSG-ENG-003).
/// </summary>
internal static class SendValues
{
    private const int MostNameCharacters = 64;

    /// <summary>Throws <see cref="ArgumentException"/> where <paramref name="values"/> is outside the rule.</summary>
    internal static void Check(IReadOnlyDictionary<string, SendValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        Entries(values, topLevel: true);
    }

    private static void Entries(IReadOnlyDictionary<string, SendValue> values, bool topLevel)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string name, SendValue value) in values)
        {
            if (!IsName(name))
            {
                throw new ArgumentException($"The value name '{name}' is not 1 to 64 letters, digits and '_' starting with a letter.", nameof(values));
            }

            if (!seen.Add(name))
            {
                throw new ArgumentException($"Two value names are '{name}' ignoring case.", nameof(values));
            }

            if (topLevel && TemplateNames.Reserved.Contains(name))
            {
                throw new ArgumentException($"The value name '{name}' is the library's own.", nameof(values));
            }

            switch (value)
            {
                case null:
                    throw new ArgumentException($"The value '{name}' is none.", nameof(values));
                case SendValue.PlainText { Value: null }:
                    throw new ArgumentException($"The value '{name}' is none.", nameof(values));
                case SendValue.List when !topLevel:
                    throw new ArgumentException($"The list '{name}' is inside a list.", nameof(values));
                case SendValue.List list:
                    ArgumentNullException.ThrowIfNull(list.Items, nameof(values));
                    foreach (IReadOnlyDictionary<string, SendValue> item in list.Items)
                    {
                        if (item is null)
                        {
                            throw new ArgumentException($"An item of the list '{name}' is none.", nameof(values));
                        }

                        Entries(item, topLevel: false);
                    }

                    break;
                default:
                    break;
            }
        }
    }

    private static bool IsName(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > MostNameCharacters || !char.IsAsciiLetter(name[0]))
        {
            return false;
        }

        foreach (char character in name)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character != '_')
            {
                return false;
            }
        }

        return true;
    }
}