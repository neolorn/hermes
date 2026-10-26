using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Hermes.Repository.Tests;

/// <summary>
/// The codes a built assembly can answer, against those <c>08</c> section 1 lists (<c>07</c>
/// MSG-ENG-007): every string of the form <c>messaging.&lt;area&gt;.&lt;name&gt;</c> it holds.
/// </summary>
internal static partial class CodeRules
{
    /// <summary>The codes of <c>08</c> section 1, the first column of its table.</summary>
    internal static IReadOnlySet<string> Listed(string reference)
    {
        HashSet<string> codes = new(StringComparer.Ordinal);
        bool inSection = false;
        foreach (string line in File.ReadLines(reference))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                inSection = line.StartsWith("## 1.", StringComparison.Ordinal);
            }
            else if (inSection && FirstCell().Match(line) is { Success: true } cell)
            {
                codes.Add(cell.Groups["code"].Value);
            }
        }

        return codes;
    }

    /// <summary>The strings of <paramref name="strings"/> shaped as a code that <paramref name="listed"/> does not hold.</summary>
    internal static IReadOnlyList<string> Violations(IEnumerable<string> strings, IReadOnlySet<string> listed)
    {
        return [.. strings.Where(text => Code().IsMatch(text) && !listed.Contains(text)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    [GeneratedRegex(@"^messaging\.[a-z]+\.[a-z]+$", RegexOptions.CultureInvariant)]
    private static partial Regex Code();

    [GeneratedRegex(@"^\| `(?<code>messaging\.[a-z]+\.[a-z]+)` \|", RegexOptions.CultureInvariant)]
    private static partial Regex FirstCell();
}