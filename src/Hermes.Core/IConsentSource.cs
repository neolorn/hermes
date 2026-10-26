using System.Threading;
using System.Threading.Tasks;

namespace Hermes.Core;

/// <summary>Answers whether a recipient consents to a category's purpose (<c>01</c> MSG-SEND-011).</summary>
public interface IConsentSource
{
    /// <summary>Answers whether the recipient <paramref name="query"/> names consents to its purpose on its channel.</summary>
    /// <param name="query">The recipient, destination, purpose and channel.</param>
    /// <param name="cancellationToken">Cancels the answer.</param>
    /// <returns>The answer.</returns>
    ValueTask<ConsentAnswer> ConsentsAsync(ConsentQuery query, CancellationToken cancellationToken);
}