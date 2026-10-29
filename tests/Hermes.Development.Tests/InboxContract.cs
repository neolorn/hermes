using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hermes.Development.Tests;

/// <summary>
/// What <c>05</c> section 6 gives the inbox's group (<c>05</c> MSG-API-007; <c>07</c>
/// MSG-ENG-006), and what the group's endpoint metadata declares, both as lines: each route,
/// each of its statuses and each member of each JSON body it answers. The section's table of
/// routes gives each route in backticks, each status in bold and a status's body as the
/// backticked <c>{ ... }</c> or <c>&lt;shape&gt;</c> after it, a status naming a code answering
/// the refusal body of MSG-API-001; its prose adds a status to every route and 400 to each route
/// whose answer takes a query; its table of shapes gives each shape's members, a member named
/// as a shape of its own followed into that shape.
/// </summary>
internal static partial class InboxContract
{
    internal const string Prefix = "/dev/inbox";

    private const string RouteHeader = "| Method and route |";

    private const string ShapeHeader = "| Shape |";

    /// <summary>The text of the repository's <c>05</c>.</summary>
    internal static string Text()
    {
        for (DirectoryInfo? folder = new(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "Hermes.sln")))
            {
                return File.ReadAllText(Path.Combine(folder.FullName, "docs", "spec", "05-api-contract.md"));
            }
        }

        throw new InvalidOperationException("No folder above the test run holds Hermes.sln.");
    }

    /// <summary><paramref name="text"/>, a chapter in the form of <c>05</c>, as lines of its inbox section, its routes under <paramref name="prefix"/>.</summary>
    internal static SortedSet<string> Read(string text, string prefix)
    {
        string[] refusal = [.. OfSketch(Backticked(text).First(token => token.StartsWith("{ \"code\"", StringComparison.Ordinal)), _ => null)];
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
                        : tokens.FirstOrDefault() is { } first && first.StartsWith('{') ? OfSketch(first, shape)
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

    /// <summary>The inbox's group as a host in Development maps it under <see cref="Prefix"/>, as lines in the form of <see cref="Read"/>.</summary>
    internal static SortedSet<string> Mapped()
    {
        SortedSet<string> lines = new(StringComparer.Ordinal);
        foreach ((string route, IReadOnlyList<IProducesResponseTypeMetadata> responses) in Endpoints())
        {
            lines.Add(route);
            foreach (IProducesResponseTypeMetadata response in responses)
            {
                lines.Add($"{route} {response.StatusCode}");
                if (response.Type is { } type && type != typeof(void) && response.ContentTypes.Contains("application/json", StringComparer.Ordinal))
                {
                    lines.UnionWith(Members(type).Select(member => $"{route} {response.StatusCode} body {member}"));
                }
            }
        }

        return lines;
    }

    /// <summary>The bodies the inbox's serializer context is declared for that no route declares it answers.</summary>
    internal static IReadOnlyList<Type> Unmapped()
    {
        HashSet<Type> used = [.. Endpoints().SelectMany(route => route.Responses).Select(response => response.Type).OfType<Type>()];
        return [.. typeof(InboxJson).GetCustomAttributesData()
            .Where(attribute => attribute.AttributeType == typeof(JsonSerializableAttribute))
            .Select(attribute => (Type)attribute.ConstructorArguments[0].Value!)
            .Where(type => !used.Contains(type))];
    }

    /// <summary>Each line <paramref name="given"/> holds that <paramref name="mapped"/> does not, and each the other way.</summary>
    internal static IReadOnlyList<string> Violations(IReadOnlySet<string> given, IReadOnlySet<string> mapped)
    {
        return
        [
            .. given.Where(line => !mapped.Contains(line)).Select(line => $"05 gives {line}, which the mapped endpoints do not."),
            .. mapped.Where(line => !given.Contains(line)).Select(line => $"The mapped endpoints give {line}, which 05 does not."),
        ];
    }

    /// <summary>The members of <paramref name="type"/> as the inbox's serializer context writes them.</summary>
    internal static IReadOnlyList<string> Members(Type type)
    {
        List<string> members = [];
        Add(type, string.Empty, members);
        return members;
    }

    /// <summary>The members of the body <paramref name="sketch"/> writes, a <c>&lt;shape&gt;</c> in it read through <paramref name="shape"/>.</summary>
    internal static IReadOnlyList<string> OfSketch(string sketch, Func<string, IReadOnlyList<string>?> shape)
    {
        string[] tokens = [.. Token().Matches(sketch).Select(match => match.Value)];
        int next = 0;
        List<string> members = [];
        ReadObject(tokens, ref next, string.Empty, members, shape);
        return members;
    }

    private static List<(string Route, IReadOnlyList<IProducesResponseTypeMetadata> Responses)> Endpoints()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Hermes:DevelopmentInbox:Folder"] = Path.Combine(Path.GetTempPath(), "hermes-development-tests", "contract") });
        builder.Logging.ClearProviders();
        builder.AddHermesDevelopmentInbox();
        using WebApplication app = builder.Build();
        app.MapHermesDevelopmentInbox(Prefix);

        List<(string, IReadOnlyList<IProducesResponseTypeMetadata>)> routes = [];
        foreach (RouteEndpoint endpoint in ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>())
        {
            IProducesResponseTypeMetadata[] responses = [.. endpoint.Metadata.OfType<IProducesResponseTypeMetadata>()];
            foreach (string method in endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [])
            {
                routes.Add(($"{method} {endpoint.RoutePattern.RawText}", responses));
            }
        }

        return routes;
    }

    private static void Add(Type type, string prefix, List<string> members)
    {
        JsonTypeInfo info = InboxJson.Default.GetTypeInfo(type) ?? throw new InvalidOperationException($"The inbox's serializer context holds no {type.Name}.");
        foreach (JsonPropertyInfo property in info.Kind == JsonTypeInfoKind.Object ? info.Properties : [])
        {
            string path = prefix + property.Name;
            members.Add(path);
            foreach (Type inner in Inner(property.PropertyType).Where(inner => inner.Assembly == typeof(InboxJson).Assembly))
            {
                Add(inner, path + ".", members);
            }
        }
    }

    private static IEnumerable<Type> Inner(Type type)
    {
        Type plain = Nullable.GetUnderlyingType(type) ?? type;
        return plain.IsGenericType ? plain.GetGenericArguments().SelectMany(Inner) : [plain];
    }

    /// <summary>The inbox section's text outside its tables, the cells of its table of routes, and its shapes' cells by name.</summary>
    private static (string Prose, List<string[]> Routes, Dictionary<string, string> Shapes) Section(string text)
    {
        List<string> prose = [];
        List<string[]> routes = [];
        Dictionary<string, string> shapes = new(StringComparer.Ordinal);
        bool inSection = false;
        List<string[]>? table = null;
        foreach (string line in text.Split('\n').Select(line => line.TrimEnd('\r')))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                inSection = line.Contains("(`MapHermesDevelopmentInbox`)", StringComparison.Ordinal);
                continue;
            }

            if (!inSection)
            {
                continue;
            }

            if (!line.StartsWith('|'))
            {
                table = null;
                prose.Add(line);
            }
            else if (line.StartsWith(RouteHeader, StringComparison.Ordinal))
            {
                table = routes;
            }
            else if (line.StartsWith(ShapeHeader, StringComparison.Ordinal))
            {
                table = [];
            }
            else if (table is not null && !line.StartsWith("|---", StringComparison.Ordinal))
            {
                string[] cells = [.. Cell().Split(line.Trim().Trim('|')).Select(cell => cell.Replace("\\|", "|", StringComparison.Ordinal).Trim())];
                if (ReferenceEquals(table, routes))
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
            members.AddRange(OfSketch(first, _ => null));
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

    private static void ReadObject(string[] tokens, ref int next, string prefix, List<string> members, Func<string, IReadOnlyList<string>?> shape)
    {
        Expect(tokens, ref next, "{");
        while (next < tokens.Length && tokens[next] != "}")
        {
            string token = tokens[next++];
            if (token is "," or "...")
            {
                continue;
            }

            if (!token.StartsWith('"'))
            {
                throw new FormatException($"A member's name was expected, not {token}.");
            }

            string path = prefix + token.Trim('"');
            members.Add(path);
            if (next < tokens.Length && tokens[next] == ":")
            {
                next++;
                ReadValue(tokens, ref next, path, members, shape);
            }
        }

        Expect(tokens, ref next, "}");
    }

    private static void ReadValue(string[] tokens, ref int next, string path, List<string> members, Func<string, IReadOnlyList<string>?> shape)
    {
        if (next < tokens.Length && tokens[next] == "{")
        {
            ReadObject(tokens, ref next, path + ".", members, shape);
            return;
        }

        if (next < tokens.Length && tokens[next] == "[")
        {
            next++;
            ReadValue(tokens, ref next, path, members, shape);
            Expect(tokens, ref next, "]");
            return;
        }

        while (next < tokens.Length && tokens[next] is not ("," or "}" or "]"))
        {
            if (shape(tokens[next]) is { } inner)
            {
                members.AddRange(inner.Select(member => path + "." + member));
            }

            next++;
        }
    }

    private static void Expect(string[] tokens, ref int next, string token)
    {
        if (next >= tokens.Length || tokens[next] != token)
        {
            throw new FormatException($"{token} was expected at token {next}.");
        }

        next++;
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

    [GeneratedRegex(@"""[^""]*""|\.\.\.|[{}\[\],:]|[^\s{}\[\],:""]+", RegexOptions.CultureInvariant)]
    private static partial Regex Token();
}