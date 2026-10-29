using System;

using Hermes.Channels;
using Hermes.Core;
using Hermes.Sending;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Hermes.Testing;

/// <summary>Registers the library's test doubles in a host's services (<c>04</c> MSG-LIB-008).</summary>
public static class MessagingTestingServiceCollectionExtensions
{
    /// <summary>
    /// Registers each in-memory provider and one <see cref="FixedSecretSource"/> as singletons,
    /// each resolvable by its own type and by its contract, in place of the shipped providers and
    /// the host's secret source, running <paramref name="configure"/> on that source at once, and
    /// removes the hosted dispatcher and the <c>observer</c> job, or keeps <c>AddHermes</c> from
    /// registering them, so only a test's passes dispatch and each delivers the pending notices
    /// after its deliveries. A secret source the host registers after this call replaces
    /// <see cref="FixedSecretSource"/>. The in-memory providers are used in place of the
    /// development inbox's too, whichever of the two calls comes first.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <param name="configure">Sets the source's secrets, as a test sets <c>connection</c> before the host starts.</param>
    /// <returns><paramref name="services"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddHermesTesting(this IServiceCollection services, Action<FixedSecretSource>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        FixedSecretSource secrets = new();
        configure?.Invoke(secrets);
        InMemoryEmailProvider email = new();
        InMemorySmsProvider sms = new();
        InMemoryPushProvider push = new();

        services.AddSingleton(email);
        services.AddSingleton<IEmailProvider>(email);
        services.AddSingleton(sms);
        services.AddSingleton<ISmsProvider>(sms);
        services.AddSingleton(push);
        services.AddSingleton<IPushProvider>(push);
        services.AddSingleton(secrets);
        services.AddSingleton<ISecretSource>(secrets);

        services.TryAddSingleton<TestProviders>();
        services.AddSingleton<TestPasses>();
        for (int index = services.Count - 1; index >= 0; index--)
        {
            if (services[index].ServiceType == typeof(IHostedService)
                && (services[index].ImplementationType == typeof(HostedDispatcher) || services[index].ImplementationType == typeof(ObserverJob)))
            {
                services.RemoveAt(index);
            }
        }

        return services;
    }
}