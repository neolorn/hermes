using System.Threading;
using System.Threading.Tasks;

namespace Hermes.Core;

/// <summary>
/// A push channel's provider for one platform (<c>02</c> MSG-PUSH-005, MSG-PUSH-008): a request
/// goes to the provider of its subscription's platform. A host replaces the shipped Web Push
/// provider by registering its own.
/// </summary>
public interface IPushProvider
{
    /// <summary>The platform this provider serves.</summary>
    PushPlatform Platform { get; }

    /// <summary>Makes one request to one subscription.</summary>
    /// <param name="request">The request, a case of this provider's platform.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The push service's answer, classed as <c>02</c> MSG-PUSH-005 gives; never text the service answered.</returns>
    ValueTask<Attempt> SendAsync(PushRequest request, CancellationToken cancellationToken);
}