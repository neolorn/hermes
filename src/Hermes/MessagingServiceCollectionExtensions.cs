using System;

using Hermes.Core;
using Hermes.Operations;
using Hermes.Sending;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hermes;

/// <summary>Registers the library in a host's services.</summary>
public static class MessagingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the engine, binding the configuration section <c>Hermes</c>, with the kinds and
    /// categories <paramref name="declare"/> declares. Every startup check runs when the host
    /// starts, before any hosted service's <c>StartAsync</c>, and a failure stops the start with
    /// a <see cref="MessagingStartupException"/> carrying its code. A host replaces a seam by
    /// registering its own before or after the call.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <param name="configuration">The host's configuration, whose section <c>Hermes</c> is the library's.</param>
    /// <param name="declare">Declares the kinds and categories the host sends.</param>
    /// <returns><paramref name="services"/>.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static IServiceCollection AddHermes(this IServiceCollection services, IConfiguration configuration, Action<DeclarationBuilder> declare)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(declare);

        DeclarationBuilder builder = new();
        declare(builder);
        Declarations declarations = builder.Declarations();

        services.AddOptions();
        services.AddSingleton(declarations);
        services.AddSingleton(_ => new ConfigurationState(configuration.GetSection("Hermes"), declarations.NeedsPublicBaseAddress, TimeZoneInfo.GetSystemTimeZones));
        services.AddSingleton<IConfigureOptions<MessagingOptions>, BindMessagingOptions>();
        services.AddSingleton<IOptionsChangeTokenSource<MessagingOptions>>(provider => provider.GetRequiredService<ConfigurationState>());
        services.AddSingleton<IValidateOptions<MessagingOptions>, MessagingOptionsValidator>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IHostedService, MessagingStartup>();
        return services;
    }
}