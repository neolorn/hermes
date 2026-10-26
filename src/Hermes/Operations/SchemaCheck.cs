using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

namespace Hermes.Operations;

/// <summary>Checks at start that the database holds every migration of the release (<c>06</c> MSG-OPS-002).</summary>
internal static class SchemaCheck
{
    /// <summary>The first of the release's migrations the database lacks, or none; a later release's migrations are accepted.</summary>
    internal static async Task<string?> MissingAsync(MessagingDatabase database, CancellationToken cancellationToken)
    {
        MessagingContext context = database.Context();
        await using (context.ConfigureAwait(false))
        {
            HashSet<string> applied = [.. await context.Database.GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false)];
            return SchemaMigration.Ids.FirstOrDefault(migration => !applied.Contains(migration));
        }
    }
}