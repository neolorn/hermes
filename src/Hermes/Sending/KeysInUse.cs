using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Operations;

using Microsoft.EntityFrameworkCore;

namespace Hermes.Sending;

/// <summary>
/// The versions of a key in use, read in the lookup's own transaction and never from a cache
/// (<c>01</c> MSG-SEND-006; <c>06</c> MSG-OPS-008): those values were made under and that are
/// not retired.
/// </summary>
internal static class KeysInUse
{
    /// <summary>The first version of <paramref name="key"/> in use that <paramref name="held"/> leaves out, or none where every one is held.</summary>
    internal static async Task<int?> MissingAsync(MessagingContext context, string key, IReadOnlyCollection<int> held, CancellationToken cancellationToken)
    {
        List<int> inUse = await context.KeyVersions
            .AsNoTracking()
            .Where(row => row.Key == key && row.FirstMadeAt != null && !row.Retired)
            .Select(row => row.Version)
            .OrderBy(version => version)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return inUse.Cast<int?>().FirstOrDefault(version => !held.Contains(version!.Value));
    }
}