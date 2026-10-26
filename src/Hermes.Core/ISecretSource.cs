using System.Threading;
using System.Threading.Tasks;

namespace Hermes.Core;

/// <summary>Reads the secrets the library needs from where the host keeps them (<c>06</c> MSG-OPS-008).</summary>
public interface ISecretSource
{
    /// <summary>Reads the secret of <paramref name="name"/>, a name of <c>08</c> section 7.</summary>
    /// <param name="name">The secret's name.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The secret's versions, or none where the host holds no such secret.</returns>
    ValueTask<SecretVersions?> ReadAsync(string name, CancellationToken cancellationToken);
}