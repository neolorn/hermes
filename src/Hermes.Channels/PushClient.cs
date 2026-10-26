using System.Net.Http;

using Microsoft.Extensions.DependencyInjection;

namespace Hermes.Channels;

/// <summary>
/// The named client <c>Hermes.Push</c> the shipped push provider makes its requests through
/// (<c>02</c> MSG-CHAN-001): it follows no redirect, times out after
/// <c>Dispatch:Timeout</c>, and carries the library's handler alone, so no handler a host
/// adds for its own clients retries a request within one call; and it writes no request's
/// address to a log.
/// </summary>
internal static class PushClient
{
    /// <summary>The client's name.</summary>
    internal const string Name = "Hermes.Push";

    /// <summary>Registers the client in <paramref name="services"/>.</summary>
    internal static void Register(IServiceCollection services)
    {
        services.AddHttpClient(Name)
            .ConfigureHttpClient((provider, client) => client.Timeout = provider.GetRequiredService<CallTimeout>().Current)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false })
            .ConfigureAdditionalHttpMessageHandlers((handlers, _) =>
            {
                // This runs after a host's ConfigureHttpClientDefaults, so it clears what they add.
                handlers.Clear();
                handlers.Add(new EmptyErrorContent());
            })
            .RemoveAllLoggers();
    }
}