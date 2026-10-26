using System.Threading;
using System.Threading.Tasks;

namespace Hermes.Core;

/// <summary>Supplies template parts, asked before the library's own sources (<c>03</c> MSG-TPL-006).</summary>
public interface ITemplateSource
{
    /// <summary>Reads the part <paramref name="key"/> names; none for a key the source does not recognise, so a later key's shape never breaks it.</summary>
    /// <param name="key">The part.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The part's Liquid text, or none.</returns>
    ValueTask<string?> ReadAsync(TemplateKey key, CancellationToken cancellationToken);
}