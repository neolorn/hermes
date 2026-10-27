using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

namespace Hermes.Operations;

/// <summary>
/// The schema check of a host's transaction (<c>01</c> MSG-SEND-004): its connection's database
/// holds every migration of the release, checked once per connection string.
/// </summary>
internal sealed class HostSchemas
{
    private readonly ConcurrentDictionary<string, bool> checkedSchemas = new(StringComparer.Ordinal);

    /// <summary>Throws <see cref="InvalidOperationException"/> where the database of <paramref name="context"/>'s connection lacks a migration of the release.</summary>
    internal async Task CheckAsync(MessagingContext context, string connection, CancellationToken cancellationToken)
    {
        if (checkedSchemas.ContainsKey(connection))
        {
            return;
        }

        HashSet<string> applied = [.. await context.Database.GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false)];
        if (SchemaMigration.Ids.FirstOrDefault(migration => !applied.Contains(migration)) is { } missing)
        {
            throw new InvalidOperationException($"The host's transaction is on a database that lacks the migration {missing}.");
        }

        checkedSchemas[connection] = true;
    }
}