using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using Npgsql;

namespace Hermes.Operations;

/// <summary>Applies the release's migrations as <c>hermes migrate</c> does (<c>06</c> MSG-OPS-002).</summary>
internal static class SchemaMigration
{
    internal const string InputInvalid = "messaging.migrate.inputinvalid";

    internal const string ConnectionFailed = "messaging.migrate.connectionfailed";

    internal const string Failed = "messaging.migrate.failed";

    /// <summary>The release's migrations, in the order they apply.</summary>
    internal static IReadOnlyList<string> Ids { get; } = ReadIds();

    /// <summary>Grants the role the transaction names the use of the schema and its tables, the server quoting the name.</summary>
    private const string Grant = """
        DO $$
        BEGIN
            EXECUTE format('GRANT USAGE ON SCHEMA messaging TO %I', current_setting('messaging.grant_role'));
            EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA messaging TO %I', current_setting('messaging.grant_role'));
        END
        $$
        """;

    /// <summary>
    /// Applies the pending migrations one at a time, each in its own transaction, then grants
    /// each of <paramref name="roles"/> the use of the schema and its tables; answers none on
    /// success, or the failure, the migrations before a failing one staying applied.
    /// </summary>
    internal static async Task<MigrationFailure?> ApplyAsync(string connection, IReadOnlyList<string> roles, CancellationToken cancellationToken)
    {
        try
        {
            _ = new NpgsqlConnectionStringBuilder(connection);
        }
        catch (ArgumentException)
        {
            return new MigrationFailure(InputInvalid, new Dictionary<string, string>(StringComparer.Ordinal) { ["member"] = "connection" });
        }

        MessagingContext context = MessagingContext.Create(connection);
        await using (context.ConfigureAwait(false))
        {
            try
            {
                await context.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception failure) when (failure is DbException or SocketException or TimeoutException or InvalidOperationException or ArgumentException)
            {
                return new MigrationFailure(ConnectionFailed, new Dictionary<string, string>(StringComparer.Ordinal));
            }

            IMigrator migrator = context.GetService<IMigrator>();
            foreach (string migration in await context.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    await migrator.MigrateAsync(migration, cancellationToken).ConfigureAwait(false);
                }
                catch (DbException)
                {
                    return new MigrationFailure(Failed, new Dictionary<string, string>(StringComparer.Ordinal) { ["migration"] = migration });
                }
            }

            if (roles.Count > 0)
            {
                await GrantAsync((NpgsqlConnection)context.Database.GetDbConnection(), roles, cancellationToken).ConfigureAwait(false);
            }
        }

        return null;
    }

    private static async Task GrantAsync(NpgsqlConnection connection, IReadOnlyList<string> roles, CancellationToken cancellationToken)
    {
        NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            foreach (string role in roles)
            {
                NpgsqlCommand name = new("SELECT set_config('messaging.grant_role', @role, true)", connection, transaction);
                await using (name.ConfigureAwait(false))
                {
                    name.Parameters.AddWithValue("role", role);
                    await name.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                NpgsqlCommand grant = new(Grant, connection, transaction);
                await using (grant.ConfigureAwait(false))
                {
                    await grant.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static string[] ReadIds()
    {
        using MessagingContext context = MessagingContext.Create("Host=localhost");
        return [.. context.Database.GetMigrations()];
    }
}