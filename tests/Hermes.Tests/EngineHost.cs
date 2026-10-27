using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Operations;
using Hermes.Sending;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Npgsql;

using Xunit;

namespace Hermes.Tests;

/// <summary>
/// A started host on a migrated test database, its clock a <see cref="ManualClock"/>, with the
/// templates <see cref="Templates(TemplateKey)"/> answers unless a test gives its own; it
/// records sends and, unless a test turns them on, dispatches nothing and runs no job.
/// </summary>
internal sealed class EngineHost : IAsyncDisposable
{
    internal const string Kind = "sign.in";

    internal const string Category = "security";

    internal static readonly DateTimeOffset Start = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    private EngineHost(IHost host, string connection, ManualClock clock)
    {
        Host = host;
        Connection = connection;
        Clock = clock;
    }

    internal IHost Host { get; }

    internal string Connection { get; }

    internal ManualClock Clock { get; }

    internal IServiceProvider Services => Host.Services;

    /// <summary>An essential kind on every channel.</summary>
    internal static void Essential(DeclarationBuilder declare)
    {
        declare.Category(Category).Essential();
        declare.Kind(Kind, Category).Email().Sms().Push();
    }

    /// <summary>The configuration a started host takes: <c>en</c> and <c>ar</c>, sends recorded, nothing dispatched and no job run.</summary>
    internal static Dictionary<string, string?> Configuration()
    {
        Dictionary<string, string?> configuration = TestHost.Languages("en", "ar");
        configuration["Hermes:Dispatch:Enabled"] = "false";
        configuration["Hermes:Jobs:Enabled"] = "false";
        return configuration;
    }

    /// <summary>The templates of every kind: each part names its language and reads <c>code</c>, a page answering none so the shipped one is read.</summary>
    internal static string? Templates(TemplateKey key)
    {
        string language = key.Language;
        return key switch
        {
            { Page: not null } => null,
            { Channel: Channel.Email, Part: "subject" } => $"Subject {language} {{{{ code }}}}",
            { Channel: Channel.Email, Part: "text" } => $"Text {language} {{{{ code }}}}",
            { Channel: Channel.Sms, Part: "text" } => $"Sms {language} {{{{ code }}}}",
            { Channel: Channel.Push, Part: "title" } => $"Title {language}",
            { Channel: Channel.Push, Part: "body" } => $"Body {language} {{{{ code }}}}",
            _ => null,
        };
    }

    /// <summary>A database of its own, the release's migrations applied.</summary>
    internal static async Task<string> DatabaseAsync(PostgresFixture postgres)
    {
        string connection = await postgres.NewDatabaseAsync();
        Assert.Null(await SchemaMigration.ApplyAsync(connection, [], TestContext.Current.CancellationToken));
        return connection;
    }

    internal static async Task<EngineHost> StartAsync(
        string connection,
        Action<DeclarationBuilder>? declare = null,
        Func<TemplateKey, string?>? templates = null,
        Action<Dictionary<string, string?>>? configure = null,
        Action<IServiceCollection>? services = null,
        SecretSourceFake? secrets = null,
        ManualClock? clock = null)
    {
        ManualClock time = clock ?? new ManualClock(Start);
        Dictionary<string, string?> configuration = Configuration();
        configure?.Invoke(configuration);
        IHost host = TestHost.Build(
            configuration,
            declare ?? Essential,
            collection =>
            {
                collection.AddSingleton<TimeProvider>(time);
                collection.AddSingleton<ISecretSource>(secrets ?? Secrets.Held(connection));
                collection.AddSingleton<ITemplateSource>(new TemplateSourceFake(templates ?? Templates));
                services?.Invoke(collection);
            },
            hostTemplates: false);
        try
        {
            await host.StartAsync(TestContext.Current.CancellationToken);
        }
        catch
        {
            host.Dispose();
            throw;
        }

        return new EngineHost(host, connection, time);
    }

    internal async Task<Result<SendReceipt>> SendAsync(Send send)
    {
        AsyncServiceScope scope = Services.CreateAsyncScope();
        await using (scope)
        {
            return await scope.ServiceProvider.GetRequiredService<IMessages>().SendAsync(send, TestContext.Current.CancellationToken);
        }
    }

    internal async Task<T> WithMessagesAsync<T>(Func<IMessages, Task<T>> use)
    {
        AsyncServiceScope scope = Services.CreateAsyncScope();
        await using (scope)
        {
            return await use(scope.ServiceProvider.GetRequiredService<IMessages>());
        }
    }

    /// <summary>The sends, deliveries and idempotency rows recorded, as one count each.</summary>
    internal async Task<(long Sends, long Deliveries, long Keys)> RecordedAsync()
    {
        const string Counts = "SELECT (SELECT count(*) FROM messaging.sends), (SELECT count(*) FROM messaging.deliveries), (SELECT count(*) FROM messaging.idempotency)";
        await using NpgsqlConnection database = new(Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new(Counts, database);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        await reader.ReadAsync(TestContext.Current.CancellationToken);
        return (reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2));
    }

    /// <summary>Runs the command <paramref name="command"/> makes on a connection to the test database, answering the rows it changed.</summary>
    internal async Task<int> RunAsync(Func<NpgsqlConnection, NpgsqlCommand> command)
    {
        await using NpgsqlConnection database = new(Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand made = command(database);
        return await made.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A delivery's column as stored.</summary>
    internal async Task<T?> ColumnAsync<T>(Guid delivery, string column)
    {
        const string Row = "SELECT * FROM messaging.deliveries WHERE id = @id";
        await using NpgsqlConnection database = new(Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new(Row, database);
        command.Parameters.AddWithValue("id", delivery);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken), "No such delivery.");
        object value = reader[column];
        return value is DBNull ? default : (T)value;
    }

    /// <summary>A delivery's protected value opened, as the dispatcher opens it, or none where the row holds none.</summary>
    internal async Task<string?> OpenAsync(Guid delivery, string field)
    {
        byte[]? stored = await ColumnAsync<byte[]>(delivery, field);
        if (stored is null)
        {
            return null;
        }

        string? person = await ColumnAsync<string>(delivery, "person");
        AsyncServiceScope scope = Services.CreateAsyncScope();
        await using (scope)
        {
            ContentOpening opening = await scope.ServiceProvider.GetRequiredService<ContentProtection>().OpenAsync(person, stored, ContentContext.Of(ContentContext.Deliveries, delivery, field), TestContext.Current.CancellationToken);
            Assert.Equal(ContentOpeningStatus.Opened, opening.Status);
            return Encoding.UTF8.GetString(opening.Plaintext.Span);
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await Host.StopAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            Host.Dispose();
        }
    }
}