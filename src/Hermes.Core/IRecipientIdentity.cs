using System.Threading;
using System.Threading.Tasks;

namespace Hermes.Core;

/// <summary>Answers the person signed in for the current request, read by the host from its own request context (<c>04</c> MSG-LIB-005).</summary>
public interface IRecipientIdentity
{
    /// <summary>Answers the person reference of the person signed in for the current request.</summary>
    /// <param name="cancellationToken">Cancels the answer.</param>
    /// <returns>The person reference, or none where no person is signed in.</returns>
    ValueTask<string?> CurrentAsync(CancellationToken cancellationToken);
}