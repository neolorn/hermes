using System;

using Hermes.Conversations.Endpoints;
using Hermes.Conversations.Live;
using Hermes.Core;
using Hermes.Endpoints;
using Hermes.Operations;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Hermes.Conversations;

/// <summary>Maps the conversations package's endpoint groups, each by its own call under the prefix the host chooses.</summary>
public static class MessagingConversationsEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the staff conversations group: the direct and group conversations of the person the
    /// registered <c>IRecipientIdentity</c> answers, under <c>/conversations</c>, reading JSON
    /// bodies as <c>application/json</c> alone, and the staff hub at <c>/live</c>, which pushes
    /// what the routes wrote to the persons connected who may read it. The host adds to the group the authorization
    /// that decides who may use it. A group is mapped before the host starts, since the startup
    /// checks read which groups are mapped.
    /// </summary>
    /// <param name="endpoints">The host's endpoints.</param>
    /// <param name="prefix">The prefix the group's routes are under.</param>
    /// <returns>The group's route group.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="InvalidOperationException"><c>AddHermes</c> or <c>AddHermesConversations</c> is not registered, or the host has started.</exception>
    public static RouteGroupBuilder MapHermesStaffConversations(this IEndpointRouteBuilder endpoints, string prefix)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(prefix);
        EndpointGroups groups = endpoints.ServiceProvider.GetService<EndpointGroups>()
            ?? throw new InvalidOperationException("AddHermes is registered before an endpoint group of the library is mapped.");
        if (endpoints.ServiceProvider.GetService<IServiceProviderIsService>() is not { } services || !services.IsService(typeof(IConversations)))
        {
            throw new InvalidOperationException("AddHermesConversations is registered before the staff conversations group is mapped.");
        }

        if (endpoints.ServiceProvider.GetRequiredService<MessagingReady>().IsReady)
        {
            throw new InvalidOperationException("The library's endpoint groups are mapped before the host starts, since its startup checks read them.");
        }

        groups.StaffConversations = true;
        RouteGroupBuilder group = endpoints.MapGroup(prefix);
        StaffEndpoints.Map(group);
        group.MapHub<StaffHub>("/live");
        return group;
    }

    /// <summary>
    /// Maps the public conversations group: support's topics, the privacy notice, starting a
    /// support conversation or leaving one through the offline form, and reading and writing
    /// it, under <c>/support</c>, reading JSON bodies as <c>application/json</c> alone. No route
    /// needs a signed-in person: the group allows anonymous callers, a signed-in customer being
    /// the person a registered <c>IRecipientIdentity</c> answers, and a visitor known by the
    /// cookie <c>messaging_visitor</c>, kept under the prefix. A group is mapped before the
    /// host starts, since the startup checks read which groups are mapped.
    /// </summary>
    /// <param name="endpoints">The host's endpoints.</param>
    /// <param name="prefix">The prefix the group's routes are under.</param>
    /// <returns>The group's route group.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="InvalidOperationException"><c>AddHermes</c> or <c>AddHermesConversations</c> is not registered, or the host has started.</exception>
    public static RouteGroupBuilder MapHermesPublicConversations(this IEndpointRouteBuilder endpoints, string prefix)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(prefix);
        if (endpoints.ServiceProvider.GetService<EndpointGroups>() is null)
        {
            throw new InvalidOperationException("AddHermes is registered before an endpoint group of the library is mapped.");
        }

        if (endpoints.ServiceProvider.GetService<IServiceProviderIsService>() is not { } services || !services.IsService(typeof(IConversations)))
        {
            throw new InvalidOperationException("AddHermesConversations is registered before the public conversations group is mapped.");
        }

        if (endpoints.ServiceProvider.GetRequiredService<MessagingReady>().IsReady)
        {
            throw new InvalidOperationException("The library's endpoint groups are mapped before the host starts, since its startup checks read them.");
        }

        RouteGroupBuilder group = endpoints.MapGroup(prefix);
        group.AllowAnonymous();
        PublicEndpoints.Map(group, prefix.TrimEnd('/'));
        return group;
    }
}