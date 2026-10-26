using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Npgsql;

using Xunit;

namespace Hermes.Cli.Tests;

[Trait("Category", "integration")]
public sealed class MigrateTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string History = "__migrations_history";

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
    public async Task MSG_OPS_002_AC3_RoleNamedInApplicationRolesCanReadAndWriteEveryTableButTheHistoryWhichItCanOnlyRead()
    {
        (_, string connection) = await postgres.NewDatabaseAsync();
        string role = NewName("app");
        await postgres.RunAsync($"CREATE ROLE {role} LOGIN PASSWORD 'test';");

        Tool.ToolRun run = await Tool.RunAsync(Tool.Input(connection, role));

        Assert.Equal((0, string.Empty), (run.Exit, run.Error));
        await using NpgsqlConnection database = new(new NpgsqlConnectionStringBuilder(connection) { Username = role, Password = "test" }.ConnectionString);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        Dictionary<string, (bool Read, bool Insert, bool Update, bool Delete)> granted = [];
        await using (NpgsqlCommand tables = new(
            """
            SELECT c.relname, has_table_privilege(c.oid, 'SELECT'), has_table_privilege(c.oid, 'INSERT'),
                   has_table_privilege(c.oid, 'UPDATE'), has_table_privilege(c.oid, 'DELETE')
            FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'messaging' AND c.relkind IN ('r', 'p')
            """,
            database))
        {
            await using NpgsqlDataReader reader = await tables.ExecuteReaderAsync(TestContext.Current.CancellationToken);
            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            {
                granted[reader.GetString(0)] = (reader.GetBoolean(1), reader.GetBoolean(2), reader.GetBoolean(3), reader.GetBoolean(4));
            }
        }

        Assert.Contains("key_versions", granted.Keys);
        Assert.Contains("alerts", granted.Keys);
        Assert.Equal((true, false, false, false), granted[History]);
        Assert.All(granted.Where(table => table.Key != History), table => Assert.True(table.Value == (true, true, true, true), $"{table.Key} is not granted"));
        await using NpgsqlCommand write = new(
            """
            INSERT INTO messaging.alerts (condition, scope, holding) VALUES ('dispatch-lag', 'email', true);
            UPDATE messaging.alerts SET holding = false WHERE condition = 'dispatch-lag';
            SELECT count(*) FROM messaging.alerts;
            DELETE FROM messaging.alerts;
            SELECT count(*) FROM messaging.__migrations_history;
            """,
            database);
        await write.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task MSG_OPS_002_AC4_RoleTheDatabaseLacksExitsOneWithInputInvalidAndAppliesNothing()
    {
        (_, string connection) = await postgres.NewDatabaseAsync();
        string present = NewName("app");
        await postgres.RunAsync($"CREATE ROLE {present};");

        Tool.ToolRun run = await Tool.RunAsync(Tool.Input(connection, present, NewName("missing")));

        AssertFailure(run, "messaging.migrate.inputinvalid", "member", "applicationRoles");
        Assert.Empty(await HistoryAsync(connection));
    }

    [Fact]
    public async Task MSG_OPS_002_AC4_NameOf64BytesWhoseFirst63AreARolesNameExitsOneWithInputInvalidAndAppliesNothing()
    {
        (_, string connection) = await postgres.NewDatabaseAsync();
        string role = NewName("app").PadRight(63, 'r');
        await postgres.RunAsync($"CREATE ROLE {role};");

        Tool.ToolRun run = await Tool.RunAsync(Tool.Input(connection, role + "s"));

        AssertFailure(run, "messaging.migrate.inputinvalid", "member", "applicationRoles");
        Assert.Empty(await HistoryAsync(connection));
    }

    [Fact]
    public async Task MSG_OPS_002_AC5_TableOwnedByAnotherRoleThatGrantsSelectWithoutTheRightToGrantItExitsOneWithGrantFailedAndGrantsNothing()
    {
        (string name, string connection, string migrator) = await MigratedByAnOrdinaryRoleAsync();
        string other = NewName("owner");
        string role = NewName("app");
        await postgres.RunAsync(
            $"""
            CREATE ROLE {other};
            CREATE ROLE {role};
            CREATE TABLE messaging.extra (id int);
            ALTER TABLE messaging.extra OWNER TO {other};
            GRANT SELECT ON messaging.extra TO {migrator};
            """,
            name);

        Tool.ToolRun run = await Tool.RunAsync(Tool.Input(connection, role));

        AssertFailure(run, "messaging.migrate.grantfailed", "role", role);
        Assert.False(await HoldsAsync(name, "schema", role, "messaging", "USAGE"));
        Assert.False(await HoldsAsync(name, "table", role, "messaging.key_versions", "SELECT"));
        Assert.False(await HoldsAsync(name, "table", role, $"messaging.{History}", "SELECT"));
    }

    [Fact]
    public async Task Migrate_GrantThatFails_ExitsOneWithGrantFailedNamingTheRoleAndGrantsNothing()
    {
        (string name, string connection, _) = await MigratedByAnOrdinaryRoleAsync();
        string other = NewName("owner");
        string role = NewName("app");
        await postgres.RunAsync(
            $"""
            CREATE ROLE {other};
            CREATE ROLE {role};
            CREATE TABLE messaging.extra (id int);
            ALTER TABLE messaging.extra OWNER TO {other};
            """,
            name);

        Tool.ToolRun run = await Tool.RunAsync(Tool.Input(connection, role));

        AssertFailure(run, "messaging.migrate.grantfailed", "role", role);
        Assert.False(await HoldsAsync(name, "schema", role, "messaging", "USAGE"));
    }

    [Fact]
    public async Task Migrate_SequenceOfTheSchema_IsGrantedUsage()
    {
        (string name, string connection) = await postgres.NewDatabaseAsync();
        Assert.Equal(0, (await Tool.RunAsync(Tool.Input(connection))).Exit);
        string role = NewName("app");
        await postgres.RunAsync($"CREATE ROLE {role}; CREATE SEQUENCE messaging.extra_numbers;", name);

        Tool.ToolRun run = await Tool.RunAsync(Tool.Input(connection, role));

        Assert.Equal((0, string.Empty), (run.Exit, run.Error));
        Assert.True(await HoldsAsync(name, "sequence", role, "messaging.extra_numbers", "USAGE"));
    }

    [Fact]
    public async Task Migrate_PrivilegeARoleAlreadyHolds_IsNotRevoked()
    {
        (string name, string connection) = await postgres.NewDatabaseAsync();
        Assert.Equal(0, (await Tool.RunAsync(Tool.Input(connection))).Exit);
        string role = NewName("app");
        await postgres.RunAsync($"CREATE ROLE {role}; GRANT INSERT ON messaging.{History} TO {role};", name);

        Tool.ToolRun run = await Tool.RunAsync(Tool.Input(connection, role));

        Assert.Equal((0, string.Empty), (run.Exit, run.Error));
        Assert.True(await HoldsAsync(name, "table", role, $"messaging.{History}", "INSERT"));
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

    private static string NewName(string prefix)
    {
        return prefix + "_" + Guid.NewGuid().ToString("N")[..12];
    }

    private static void AssertFailure(Tool.ToolRun run, string code, string detail, string value)
    {
        Assert.Equal(1, run.Exit);
        (string failed, IReadOnlyDictionary<string, string> details) = run.Failure();
        Assert.Equal(code, failed);
        Assert.Equal(new Dictionary<string, string> { [detail] = value }, details);
    }

    /// <summary>
    /// Makes a database owned by a new login role that is no superuser, and applies the
    /// migrations as that role; answers the database, the role's connection string and the role.
    /// </summary>
    private async Task<(string Name, string Connection, string Migrator)> MigratedByAnOrdinaryRoleAsync()
    {
        (string name, string connection) = await postgres.NewDatabaseAsync();
        string migrator = NewName("migrator");
        await postgres.RunAsync($"CREATE ROLE {migrator} LOGIN PASSWORD 'test'; ALTER DATABASE {name} OWNER TO {migrator};");
        string own = new NpgsqlConnectionStringBuilder(connection) { Username = migrator, Password = "test" }.ConnectionString;
        Tool.ToolRun run = await Tool.RunAsync(Tool.Input(own));
        Assert.True(run.Exit == 0, run.Error);
        return (name, own, migrator);
    }

    /// <summary>Whether <paramref name="role"/> holds <paramref name="privilege"/> on <paramref name="target"/>, a schema, table or sequence, as the superuser reads it.</summary>
    private async Task<bool> HoldsAsync(string database, string kind, string role, string target, string privilege)
    {
        string query = kind switch
        {
            "schema" => "SELECT has_schema_privilege(@role::name, @target, @privilege)",
            "table" => "SELECT has_table_privilege(@role::name, @target, @privilege)",
            "sequence" => "SELECT has_sequence_privilege(@role::name, @target, @privilege)",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "A schema, table or sequence."),
        };
        await using NpgsqlConnection connection = new(postgres.Connection(database));
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new(query, connection);
        command.Parameters.AddWithValue("role", role);
        command.Parameters.AddWithValue("target", target);
        command.Parameters.AddWithValue("privilege", privilege);
        return (bool)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
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