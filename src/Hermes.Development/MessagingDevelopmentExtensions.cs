using System;
using System.Linq;

using Hermes.Channels;
using Hermes.Core;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Hermes.Development;

/// <summary>Registers and maps the development inbox (<c>02</c> MSG-DEV-001; <c>05</c> MSG-API-007).</summary>
public static class MessagingDevelopmentExtensions
{
    /// <summary>
    /// In an application whose environment is Development, captures every message instead of
    /// sending it: the inbox's providers take the place of the three channels' providers, the
    /// shipped ones and every one the host registers, before this call or after it, and each
    /// message they are handed is kept in the inbox's folder. Where <c>AddHermesTesting</c> is
    /// registered too, before this call or after it, its in-memory providers are used instead
    /// and the inbox captures nothing. In any other environment the call does nothing. The
    /// inbox's providers are put in place when the host's services are built, through the
    /// default service provider factory, which the call sets with the options a host in
    /// Development has; a host that sets a factory of its own after this call keeps them from
    /// being put in place, and its start then fails with <see cref="InvalidOperationException"/>.
    /// The keys <c>Hermes:DevelopmentInbox:...</c> are checked when the host starts, and a
    /// value outside its rule stops the start with a <see cref="MessagingStartupException"/>.
    /// </summary>
    /// <param name="builder">The host's builder.</param>
    /// <returns><paramref name="builder"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
    public static IHostApplicationBuilder AddHermesDevelopmentInbox(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        IServiceCollection services = builder.Services;
        if (!builder.Environment.IsDevelopment() || services.Any(service => service.ServiceType == typeof(InboxWriter)))
        {
            return builder;
        }

        IConfiguration configuration = builder.Configuration;
        string app = builder.Environment.ApplicationName;
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(_ => InboxSettings.Read(configuration));
        services.AddSingleton(provider => new InboxWriter(provider.GetRequiredService<InboxSettings>(), provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton(provider => new InboxCapture(provider.GetRequiredService<InboxWriter>(), app));
        services.AddSingleton<InboxGroup>();
        services.AddSingleton<InboxEmailProvider>();
        services.AddSingleton<InboxSmsProvider>();
        services.AddSingleton<InboxPushProvider>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, InboxStartup>());
        builder.ConfigureContainer(
            new DefaultServiceProviderFactory(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }),
            built =>
            {
                if (!ProviderPrecedence.HoldTestProviders(built))
                {
                    ProviderPrecedence.PutInPlace(
                        built,
                        provider => provider.GetRequiredService<InboxEmailProvider>(),
                        provider => provider.GetRequiredService<InboxSmsProvider>(),
                        provider => provider.GetRequiredService<InboxPushProvider>());
                }
            });
        return builder;
    }

    /// <summary>
    /// Maps the development inbox's group under <paramref name="prefix"/> in an application
    /// whose environment is Development, with <c>AllowAnonymous()</c>: its page, and its JSON
    /// answers of the messages the inbox holds, answered only to the machine itself under a
    /// loopback host name (<c>02</c> MSG-DEV-005). In any other environment the group holds no
    /// endpoint.
    /// </summary>
    /// <param name="endpoints">The host's endpoints.</param>
    /// <param name="prefix">The group's prefix, as the host chooses it.</param>
    /// <returns>The group.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="InvalidOperationException">The environment is Development and <c>AddHermesDevelopmentInbox</c> is not registered.</exception>
    public static RouteGroupBuilder MapHermesDevelopmentInbox(this IEndpointRouteBuilder endpoints, string prefix)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(prefix);
        RouteGroupBuilder group = endpoints.MapGroup(prefix);
        if (!endpoints.ServiceProvider.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            return group;
        }

        InboxGroup mapped = endpoints.ServiceProvider.GetService<InboxGroup>()
            ?? throw new InvalidOperationException("The development inbox's group is mapped in Development without AddHermesDevelopmentInbox.");
        mapped.Prefix = prefix;
        InboxEndpoints.Map(group);
        return group;
    }
}