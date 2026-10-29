using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Hermes.Tests;

/// <summary>
/// What <c>05</c>'s section of the development inbox (<c>MapHermesDevelopmentInbox</c>,
/// <c>05</c> MSG-API-007) gives its group, as lines in the form of
/// <see cref="MappedEndpoints.Lines"/>, which <see cref="ApiContract"/> joins with the other
/// sections'. Its table of routes gives each route in backticks, each status in bold and a
/// status's body as the backticked <c>{ ... }</c> or <c>&lt;shape&gt;</c> after it, a status
/// naming a code answering the refusal body of MSG-API-001; its prose adds a status to every
/// route and one to each route whose answer takes a query; its table of shapes gives each
/// shape's members, a member named as a shape of its own followed into that shape.
/// </summary>
internal static partial class InboxSection
{
    /// <summary>The text that names the section in its heading.</summary>
    internal const string Heading = "(`MapHermesDevelopmentInbox`)";

    private const string RouteHeader = "| Method and route |";

    private const string ShapeHeader = "| Shape |";

    /// <summary>The inbox section of <paramref name="text"/>, a chapter in the form of <c>05</c>, as lines, its routes under <paramref name="prefix"/>.</summary>
    internal static SortedSet<string> Read(string text, string prefix)
    {
        IReadOnlyList<string> refusal = BodyMembers.OfSketch(Backticked(text).First(token => token.StartsWith("{ \"code\"", StringComparison.Ordinal)));
        (string prose, List<string[]> routes, Dictionary<string, string> shapes) = Section(text);
        Func<string, IReadOnlyList<string>?> shape = name => shapes.ContainsKey(name) ? Shape(name, shapes) : null;
        Match every = EveryRoute().Match(prose);
        Match query = QueryRule().Match(prose);

        SortedSet<string> lines = new(StringComparer.Ordinal);
        foreach (string[] row in routes)
        {
            string answered = row[^1];
            foreach (Match route in Backticked(row[0]).Select(token => RouteToken().Match(token)).Where(match => match.Success))
            {
                string name = $"{route.Groups["method"].Value} {prefix}{route.Groups["path"].Value}";
                lines.Add(name);
                Dictionary<int, IReadOnlyList<string>?> answers = [];
                foreach ((int status, string segment) in Statuses(answered))
                {
                    string[] tokens = [.. Backticked(segment)];
                    answers[status] = tokens.Any(token => token.StartsWith("messaging.", StringComparison.Ordinal)) ? refusal
                        : tokens.FirstOrDefault() is { } first && first.StartsWith('{') ? BodyMembers.OfSketch(first, shape)
                        : tokens.FirstOrDefault() is { } named && shape(named) is { } members ? members
                        : null;
                }

                if (every.Success)
                {
                    answers[Status(every)] = refusal;
                }

                if (query.Success && answered.Contains("query", StringComparison.Ordinal))
                {
                    answers[Status(query)] = refusal;
                }

                foreach ((int status, IReadOnlyList<string>? members) in answers)
                {
                    string answer = $"{name} {status.ToString(CultureInfo.InvariantCulture)}";
                    lines.Add(answer);
                    lines.UnionWith((members ?? []).Select(member => $"{answer} body {member}"));
                }
            }
        }

        return lines;
    }

    /// <summary>The section's text outside its tables, the cells of its table of routes, and its shapes' cells by name.</summary>
    private static (string Prose, List<string[]> Routes, Dictionary<string, string> Shapes) Section(string text)
    {
        List<string> prose = [];
        List<string[]> routes = [];
        Dictionary<string, string> shapes = new(StringComparer.Ordinal);
        bool inSection = false;
        bool inRoutes = false;
        bool inShapes = false;
        foreach (string line in text.Split('\n').Select(line => line.TrimEnd('\r')))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                inSection = line.Contains(Heading, StringComparison.Ordinal);
                continue;
            }

            if (!inSection)
            {
                continue;
            }

            if (!line.StartsWith('|'))
            {
                inRoutes = false;
                inShapes = false;
                prose.Add(line);
            }
            else if (line.StartsWith(RouteHeader, StringComparison.Ordinal))
            {
                inRoutes = true;
            }
            else if (line.StartsWith(ShapeHeader, StringComparison.Ordinal))
            {
                inShapes = true;
            }
            else if ((inRoutes || inShapes) && !line.StartsWith("|---", StringComparison.Ordinal))
            {
                string[] cells = [.. Cell().Split(line.Trim().Trim('|')).Select(cell => cell.Replace("\\|", "|", StringComparison.Ordinal).Trim())];
                if (inRoutes)
                {
                    routes.Add(cells);
                }
                else
                {
                    shapes[Backticked(cells[0]).Single()] = cells[1];
                }
            }
        }

        return (string.Join(' ', prose), routes, shapes);
    }

    /// <summary>
    /// The members of the shape <paramref name="name"/>: its sketch's, or another shape's and the
    /// backticked names after them; a member written "<c>`x` as `y`</c>" has <c>y</c>'s members,
    /// and a member named as a shape of its own has that shape's.
    /// </summary>
    private static List<string> Shape(string name, Dictionary<string, string> shapes)
    {
        string cell = shapes[name];
        string[] tokens = [.. Backticked(cell)];
        List<string> members = [];
        if (tokens.FirstOrDefault() is { } first && first.StartsWith('{'))
        {
            members.AddRange(BodyMembers.OfSketch(first));
        }
        else if (Based().Match(cell) is { Success: true } based)
        {
            members.AddRange(Shape($"<{based.Groups["shape"].Value}>", shapes));
            members.AddRange(tokens.Where(token => token.Length > 2 && token[0] == '"' && token[^1] == '"').Select(token => token.Trim('"')));
        }

        foreach (Match same in SameAs().Matches(cell))
        {
            string from = same.Groups["as"].Value + ".";
            members.AddRange([.. members.Where(member => member.StartsWith(from, StringComparison.Ordinal)).Select(member => same.Groups["member"].Value + "." + member[from.Length..])]);
        }

        foreach (string member in members.Where(member => !member.Contains('.', StringComparison.Ordinal) && shapes.ContainsKey(member)).ToList())
        {
            members.AddRange(Shape(member, shapes).Select(inner => member + "." + inner));
        }

        return members;
    }

    private static IEnumerable<(int Status, string Segment)> Statuses(string answers)
    {
        Match[] statuses = [.. StatusToken().Matches(answers).AsEnumerable()];
        for (int index = 0; index < statuses.Length; index++)
        {
            int start = statuses[index].Index + statuses[index].Length;
            int end = index + 1 < statuses.Length ? statuses[index + 1].Index : answers.Length;
            yield return (Status(statuses[index]), answers[start..end]);
        }
    }

    private static int Status(Match match)
    {
        return int.Parse(match.Groups["status"].Value, CultureInfo.InvariantCulture);
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
    private static partial Regex StatusToken();

    [GeneratedRegex(@"Every route also answers \*\*(?<status>\d{3})\*\*", RegexOptions.CultureInvariant)]
    private static partial Regex EveryRoute();

    [GeneratedRegex(@"a query parameter outside its rule \*\*(?<status>\d{3})\*\*", RegexOptions.CultureInvariant)]
    private static partial Regex QueryRule();

    [GeneratedRegex(@"^The (?<shape>\w+)'s members", RegexOptions.CultureInvariant)]
    private static partial Regex Based();

    [GeneratedRegex(@"`(?<member>\w+)` as `(?<as>\w+)`", RegexOptions.CultureInvariant)]
    private static partial Regex SameAs();
}