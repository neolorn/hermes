using System;
using System.Net.Http;

using Hermes.Core;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hermes.Channels;

/// <summary>Registers the shipped channel providers in a host's services.</summary>
public static class MessagingChannelsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the shipped providers alone, with their own checks, for an application that
    /// only hands messages to a provider. A channel is used where its configuration section
    /// (<c>Hermes:Email</c>, <c>Hermes:Sms</c>, <c>Hermes:Push</c>) is present; its keys are
    /// checked when the host starts, before any hosted service's <c>StartAsync</c>, and a
    /// failure stops the start with a <see cref="MessagingStartupException"/> carrying its
    /// code. A shipped provider called before the checks complete throws
    /// <see cref="InvalidOperationException"/>. A host replaces a shipped provider by
    /// registering its own before or after the call.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <param name="configuration">The host's configuration, whose section <c>Hermes</c> is the library's.</param>
    /// <returns><paramref name="services"/>.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static IServiceCollection AddHermesChannels(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions();
        services.TryAddSingleton(_ => new ChannelsConfiguration(configuration.GetSection("Hermes")));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<ChannelsOptions>, BindChannelsOptions>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IOptionsChangeTokenSource<ChannelsOptions>, ChannelsConfiguration>(provider => provider.GetRequiredService<ChannelsConfiguration>()));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<ChannelsOptions>, ChannelsOptionsValidator>());
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(provider => new CallTimeout(() => provider.GetRequiredService<IOptionsMonitor<ChannelsOptions>>().CurrentValue.Dispatch.Timeout));
        services.TryAddSingleton<EmailSubmission>();
        services.TryAddSingleton<IEmailProvider>(provider => new SmtpProvider(
            provider.GetRequiredService<IOptionsMonitor<ChannelsOptions>>(),
            provider.GetRequiredService<EmailSubmission>(),
            provider.GetRequiredService<CallTimeout>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ChannelsConfiguration>().ThrowIfNotStarted));
        services.TryAddSingleton<PushKeys>();
        PushClient.Register(services);
        services.TryAddSingleton<IPushProvider>(provider =>
        {
            ChannelsConfiguration state = provider.GetRequiredService<ChannelsConfiguration>();
            IOptionsMonitor<ChannelsOptions> options = provider.GetRequiredService<IOptionsMonitor<ChannelsOptions>>();
            return new WebPushProvider(
                provider.GetRequiredService<IHttpClientFactory>(),
                () =>
                {
                    state.ThrowIfNotStarted();
                    return options.CurrentValue.Push.Subject ?? throw new InvalidOperationException("Push is not used: Hermes:Push:Subject is not configured.");
                },
                provider.GetRequiredService<PushKeys>(),
                provider.GetRequiredService<TimeProvider>());
        });
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, ChannelsStartup>());
        return services;
    }
}