using System;
using System.Net.Http;

using Hermes.Core;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Hermes.Sms.Cequens;

/// <summary>Registers the Cequens SMS provider in a host's services (<c>02</c> MSG-SMS-007).</summary>
public static class CequensSmsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Cequens SMS provider as the <see cref="ISmsProvider"/>, replacing any
    /// registered before it, with the named HTTP client <c>Hermes.Sms.Cequens</c> it calls
    /// the gateway through and a check of its credential when the host starts. The credential
    /// is the account's access token, the secret <c>smsCequens</c> the host's
    /// <see cref="ISecretSource"/> answers, read at each call at its highest version. A host
    /// whose secret source does not answer it, or whose highest version is outside its form,
    /// stops at its start with a <see cref="MessagingStartupException"/> carrying the code
    /// <c>messaging.startup.secretunavailable</c> or <c>messaging.startup.secretinvalid</c>
    /// and the key <c>smsCequens</c>.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <returns><paramref name="services"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddHermesCequensSms(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        CequensClient.Register(services);
        services.RemoveAll<ISmsProvider>();
        services.AddTransient<ISmsProvider>(provider => new CequensSmsProvider(
            provider.GetRequiredService<IHttpClientFactory>(),
            provider.GetRequiredService<ISecretSource>()));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, CequensStartup>());
        return services;
    }
}