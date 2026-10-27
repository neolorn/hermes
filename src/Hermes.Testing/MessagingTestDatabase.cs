using System;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Operations;

namespace Hermes.Testing;

/// <summary>Brings a test database to the release's schema (<c>04</c> MSG-LIB-008).</summary>
public static class MessagingTestDatabase
{
    /// <summary>Applies the release's migrations to the database of <paramref name="connection"/>, as <c>hermes migrate</c> does.</summary>
    /// <param name="connection">The test database's connection string.</param>
    /// <param name="cancellationToken">Stops the migration.</param>
    /// <returns>A task that completes once the schema is the release's.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The migrations could not be applied; the message names the code.</exception>
    public static async ValueTask MigrateAsync(string connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (await SchemaMigration.ApplyAsync(connection, [], cancellationToken).ConfigureAwait(false) is { } failure)
        {
            throw new InvalidOperationException($"The migrations were not applied: {failure.Code}.");
        }
    }
}