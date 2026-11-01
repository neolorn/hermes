using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Hermes.Conversations;
using Hermes.Core;
using Hermes.Development;
using Hermes.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hermes.Tests;

/// <summary>
/// Every endpoint group of <c>05</c> mapped under <see cref="Endpoints.Prefix"/> on test hosts
/// that are built and never started, the development inbox's on one whose environment is
/// Development (M-025), read from their endpoint metadata (<c>07</c> MSG-ENG-006, <c>04</c>
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
        [$"POST {Endpoints.Prefix}/inapp/{{id}}/read"] = typeof(EmptyBody),
        [$"POST {Endpoints.Prefix}/inapp/read"] = typeof(EmptyBody),
        [$"POST {Endpoints.StaffPrefix}/conversations/direct"] = BodyMembers.ConversationsBody("ParticipantsRequest"),
        [$"POST {Endpoints.StaffPrefix}/conversations/{{id}}/messages"] = BodyMembers.ConversationsBody("MessageRequest"),
        [$"PUT {Endpoints.StaffPrefix}/conversations/{{id}}/messages/{{messageId}}"] = BodyMembers.ConversationsBody("EditRequest"),
        [$"POST {Endpoints.StaffPrefix}/conversations/{{id}}/read"] = BodyMembers.ConversationsBody("ReadRequest"),
        [$"PUT {Endpoints.StaffPrefix}/conversations/{{id}}/mute"] = BodyMembers.ConversationsBody("MuteRequest"),
        [$"POST {Endpoints.StaffPrefix}/conversations/{{id}}/participants"] = BodyMembers.ConversationsBody("ParticipantsRequest"),
        [$"POST {Endpoints.StaffPrefix}/conversations/{{id}}/leave"] = typeof(EmptyBody),
        [$"PUT {Endpoints.StaffPrefix}/support/availability"] = BodyMembers.ConversationsBody("AvailabilityRequest"),
        [$"POST {Endpoints.StaffPrefix}/support/conversations/{{id}}/transfer"] = BodyMembers.ConversationsBody("TransferRequest"),
        [$"POST {Endpoints.StaffPrefix}/support/conversations/{{id}}/close"] = typeof(EmptyBody),
        [$"POST {Endpoints.StaffPrefix}/support/conversations/{{id}}/block"] = BodyMembers.ConversationsBody("BlockRequest"),
        [$"PUT {Endpoints.StaffPrefix}/support/conversations/{{id}}/tags"] = BodyMembers.ConversationsBody("TagsRequest"),
        [$"POST {Endpoints.StaffPrefix}/support/conversations/{{id}}/snooze"] = BodyMembers.ConversationsBody("SnoozeRequest"),
        [$"POST {Endpoints.StaffPrefix}/support/replies"] = BodyMembers.ConversationsBody("ReplyRequest"),
        [$"PUT {Endpoints.StaffPrefix}/support/replies/{{id}}"] = BodyMembers.ConversationsBody("ReplyChange"),
        [$"POST {Endpoints.PublicPrefix}/support/notice"] = BodyMembers.ConversationsBody("NoticeRequest"),
        [$"POST {Endpoints.PublicPrefix}/support/conversations"] = BodyMembers.ConversationsBody("StartRequest"),
        [$"POST {Endpoints.PublicPrefix}/support/offline"] = BodyMembers.ConversationsBody("OfflineRequest"),
        [$"POST {Endpoints.PublicPrefix}/support/conversations/{{id}}/messages"] = BodyMembers.ConversationsBody("EditRequest"),
        [$"POST {Endpoints.PublicPrefix}/support/conversations/{{id}}/read"] = BodyMembers.ConversationsBody("ReadRequest"),
        [$"POST {Endpoints.PublicPrefix}/support/conversations/{{id}}/close"] = typeof(EmptyBody),
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Each mapped route, with the responses its metadata declares.</summary>
    internal static IReadOnlyList<(string Route, IReadOnlyList<IProducesResponseTypeMetadata> Responses)> Read()
    {
        return Read(out _);
    }

    /// <summary>Each mapped route, with the responses its metadata declares, and in <paramref name="hubs"/> the mapped hubs as lines (<see cref="HubContract"/>).</summary>
    private static IReadOnlyList<(string Route, IReadOnlyList<IProducesResponseTypeMetadata> Responses)> Read(out IReadOnlyList<string> hubs)
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
            after: services => services.AddHermesConversations(),
            map: application =>
            {
                application.MapHermesPreferences(Endpoints.Prefix);
                application.MapHermesUnsubscribe(Endpoints.Prefix);
                application.MapHermesPushSubscriptions(Endpoints.Prefix);
                application.MapHermesSmsReports(Endpoints.Prefix);
                application.MapHermesInApp(Endpoints.Prefix);
                application.MapHermesStaffConversations(Endpoints.StaffPrefix);
                application.MapHermesPublicConversations(Endpoints.PublicPrefix);
            });

        // The development inbox maps its group only where the environment is Development (02 MSG-DEV-001).
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Hermes:DevelopmentInbox:Folder"] = Path.Combine(Path.GetTempPath(), "hermes-tests", "inbox-contract") });
        builder.Logging.ClearProviders();
        builder.AddHermesDevelopmentInbox();
        using WebApplication inbox = builder.Build();
        inbox.MapHermesDevelopmentInbox(Endpoints.Prefix);

        hubs = [.. HubContract.Mapped(((IEndpointRouteBuilder)host).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>())];
        return [.. Routes(host), .. Routes(inbox)];
    }

    private static IEnumerable<(string Route, IReadOnlyList<IProducesResponseTypeMetadata> Responses)> Routes(IHost host)
    {
        foreach (RouteEndpoint endpoint in ((IEndpointRouteBuilder)host).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>())
        {
            IProducesResponseTypeMetadata[] responses = [.. endpoint.Metadata.OfType<IProducesResponseTypeMetadata>()];
            foreach (string method in endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [])
            {
                yield return ($"{method} {endpoint.RoutePattern.RawText}", responses);
            }
        }
    }

    /// <summary>
    /// The mapped endpoints as lines, in the form of <see cref="ApiContract"/>: each route, each
    /// status it declares, each member of each JSON body it answers, and each member of the body
    /// it reads.
    /// </summary>
    internal static SortedSet<string> Lines()
    {
        SortedSet<string> lines = new(StringComparer.Ordinal);
        foreach ((string route, IReadOnlyList<IProducesResponseTypeMetadata> responses) in Read(out IReadOnlyList<string> hubs))
        {
            lines.UnionWith(hubs);
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