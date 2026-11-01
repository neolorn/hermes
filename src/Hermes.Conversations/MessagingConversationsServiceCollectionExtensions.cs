using System;
using System.Linq;

using Hermes.Conversations.Chats;
using Hermes.Core;
using Hermes.Operations;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Hermes.Conversations;

/// <summary>Registers conversations in a host's services.</summary>
public static class MessagingConversationsServiceCollectionExtensions
{
    /// <summary>
    /// Registers conversations (<c>09</c>) beside the engine, which <c>AddHermes</c> registers
    /// first, and <see cref="IConversations"/> as a scoped service, as <c>AddHermes</c> registers
    /// <c>IInApp</c>. Their checks run when the host starts, after the engine's, and stop the start
    /// with a <see cref="MessagingStartupException"/> carrying its code: the membership seam
    /// <see cref="IConversationMembership"/> not registered, or a key under
    /// <c>Hermes:Conversations:</c> the package does not know. Calling it again registers nothing more.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <returns><paramref name="services"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    /// <exception cref="InvalidOperationException"><c>AddHermes</c> is not registered.</exception>
    public static IServiceCollection AddHermesConversations(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (!services.Any(service => service.ServiceType == typeof(ConfigurationState)))
        {
            throw new InvalidOperationException("AddHermes is registered before AddHermesConversations is called.");
        }

        services.TryAddScoped<IConversations, StaffChats>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, ConversationsStartup>());
        return services;
    }
}