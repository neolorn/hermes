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