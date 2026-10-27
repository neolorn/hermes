using System;

using Hermes.Endpoints;
using Hermes.Operations;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Hermes;

/// <summary>
/// Maps the library's endpoint groups, each by its own call under the prefix the host
/// chooses, so a host mounts each where it wants and leaves out the rest. A group is mapped
/// before the host starts, since the startup checks read which groups are mapped.
/// </summary>
public static class MessagingEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the preferences group: <c>GET /preferences</c> and
    /// <c>PUT /preferences/{category}/{channel}</c>, for the person the registered
    /// <c>IRecipientIdentity</c> answers. A host adds its own requirements to the group as for
    /// any of its endpoints.
    /// </summary>
    /// <param name="endpoints">The host's endpoints.</param>
    /// <param name="prefix">The prefix the group's routes are under.</param>
    /// <returns>The group's route group.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="InvalidOperationException"><c>AddHermes</c> is not registered, or the host has started.</exception>
    public static RouteGroupBuilder MapHermesPreferences(this IEndpointRouteBuilder endpoints, string prefix)
    {
        RouteGroupBuilder group = Group(endpoints, prefix, groups => groups.Preferences = true);
        PreferenceEndpoints.Map(group);
        return group;
    }

    private static RouteGroupBuilder Group(IEndpointRouteBuilder endpoints, string prefix, Action<EndpointGroups> record)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(prefix);
        EndpointGroups groups = endpoints.ServiceProvider.GetService<EndpointGroups>()
            ?? throw new InvalidOperationException("AddHermes is registered before an endpoint group of the library is mapped.");
        if (endpoints.ServiceProvider.GetRequiredService<MessagingReady>().IsReady)
        {
            throw new InvalidOperationException("The library's endpoint groups are mapped before the host starts, since its startup checks read them.");
        }

        record(groups);
        return endpoints.MapGroup(prefix);
    }
}