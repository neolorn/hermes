using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Hermes.Repository.Tests;

/// <summary>
/// The secret scanner's configuration (<c>07</c> MSG-ENG-001): gitleaks' default, extended by
/// entries of one form only, each for a published test vector the tests hold as a literal:
/// one file under <c>tests/</c> and one exact value, both required to match, with the document
/// and section as its reason.
/// </summary>
/// <remarks>
/// The configuration is read as the small part of TOML it may use: a title, the
/// <c>[extend]</c> table and <c>[[allowlists]]</c> tables of one-line keys, each value a
/// string, a boolean or a one-line array of strings.
/// </remarks>
internal static partial class ScannerConfigurationRules
{
    internal static string File(string root)
    {
        return Path.Combine(root, "build", "gates", "gitleaks.toml");
    }

    /// <summary>Answers each part of the configuration of the repository at <paramref name="root"/> outside the rule.</summary>
    internal static IReadOnlyList<string> Violations(string root)
    {
        string file = File(root);
        if (!System.IO.File.Exists(file))
        {
            return ["build/gates/gitleaks.toml is missing."];
        }

        List<string> violations = [];
        List<(string Table, Dictionary<string, string> Keys)> tables = [("", new(StringComparer.Ordinal))];
        int number = 0;
        foreach (string raw in System.IO.File.ReadLines(file))
        {
            number++;
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            Match header = TablePattern().Match(line);
            if (header.Success)
            {
                tables.Add((header.Groups["name"].Value, new(StringComparer.Ordinal)));
                continue;
            }

            Match key = KeyPattern().Match(line);
            if (!key.Success || !tables[^1].Keys.TryAdd(key.Groups["key"].Value, key.Groups["value"].Value.Trim()))
            {
                violations.Add($"gitleaks.toml line {number} is not a key the rule allows once in its table.");
            }
        }

        foreach ((string table, Dictionary<string, string> keys) in tables)
        {
            violations.AddRange(table switch
            {
                "" => TopViolations(keys),
                "[extend]" => ExtendViolations(keys),
                "[[allowlists]]" => AllowListViolations(root, keys),
                _ => [$"gitleaks.toml adds the table {table}, which is not of the rule's form."],
            });
        }

        if (!tables.Any(table => string.Equals(table.Table, "[extend]", StringComparison.Ordinal)))
        {
            violations.Add("gitleaks.toml does not extend gitleaks' default.");
        }

        return violations;
    }

    private static IEnumerable<string> TopViolations(Dictionary<string, string> keys)
    {
        return keys.Keys.Where(key => !string.Equals(key, "title", StringComparison.Ordinal))
            .Select(key => $"gitleaks.toml sets {key} at its top, which is not of the rule's form.");
    }

    private static IEnumerable<string> ExtendViolations(Dictionary<string, string> keys)
    {
        if (keys.Count != 1 || !keys.TryGetValue("useDefault", out string? value) || !string.Equals(value, "true", StringComparison.Ordinal))
        {
            yield return "gitleaks.toml's [extend] table holds more than useDefault = true.";
        }
    }

    private static IEnumerable<string> AllowListViolations(string root, Dictionary<string, string> keys)
    {
        string[] expected = ["description", "condition", "paths", "regexes"];
        if (keys.Count != expected.Length || !expected.All(keys.ContainsKey))
        {
            yield return "An allow-list entry of gitleaks.toml does not hold exactly description, condition, paths and regexes.";
            yield break;
        }

        string? description = Text(keys["description"]);
        if (description is null || !ReasonPattern().IsMatch(description))
        {
            yield return "An allow-list entry of gitleaks.toml does not give a document and its section as its reason.";
        }

        if (!string.Equals(Text(keys["condition"]), "AND", StringComparison.Ordinal))
        {
            yield return "An allow-list entry of gitleaks.toml does not require both its file and its value to match.";
        }

        string? path = Exact(keys["paths"]);
        if (path is null)
        {
            yield return "An allow-list entry of gitleaks.toml does not name one file exactly.";
        }
        else if (!path.StartsWith("tests/", StringComparison.Ordinal))
        {
            yield return $"An allow-list entry of gitleaks.toml names {path}, which is not under tests/.";
        }
        else if (!System.IO.File.Exists(Path.Combine(root, path)))
        {
            yield return $"An allow-list entry of gitleaks.toml names {path}, which does not exist.";
        }

        if (Exact(keys["regexes"]) is null)
        {
            yield return "An allow-list entry of gitleaks.toml does not name one value exactly.";
        }
    }

    /// <summary>
    /// The literal an array of one string, <c>['''^literal$''']</c>, matches exactly, or none
    /// when the array is not of that form.
    /// </summary>
    private static string? Exact(string value)
    {
        Match array = SingleArrayPattern().Match(value);
        if (!array.Success)
        {
            return null;
        }

        string? pattern = Text(array.Groups["item"].Value.Trim());
        if (pattern is null || pattern.Length < 3 || !pattern.StartsWith('^') || !pattern.EndsWith('$'))
        {
            return null;
        }

        string body = pattern[1..^1];
        string literal = Regex.Unescape(body);
        return string.Equals(Regex.Escape(literal), body, StringComparison.Ordinal) ? literal : null;
    }

    /// <summary>The text of a TOML string, or none when the value is not one.</summary>
    private static string? Text(string value)
    {
        foreach (string quote in new[] { "'''", "\"", "'" })
        {
            if (value.Length >= 2 * quote.Length && value.StartsWith(quote, StringComparison.Ordinal) && value.EndsWith(quote, StringComparison.Ordinal))
            {
                string text = value[quote.Length..^quote.Length];
                return string.Equals(quote, "\"", StringComparison.Ordinal) ? text.Replace("\\\"", "\"", StringComparison.Ordinal).Replace("\\\\", "\\", StringComparison.Ordinal) : text;
            }
        }

        return null;
    }

    [GeneratedRegex(@"^(?<name>\[\[?[A-Za-z.]+\]\]?)$")]
    private static partial Regex TablePattern();

    [GeneratedRegex(@"^(?<key>[A-Za-z]+)\s*=\s*(?<value>.+)$")]
    private static partial Regex KeyPattern();

    [GeneratedRegex(@"^\[\s*(?<item>[^,\]]+?)\s*,?\s*\]$")]
    private static partial Regex SingleArrayPattern();

    [GeneratedRegex(@"^RFC \d+ section \d+(\.\d+)*\b")]
    private static partial Regex ReasonPattern();
}