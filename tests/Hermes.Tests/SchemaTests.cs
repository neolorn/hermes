using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Operations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Npgsql;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "integration")]
public sealed class SchemaTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task MSG_OPS_002_AC1_StartingAgainstADatabaseMissingTheReleasesLastMigrationStopsTheStart()
    {
        string connection = await postgres.NewDatabaseAsync();
        IReadOnlyList<string> migrations = SchemaMigration.Ids;
        if (migrations.Count > 1)
        {
            await using MessagingContext context = MessagingContext.Create(connection);
            await context.GetService<IMigrator>().MigrateAsync(migrations[^2], TestContext.Current.CancellationToken);
        }

        using IHost host = Host(connection);

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.schemamismatch", failure.Code);
        Assert.Equal(migrations[^1], failure.Details["migration"]);
    }

    [Fact]
    public async Task MSG_OVR_002_AC2_EveryTableTheLibraryUsesLivesInItsOwnSchema()
    {
        string connection = await postgres.NewDatabaseAsync();
        Assert.Null(await SchemaMigration.ApplyAsync(connection, [], TestContext.Current.CancellationToken));
        await using MessagingContext context = MessagingContext.Create(connection);
        List<string> schemas = [];
        await using (NpgsqlConnection database = new(connection))
        {
            await database.OpenAsync(TestContext.Current.CancellationToken);
            await using NpgsqlCommand command = new("SELECT DISTINCT table_schema FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog', 'information_schema')", database);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            {
                schemas.Add(reader.GetString(0));
            }
        }

        Assert.All(context.Model.GetEntityTypes(), entity => Assert.Equal("messaging", entity.GetSchema()));
        Assert.Equal(["messaging"], schemas);
    }

    [Fact]
    public async Task Start_DatabaseHoldingEveryMigration_PassesTheSchemaCheck()
    {
        string connection = await postgres.NewDatabaseAsync();
        Assert.Null(await SchemaMigration.ApplyAsync(connection, [], TestContext.Current.CancellationToken));

        using IHost host = Host(connection);

        await AssertPastTheSchema(host);
    }

    [Fact]
    public async Task Start_DatabaseHoldingALaterReleasesMigration_IsAccepted()
    {
        string connection = await postgres.NewDatabaseAsync();
        Assert.Null(await SchemaMigration.ApplyAsync(connection, [], TestContext.Current.CancellationToken));
        await using (NpgsqlConnection database = new(connection))
        {
            await database.OpenAsync(TestContext.Current.CancellationToken);
            await using NpgsqlCommand command = new("INSERT INTO messaging.__migrations_history VALUES ('99991231000000_Later', '10.0.14')", database);
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        using IHost host = Host(connection);

        await AssertPastTheSchema(host);
    }

    [Fact]
    public async Task Insert_KeyOutsideTheFiveKeys_IsRefusedByTheDatabase()
    {
        string connection = await postgres.NewDatabaseAsync();
        Assert.Null(await SchemaMigration.ApplyAsync(connection, [], TestContext.Current.CancellationToken));
        await using NpgsqlConnection database = new(connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("INSERT INTO messaging.key_versions (key, version, check_value, retired, value_count) VALUES ('otherKeys', 1, '\\x00', false, 0)", database);

        PostgresException refusal = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));

        Assert.Equal(PostgresErrorCodes.CheckViolation, refusal.SqlState);
    }

    private static IHost Host(string connection)
    {
        return TestHost.Build(
            TestHost.Languages(),
            declare =>
            {
                declare.Category("security").Essential();
                declare.Kind("sign.in", "security").Sms();
            },
            services => services.AddSingleton<ISecretSource>(Secrets.Held(connection)));
    }

    private static async Task AssertPastTheSchema(IHost host)
    {
        MessagingStartupException? failure = await Record.ExceptionAsync(() => host.StartAsync(TestContext.Current.CancellationToken)) as MessagingStartupException;

        Assert.True(
            failure is null || failure.Code is not ("messaging.startup.schemamismatch" or "messaging.startup.declarationmissing" or "messaging.startup.declarationinvalid" or "messaging.startup.configurationinvalid"),
            $"The start stopped at the schema: {failure?.Message}");
        if (failure is null)
        {
            await host.StopAsync(TestContext.Current.CancellationToken);
        }
    }
}