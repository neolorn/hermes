using System;
using System.Threading.Tasks;

using DotNet.Testcontainers.Containers;

using Npgsql;

using Testcontainers.PostgreSql;

using Xunit;

namespace Hermes.Conversations.Tests;

/// <summary>One PostgreSQL container for a test class, each test taking a database of its own in it.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container;

    public PostgresFixture()
    {
        container = new PostgreSqlBuilder("postgres:16.15").WithPassword(Password).Build();
    }

    /// <summary>The database's password, made for this container, so a test can look for it in what the library writes.</summary>
    internal string Password { get; } = "pw" + Guid.NewGuid().ToString("N");

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await container.DisposeAsync();
    }

    /// <summary>Makes an empty database and answers its connection string.</summary>
    internal async Task<string> NewDatabaseAsync()
    {
        string name = "test_" + Guid.NewGuid().ToString("N");
        ExecResult made = await container.ExecScriptAsync($"CREATE DATABASE {name}", TestContext.Current.CancellationToken);
        Assert.True(made.ExitCode == 0, made.Stderr);
        return new NpgsqlConnectionStringBuilder(container.GetConnectionString()) { Database = name }.ConnectionString;
    }
}