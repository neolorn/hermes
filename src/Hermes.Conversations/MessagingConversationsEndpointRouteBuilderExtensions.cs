using System;

using Hermes.Conversations.Endpoints;
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
    /// bodies as <c>application/json</c> alone. The host adds to the group the authorization
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
        return group;
    }
}