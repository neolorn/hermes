using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace Hermes.Content;

/// <summary>
/// The filters a template may use, each with the arguments it requires (<c>03</c> MSG-TPL-004,
/// MSG-TPL-007); every other filter is refused.
/// </summary>
internal static class FilterRules
{
    internal const string FormatDate = "format_date";

    internal const string FormatNumber = "format_number";

    /// <summary>The standard numeric format's highest precision that is allowed (<c>03</c> MSG-TPL-004).</summary>
    internal const int MaxPrecision = 28;

    private static readonly FrozenDictionary<string, int> Required = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["append"] = 1,
        ["capitalize"] = 0,
        ["downcase"] = 0,
        ["upcase"] = 0,
        ["lstrip"] = 0,
        ["rstrip"] = 0,
        ["strip"] = 0,
        ["prepend"] = 1,
        ["remove"] = 1,
        ["remove_first"] = 1,
        ["replace"] = 2,
        ["replace_first"] = 2,
        ["slice"] = 1,
        ["strip_html"] = 0,
        ["strip_newlines"] = 0,
        ["truncate"] = 0,
        ["truncatewords"] = 0,
        ["url_encode"] = 0,
        ["abs"] = 0,
        ["at_least"] = 1,
        ["at_most"] = 1,
        ["ceil"] = 0,
        ["divided_by"] = 1,
        ["floor"] = 0,
        ["minus"] = 1,
        ["modulo"] = 1,
        ["plus"] = 1,
        ["round"] = 0,
        ["times"] = 1,
        ["first"] = 0,
        ["last"] = 0,
        ["join"] = 0,
        ["map"] = 1,
        ["reverse"] = 0,
        ["size"] = 0,
        ["sort"] = 0,
        ["sort_natural"] = 0,
        ["uniq"] = 0,
        ["where"] = 1,
        ["compact"] = 0,
        ["default"] = 1,
        [FormatDate] = 1,
        [FormatNumber] = 1,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The allowed filters' names.</summary>
    internal static IEnumerable<string> Names => Required.Keys;

    /// <summary>The arguments <paramref name="filter"/> requires, or none where it is not allowed.</summary>
    internal static int? Arguments(string filter)
    {
        return Required.TryGetValue(filter, out int count) ? count : null;
    }

    /// <summary>
    /// Whether <paramref name="format"/>, given to <c>format_number</c>, is a standard numeric
    /// format (a letter and a precision) whose precision is above <see cref="MaxPrecision"/>.
    /// </summary>
    internal static bool IsTooPrecise(string format)
    {
        if (format.Length < 2 || !char.IsAsciiLetter(format[0]))
        {
            return false;
        }

        for (int index = 1; index < format.Length; index++)
        {
            if (!char.IsAsciiDigit(format[index]))
            {
                return false;
            }
        }

        ReadOnlySpan<char> digits = format.AsSpan(1).TrimStart('0');
        return digits.Length > 2 || (digits.Length > 0 && int.Parse(digits, System.Globalization.CultureInfo.InvariantCulture) > MaxPrecision);
    }
}