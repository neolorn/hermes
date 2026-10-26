using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Npgsql;

using Xunit;

namespace Hermes.Cli.Tests;

[Trait("Category", "integration")]
public sealed class MigrateTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task MSG_OPS_002_AC2_MigrateTwiceAppliesNothingTheSecondTimeAndExitsZeroBothTimes()
    {
        (_, string connection) = await postgres.NewDatabaseAsync();

        Tool.ToolRun first = await Tool.RunAsync(Tool.Input(connection));
        IReadOnlyList<string> afterFirst = await HistoryAsync(connection);
        Tool.ToolRun second = await Tool.RunAsync(Tool.Input(connection));
        IReadOnlyList<string> afterSecond = await HistoryAsync(connection);

        Assert.Equal((0, string.Empty), (first.Exit, first.Error));
        Assert.Equal((0, string.Empty), (second.Exit, second.Error));
        Assert.NotEmpty(afterFirst);
        Assert.Equal(afterFirst, afterSecond);
    }

    [Fact]
    public async Task MSG_OPS_002_AC3_RoleNamedInApplicationRolesCanReadAndWriteEveryTable()
    {
        (_, string connection) = await postgres.NewDatabaseAsync();
        string role = "app_" + Guid.NewGuid().ToString("N")[..8];
        await postgres.RunAsync($"CREATE ROLE {role} LOGIN PASSWORD 'test';");

        Tool.ToolRun run = await Tool.RunAsync(Tool.Input(connection, role));

        Assert.Equal(0, run.Exit);
        await using NpgsqlConnection database = new(new NpgsqlConnectionStringBuilder(connection) { Username = role, Password = "test" }.ConnectionString);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        Dictionary<string, bool> granted = [];
        await using (NpgsqlCommand tables = new(
            """
            SELECT c.relname,
                   has_table_privilege(c.oid, 'SELECT') AND has_table_privilege(c.oid, 'INSERT')
                   AND has_table_privilege(c.oid, 'UPDATE') AND has_table_privilege(c.oid, 'DELETE')
            FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'messaging' AND c.relkind IN ('r', 'p')
            """,
            database))
        {
            await using NpgsqlDataReader reader = await tables.ExecuteReaderAsync(TestContext.Current.CancellationToken);
            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            {
                granted[reader.GetString(0)] = reader.GetBoolean(1);
            }
        }

        Assert.Contains("key_versions", granted.Keys);
        Assert.Contains("alerts", granted.Keys);
        Assert.All(granted, table => Assert.True(table.Value, $"{table.Key} is not granted"));
        await using NpgsqlCommand write = new(
            """
            INSERT INTO messaging.alerts (condition, scope, holding) VALUES ('dispatch-lag', 'email', true);
            UPDATE messaging.alerts SET holding = false WHERE condition = 'dispatch-lag';
            SELECT count(*) FROM messaging.alerts;
            DELETE FROM messaging.alerts;
            """,
            database);
        await write.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Migrate_ConnectionThatCannotConnect_ExitsOneWithConnectionFailed()
    {
        Tool.ToolRun run = await Tool.RunAsync(Tool.Input("Host=127.0.0.1;Port=1;Timeout=2;Username=none;Database=none"));

        Assert.Equal(1, run.Exit);
        (string code, IReadOnlyDictionary<string, string> details) = run.Failure();
        Assert.Equal("messaging.migrate.connectionfailed", code);
        Assert.Empty(details);
    }

    [Fact]
    public async Task Migrate_MigrationThatFails_ExitsOneNamingIt()
    {
        (string name, string connection) = await postgres.NewDatabaseAsync();
        await postgres.RunAsync("CREATE SCHEMA messaging; CREATE TABLE messaging.key_versions (other int);", name);

        Tool.ToolRun run = await Tool.RunAsync(Tool.Input(connection));

        Assert.Equal(1, run.Exit);
        (string code, IReadOnlyDictionary<string, string> details) = run.Failure();
        Assert.Equal("messaging.migrate.failed", code);
        Assert.EndsWith("_Foundations", details["migration"], StringComparison.Ordinal);
        Assert.Empty(await HistoryAsync(connection));
    }

    private static async Task<IReadOnlyList<string>> HistoryAsync(string connection)
    {
        await using NpgsqlConnection database = new(connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("SELECT \"MigrationId\" FROM messaging.__migrations_history ORDER BY 1", database);
        List<string> ids = [];
        try
        {
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            {
                ids.Add(reader.GetString(0));
            }
        }
        catch (PostgresException missing) when (missing.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            return ids;
        }

        return ids;
    }
}