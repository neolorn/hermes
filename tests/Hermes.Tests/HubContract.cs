using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;

namespace Hermes.Tests;

/// <summary>
/// The hubs as lines, in the form of <see cref="ApiContract"/> (<c>05</c> MSG-API-009; <c>07</c>
/// MSG-ENG-006): each hub's route, <c>HUB /prefix/route</c>, and each of its methods with the
/// side that sends it and its arguments, <c>HUB /prefix/route server message
/// conversationId,message</c>. <c>05</c> gives a hub's route in its section's text, "hub at
/// <c>/live</c>", and its methods in a table of hub methods.
/// </summary>
internal static partial class HubContract
{
    private const string Header = "| Hub method |";

    /// <summary>The hubs <paramref name="text"/>, a chapter in the form of <c>05</c>, gives, their routes under their group's prefix (<see cref="Endpoints.PrefixOf"/>).</summary>
    internal static IEnumerable<string> Read(string text, string prefix)
    {
        string? route = null;
        string under = prefix;
        bool inTable = false;
        foreach (string line in text.Split('\n').Select(line => line.TrimEnd('\r')))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                route = null;
                under = Endpoints.PrefixOf(line, prefix);
            }

            if (HubRoute().Match(line) is { Success: true } named)
            {
                route = $"HUB {under}{named.Groups["route"].Value}";
                yield return route;
            }

            if (!line.StartsWith('|'))
            {
                inTable = false;
                continue;
            }

            if (line.StartsWith(Header, StringComparison.Ordinal))
            {
                inTable = true;
                continue;
            }

            if (inTable && route is not null && !line.StartsWith("|---", StringComparison.Ordinal))
            {
                string[] cells = [.. line.Trim().Trim('|').Split('|').Select(cell => cell.Trim())];
                string side = cells[1].Contains("client", StringComparison.OrdinalIgnoreCase) ? "client" : "server";
                // The arguments are the backticked names before the cell's first colon, which begins their description.
                string[] arguments = [.. Backticked().Matches(cells[2].Split(':')[0]).Select(match => match.Groups["token"].Value).Where(token => !token.StartsWith('<'))];
                yield return $"{route} {side} {Backticked().Match(cells[0]).Groups["token"].Value} {string.Join(',', arguments)}";
            }
        }
    }

    /// <summary>The mapped hubs among <paramref name="endpoints"/> as lines: each hub's route, the methods a client calls, public on its type, and those it sends, as the hub's <c>ServerMethods</c> lists them.</summary>
    internal static IEnumerable<string> Mapped(IEnumerable<RouteEndpoint> endpoints)
    {
        foreach (IGrouping<Type, RouteEndpoint> hub in endpoints.Where(endpoint => endpoint.Metadata.GetMetadata<HubMetadata>() is not null).GroupBy(endpoint => endpoint.Metadata.GetMetadata<HubMetadata>()!.HubType))
        {
            string route = $"HUB {hub.Select(endpoint => endpoint.RoutePattern.RawText!).OrderBy(raw => raw.Length).First()}";
            yield return route;
            foreach (MethodInfo method in hub.Key.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(method => method.GetBaseDefinition().DeclaringType == hub.Key && !method.IsSpecialName))
            {
                yield return $"{route} client {method.Name} {string.Join(',', method.GetParameters().Select(parameter => parameter.Name))}";
            }

            PropertyInfo sent = hub.Key.GetProperty("ServerMethods", BindingFlags.NonPublic | BindingFlags.Static) ?? throw new InvalidOperationException($"{hub.Key.Name} lists no ServerMethods.");
            foreach ((string name, string[] arguments) in ((string Name, string[] Arguments)[])sent.GetValue(null)!)
            {
                yield return $"{route} server {name} {string.Join(',', arguments)}";
            }
        }
    }

    [GeneratedRegex(@"hub at `(?<route>/[^`\s]*)`", RegexOptions.CultureInvariant)]
    private static partial Regex HubRoute();

    [GeneratedRegex(@"`(?<token>[^`]+)`", RegexOptions.CultureInvariant)]
    private static partial Regex Backticked();
}