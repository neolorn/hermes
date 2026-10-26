using System;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace Hermes.Operations;

/// <summary>
/// The library's own connection to the host's database, opened at start from the secret
/// <c>connection</c> (<c>06</c> MSG-OPS-003), on which its own work runs.
/// </summary>
internal sealed class MessagingDatabase : IAsyncDisposable
{
    private NpgsqlDataSource? source;

    private DbContextOptions<MessagingContext>? options;

    /// <summary>The data source, once the start has opened it.</summary>
    internal NpgsqlDataSource Source => source ?? throw new InvalidOperationException("The library's database is used before the startup checks opened it.");

    /// <summary>Opens the data source on <paramref name="connection"/>.</summary>
    internal void Open(string connection)
    {
        source = new NpgsqlDataSourceBuilder(connection).Build();
        options = MessagingContext.Options(source);
    }

    /// <summary>A new context on the data source.</summary>
    internal MessagingContext Context()
    {
        return new MessagingContext(options ?? throw new InvalidOperationException("The library's database is used before the startup checks opened it."));
    }

    public ValueTask DisposeAsync()
    {
        return source?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}