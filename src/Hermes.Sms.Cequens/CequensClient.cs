using System;
using System.Net.Http;

using Microsoft.Extensions.DependencyInjection;

namespace Hermes.Sms.Cequens;

/// <summary>
/// The named client <c>Hermes.Sms.Cequens</c> the provider calls the gateway through
/// (<c>02</c> MSG-SMS-007), built as <c>Hermes.Push</c> is (MSG-CHAN-001): it follows no
/// redirect and carries no handler a host adds for its own clients, so nothing retries a
/// request within one call, and it writes no request's address to a log.
/// </summary>
internal static class CequensClient
{
    /// <summary>The client's name.</summary>
    internal const string Name = "Hermes.Sms.Cequens";

    /// <summary>The send's address.</summary>
    internal static readonly Uri Messages = new("https://apis.cequens.com/sms/v1/messages", UriKind.Absolute);

    /// <summary>The balance's address.</summary>
    internal static readonly Uri Balance = new("https://apis.cequens.com/sms/v1/account/balance", UriKind.Absolute);

    /// <summary>Registers the client in <paramref name="services"/>.</summary>
    internal static void Register(IServiceCollection services)
    {
        services.AddHttpClient(Name)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false })
            .ConfigureAdditionalHttpMessageHandlers((handlers, _) =>
            {
                // This runs after a host's ConfigureHttpClientDefaults, so it clears what they add.
                handlers.Clear();
            })
            .RemoveAllLoggers();
    }
}