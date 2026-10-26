using System.Threading;
using System.Threading.Tasks;

namespace Hermes.Core;

/// <summary>Is told of each status a delivery reaches (<c>01</c> MSG-SEND-017).</summary>
public interface IDeliveryObserver
{
    /// <summary>Takes a notice: it is taken when the call returns, and not taken when it throws or runs past <c>Hermes:Dispatch:Timeout</c>.</summary>
    /// <param name="notice">The notice.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the notice is taken.</returns>
    ValueTask ObserveAsync(DeliveryNotice notice, CancellationToken cancellationToken);
}