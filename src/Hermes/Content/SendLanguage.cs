using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Hermes.Content;

/// <summary>
/// A send's language and time zone (<c>01</c> MSG-SEND-003; <c>03</c> MSG-TPL-003): a tag
/// matched to a declared language ignoring case, and failing that by its primary subtag alone,
/// the default otherwise; a time zone that does not resolve replaced by the deployment's.
/// </summary>
internal static class SendLanguage
{
    internal const string LeftToRight = "ltr";

    internal const string RightToLeft = "rtl";

    /// <summary>The declared language <paramref name="tag"/> matches, as it is declared, or <paramref name="fallback"/>'s declared form.</summary>
    internal static string Resolve(string? tag, IReadOnlyList<string> declared, string fallback)
    {
        if (tag is not null)
        {
            if (declared.FirstOrDefault(language => string.Equals(language, tag, StringComparison.OrdinalIgnoreCase)) is { } exact)
            {
                return exact;
            }

            int dash = tag.IndexOf('-', StringComparison.Ordinal);
            string primary = dash < 0 ? tag : tag[..dash];
            if (declared.FirstOrDefault(language => string.Equals(language, primary, StringComparison.OrdinalIgnoreCase)) is { } matched)
            {
                return matched;
            }
        }

        return declared.First(language => string.Equals(language, fallback, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The culture of a declared language, a predefined one, which the startup check made sure of.</summary>
    internal static CultureInfo Culture(string language)
    {
        return CultureInfo.GetCultureInfo(language, predefinedOnly: true);
    }

    /// <summary><c>rtl</c> where the language's culture's text runs right to left, <c>ltr</c> otherwise.</summary>
    internal static string Direction(string language)
    {
        return Culture(language).TextInfo.IsRightToLeft ? RightToLeft : LeftToRight;
    }

    /// <summary>The identifier of the time zone <paramref name="zone"/> names where it resolves, <paramref name="fallback"/> otherwise.</summary>
    internal static string Zone(string? zone, string fallback)
    {
        return zone is not null && TimeZoneInfo.TryFindSystemTimeZoneById(zone, out _) ? zone : fallback;
    }
}