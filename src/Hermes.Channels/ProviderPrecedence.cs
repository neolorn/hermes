using System;
using System.Linq;

using Hermes.Core;

using Microsoft.Extensions.DependencyInjection;

namespace Hermes.Channels;

/// <summary>
/// Which providers a host's services resolve where the development inbox is registered
/// (<c>02</c> MSG-DEV-001): the in-memory providers where <c>AddHermesTesting</c> is
/// registered, and otherwise the inbox's, in place of the shipped providers and every one the
/// host registers. It is applied when the host's services are built, after every registration.
/// </summary>
internal static class ProviderPrecedence
{
    /// <summary>Whether <paramref name="services"/> hold the in-memory providers of <c>AddHermesTesting</c>.</summary>
    internal static bool HoldTestProviders(IServiceCollection services)
    {
        return services.Any(service => service.ServiceType == typeof(TestProviders));
    }

    /// <summary>
    /// Removes every registration of the three providers' contracts and adds
    /// <paramref name="email"/>, <paramref name="sms"/> and <paramref name="push"/> in their place.
    /// </summary>
    internal static void PutInPlace(
        IServiceCollection services,
        Func<IServiceProvider, IEmailProvider> email,
        Func<IServiceProvider, ISmsProvider> sms,
        Func<IServiceProvider, IPushProvider> push)
    {
        for (int index = services.Count - 1; index >= 0; index--)
        {
            Type type = services[index].ServiceType;
            if (!services[index].IsKeyedService && (type == typeof(IEmailProvider) || type == typeof(ISmsProvider) || type == typeof(IPushProvider)))
            {
                services.RemoveAt(index);
            }
        }

        services.AddSingleton(email);
        services.AddSingleton(sms);
        services.AddSingleton(push);
    }
}