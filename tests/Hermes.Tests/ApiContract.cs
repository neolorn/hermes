using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Hermes.Tests;

/// <summary>
/// What <c>docs/spec/05-api-contract.md</c> gives the endpoints (<c>07</c> MSG-ENG-006), as lines
/// in the form of <see cref="MappedEndpoints.Lines"/>: each route, each of its statuses, each
/// member of each JSON body it answers, and each member of the body it reads. A table of routes
/// gives each route in backticks, each status in bold, a status's body as the backticked
/// <c>{ ... }</c> after it and the body a route reads as the one in its <c>Body</c> cell; a
/// status naming a code answers the refusal body of MSG-API-001, and one written "as for
/// <c>POST</c>" answers as that method's route does. MSG-API-001 adds 400, 413 and 415, each a
/// refusal, to a route reading a JSON body; MSG-API-002 adds 401 to each route of a group whose
/// text says its routes need a signed-in person, save one whose answers say it needs none.
/// </summary>
internal static partial class ApiContract
{
    private const string Header = "| Method and route |";

    private const string SignedIn = "need a signed-in person (MSG-API-002)";

    private const string NoneSignedIn = "Needs no signed-in person";

    private static readonly int[] JsonRefusals = [400, 413, 415];

    /// <summary>The repository's <c>05</c> as lines, its routes under <see cref="Endpoints.Prefix"/>.</summary>
    internal static SortedSet<string> Read()
    {
        return Read(Text(), Endpoints.Prefix);
    }

    /// <summary>The text of the repository's <c>05</c>.</summary>
    internal static string Text()
    {
        return File.ReadAllText(Path.Combine(ReleaseSet.Root(), "docs", "spec", "05-api-contract.md"));
    }

    /// <summary><paramref name="text"/>, a chapter in the form of <c>05</c>, as lines, its routes under <paramref name="prefix"/>.</summary>
    internal static SortedSet<string> Read(string text, string prefix)
    {
        ArgumentNullException.ThrowIfNull(text);
        List<(string Prose, List<string[]> Rows)> sections = Sections(text);
        string refusal = sections.SelectMany(section => Backticked(section.Prose)).First(token => token.StartsWith("{ \"code\"", StringComparison.Ordinal));

        // A status's body by route and status, null for one with no JSON body.
        Dictionary<(string Method, string Path, int Status), string?> answers = [];
        List<(string Method, string Path, int Status, string From)> asFor = [];
        SortedSet<string> lines = new(StringComparer.Ordinal);
        foreach ((string prose, List<string[]> rows) in sections)
        {
            bool signedIn = prose.Contains(SignedIn, StringComparison.Ordinal);
            foreach (string[] row in rows)
            {
                string answered = row[^1];
                string? read = row.Length == 3 ? Backticked(row[1]).FirstOrDefault(token => token.StartsWith('{')) : null;
                foreach (Match route in Backticked(row[0]).Select(token => RouteToken().Match(token)).Where(match => match.Success))
                {
                    string method = route.Groups["method"].Value;
                    string path = route.Groups["path"].Value;
                    string name = $"{method} {prefix}{path}";
                    lines.Add(name);
                    if (read is not null)
                    {
                        lines.UnionWith(BodyMembers.OfSketch(read).Select(member => $"{name} request {member}"));
                        foreach (int status in JsonRefusals)
                        {
                            answers[(method, path, status)] = refusal;
                        }
                    }

                    if (signedIn && !answered.Contains(NoneSignedIn, StringComparison.Ordinal))
                    {
                        answers[(method, path, 401)] = refusal;
                    }

                    foreach ((int status, string segment) in Statuses(answered))
                    {
                        Match from = AsFor().Match(segment);
                        if (from.Success)
                        {
                            asFor.Add((method, path, status, from.Groups["method"].Value));
                            continue;
                        }

                        string[] tokens = [.. Backticked(segment)];
                        string? body = tokens.Any(token => token.StartsWith("messaging.", StringComparison.Ordinal)) ? refusal
                            : tokens.FirstOrDefault() is { } first && first.StartsWith('{') ? first
                            : null;
                        answers[(method, path, status)] = body ?? answers.GetValueOrDefault((method, path, status));
                    }
                }
            }
        }

        foreach ((string method, string path, int status, string from) in asFor)
        {
            answers[(method, path, status)] = answers[(from, path, status)];
        }

        foreach (((string method, string path, int status), string? body) in answers)
        {
            string name = $"{method} {prefix}{path} {status.ToString(CultureInfo.InvariantCulture)}";
            lines.Add(name);
            if (body is not null)
            {
                lines.UnionWith(BodyMembers.OfSketch(body).Select(member => $"{name} body {member}"));
            }
        }

        return lines;
    }

    /// <summary>Each line <paramref name="given"/> holds that <paramref name="mapped"/> does not, and each the other way.</summary>
    internal static IReadOnlyList<string> Violations(IReadOnlySet<string> given, IReadOnlySet<string> mapped)
    {
        ArgumentNullException.ThrowIfNull(given);
        ArgumentNullException.ThrowIfNull(mapped);
        return
        [
            .. given.Where(line => !mapped.Contains(line)).Select(line => $"05 gives {line}, which the mapped endpoints do not."),
            .. mapped.Where(line => !given.Contains(line)).Select(line => $"The mapped endpoints give {line}, which 05 does not."),
        ];
    }

    /// <summary>Each section's text outside its tables, joined, and the cells of each row of its tables of routes.</summary>
    private static List<(string Prose, List<string[]> Rows)> Sections(string text)
    {
        List<(string Prose, List<string[]> Rows)> sections = [];
        List<string> prose = [];
        List<string[]> rows = [];
        bool inTable = false;
        foreach (string line in text.Split('\n').Select(line => line.TrimEnd('\r')))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                sections.Add((string.Join(' ', prose), rows));
                prose = [];
                rows = [];
            }

            if (!line.StartsWith('|'))
            {
                inTable = false;
                prose.Add(line);
            }
            else if (line.StartsWith(Header, StringComparison.Ordinal))
            {
                inTable = true;
            }
            else if (inTable && !line.StartsWith("|---", StringComparison.Ordinal))
            {
                rows.Add([.. Cell().Split(line.Trim().Trim('|')).Select(cell => cell.Replace("\\|", "|", StringComparison.Ordinal).Trim())]);
            }
        }

        sections.Add((string.Join(' ', prose), rows));
        return sections;
    }

    /// <summary>Each bold status of <paramref name="answers"/>, with its text up to the next one.</summary>
    private static IEnumerable<(int Status, string Segment)> Statuses(string answers)
    {
        Match[] statuses = [.. Status().Matches(answers).AsEnumerable()];
        for (int index = 0; index < statuses.Length; index++)
        {
            int start = statuses[index].Index + statuses[index].Length;
            int end = index + 1 < statuses.Length ? statuses[index + 1].Index : answers.Length;
            yield return (int.Parse(statuses[index].Groups["status"].Value, CultureInfo.InvariantCulture), answers[start..end]);
        }
    }

    private static IEnumerable<string> Backticked(string text)
    {
        return BacktickedToken().Matches(text).Select(match => match.Groups["token"].Value);
    }

    [GeneratedRegex(@"(?<!\\)\|", RegexOptions.CultureInvariant)]
    private static partial Regex Cell();

    [GeneratedRegex(@"`(?<token>[^`]+)`", RegexOptions.CultureInvariant)]
    private static partial Regex BacktickedToken();

    [GeneratedRegex(@"^(?<method>GET|POST|PUT|DELETE|PATCH) (?<path>/\S*)$", RegexOptions.CultureInvariant)]
    private static partial Regex RouteToken();

    [GeneratedRegex(@"\*\*(?<status>\d{3})\*\*", RegexOptions.CultureInvariant)]
    private static partial Regex Status();

    [GeneratedRegex(@"^\s*as for `(?<method>[A-Z]+)`", RegexOptions.CultureInvariant)]
    private static partial Regex AsFor();
}