using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

using Hermes.Core;
using Hermes.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hermes.Tests;

/// <summary>
/// Every endpoint group mapped under <see cref="Endpoints.Prefix"/> on a test host that is built
/// and never started, read from its endpoint metadata (<c>07</c> MSG-ENG-006, <c>04</c>
/// MSG-LIB-006): each route as <c>METHOD /prefix/route</c>, with the responses it declares.
/// </summary>
internal static class MappedEndpoints
{
    /// <summary>
    /// The body each route reads, by route: an endpoint reads its body through the serializer
    /// context itself, to bound it and answer its own refusals (<c>05</c> MSG-API-001), so its
    /// metadata declares none.
    /// </summary>
    internal static FrozenDictionary<string, Type> Requests { get; } = new Dictionary<string, Type>
    {
        [$"PUT {Endpoints.Prefix}/preferences/{{category}}/{{channel}}"] = typeof(PreferenceChange),
        [$"POST {Endpoints.Prefix}/push/subscriptions"] = typeof(SubscriptionBody),
        [$"DELETE {Endpoints.Prefix}/push/subscriptions"] = typeof(RemovalBody),
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Each mapped route, with the responses its metadata declares.</summary>
    internal static IReadOnlyList<(string Route, IReadOnlyList<IProducesResponseTypeMetadata> Responses)> Read()
    {
        Dictionary<string, string?> configuration = EngineHost.Configuration();
        UnsubscribeLinkTests.Configure(configuration);
        configuration["Hermes:Push:AllowedHosts:0"] = "push.example.com";
        using IHost host = TestHost.Build(
            configuration,
            UnsubscribeLinkTests.Declare,
            services =>
            {
                Endpoints.Identity(services);
                services.AddSingleton<IPushProvider, RecordingPushProvider>();
                services.AddSingleton<ISecretSource>(Secrets.Held().With("smsReportKey", Secrets.Bytes(16, 5)));
            },
            map: application =>
            {
                application.MapHermesPreferences(Endpoints.Prefix);
                application.MapHermesUnsubscribe(Endpoints.Prefix);
                application.MapHermesPushSubscriptions(Endpoints.Prefix);
                application.MapHermesSmsReports(Endpoints.Prefix);
            });

        List<(string, IReadOnlyList<IProducesResponseTypeMetadata>)> routes = [];
        foreach (RouteEndpoint endpoint in ((IEndpointRouteBuilder)host).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>())
        {
            IProducesResponseTypeMetadata[] responses = [.. endpoint.Metadata.OfType<IProducesResponseTypeMetadata>()];
            foreach (string method in endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [])
            {
                routes.Add(($"{method} {endpoint.RoutePattern.RawText}", responses));
            }
        }

        return routes;
    }

    /// <summary>
    /// The mapped endpoints as lines, in the form of <see cref="ApiContract"/>: each route, each
    /// status it declares, each member of each JSON body it answers, and each member of the body
    /// it reads.
    /// </summary>
    internal static SortedSet<string> Lines()
    {
        SortedSet<string> lines = new(StringComparer.Ordinal);
        foreach ((string route, IReadOnlyList<IProducesResponseTypeMetadata> responses) in Read())
        {
            lines.Add(route);
            foreach (IProducesResponseTypeMetadata response in responses)
            {
                lines.Add($"{route} {response.StatusCode}");
                bool json = response.Type is not null && response.Type != typeof(void) && response.ContentTypes.Contains("application/json", StringComparer.Ordinal);
                if (json)
                {
                    lines.UnionWith(BodyMembers.Of(response.Type!).Select(member => $"{route} {response.StatusCode} body {member}"));
                }
            }

            if (Requests.TryGetValue(route, out Type? request))
            {
                lines.UnionWith(BodyMembers.Of(request).Select(member => $"{route} request {member}"));
            }
        }

        return lines;
    }

    /// <summary>The bodies the serializer context holds that no mapped route reads or declares it answers.</summary>
    internal static IReadOnlyList<Type> Unmapped()
    {
        HashSet<Type> used = [.. Requests.Values, .. Read().SelectMany(route => route.Responses).Select(response => response.Type).OfType<Type>()];
        return [.. BodyMembers.Roots().Where(type => !used.Contains(type))];
    }
}