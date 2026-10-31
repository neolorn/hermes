using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Operations;
using Hermes.Sending;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
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

    /// <summary>The shortest value the log check looks for, so a short code cannot match a count or a time by chance.</summary>
    private const int ShortestChecked = 4;

    private readonly LogCapture logs;

    private readonly ConcurrentDictionary<string, byte> used = new(StringComparer.Ordinal);

    private int stopped;

    private EngineHost(IHost host, string connection, ManualClock clock, LogCapture logs)
    {
        Host = host;
        Connection = connection;
        Clock = clock;
        this.logs = logs;
    }

    internal IHost Host { get; }

    internal string Connection { get; }

    internal ManualClock Clock { get; }

    internal IServiceProvider Services => Host.Services;

    /// <summary>A client of the test server, where the host maps endpoints.</summary>
    internal HttpClient Client()
    {
        return Host.GetTestClient();
    }

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
            { Channel: Channel.InApp, Part: "title" } => $"Title {language} {{{{ code }}}}",
            { Channel: Channel.InApp, Part: "body" } => $"Body {language}",
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
        ManualClock? clock = null,
        LogCapture? logs = null,
        Action<WebApplication>? map = null)
    {
        ManualClock time = clock ?? new ManualClock(Start);
        LogCapture capture = logs ?? new LogCapture();
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
            logs: capture,
            hostTemplates: false,
            map: map);
        try
        {
            await host.StartAsync(TestContext.Current.CancellationToken);
        }
        catch
        {
            host.Dispose();
            throw;
        }

        return new EngineHost(host, connection, time, capture);
    }

    internal async Task<Result<SendReceipt>> SendAsync(Send send)
    {
        ArgumentNullException.ThrowIfNull(send);
        Uses(send.Email, send.Phone, (send.Recipient as Recipient.Person)?.Reference);
        Uses([.. send.Values.Values.OfType<SendValue.PlainText>().Select(value => value.Value)]);
        AsyncServiceScope scope = Services.CreateAsyncScope();
        await using (scope)
        {
            return await scope.ServiceProvider.GetRequiredService<IMessages>().SendAsync(send, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>The configuration of an application that dispatches when a test runs a pass, its retries without jitter and its rates past any test's batch.</summary>
    internal static void Dispatching(Dictionary<string, string?> configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        configuration["Hermes:Dispatch:Enabled"] = "true";
        configuration["Hermes:Dispatch:Hosted"] = "false";
        configuration["Hermes:Retry:Jitter"] = "0";
        configuration["Hermes:Email:Rate"] = "1000";
        configuration["Hermes:Sms:Rate"] = "1000";
    }

    /// <summary>Runs one pass of each channel.</summary>
    internal Task<int> DispatchAsync(CancellationToken? cancellationToken = null)
    {
        return Services.GetRequiredService<IDispatcher>().RunPassAsync(cancellationToken ?? TestContext.Current.CancellationToken).AsTask();
    }

    /// <summary>The send as <c>IMessages.ReadAsync</c> answers it.</summary>
    internal async Task<SendRecord> ReadAsync(SendId send)
    {
        Result<SendRecord> record = await WithMessagesAsync(messages => messages.ReadAsync(send, TestContext.Current.CancellationToken).AsTask());
        return record.Value!;
    }

    internal async Task<T> WithMessagesAsync<T>(Func<IMessages, Task<T>> use)
    {
        AsyncServiceScope scope = Services.CreateAsyncScope();
        await using (scope)
        {
            return await use(scope.ServiceProvider.GetRequiredService<IMessages>());
        }
    }

    internal async Task<T> WithSubjectsAsync<T>(Func<ISubjects, Task<T>> use)
    {
        AsyncServiceScope scope = Services.CreateAsyncScope();
        await using (scope)
        {
            return await use(scope.ServiceProvider.GetRequiredService<ISubjects>());
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

    /// <summary>Adds destinations, content or person references the test used, which no library log line may hold (<c>06</c> MSG-OPS-005).</summary>
    internal void Uses(params string?[] values)
    {
        foreach (string? value in values)
        {
            if (value is { Length: >= ShortestChecked })
            {
                used.TryAdd(value, 0);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref stopped, 1) == 1)
        {
            return;
        }

        try
        {
            await Host.StopAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            Host.Dispose();
        }

        List<string> logged = [.. logs.Entries
            .Where(entry => entry.Category.StartsWith("Hermes.", StringComparison.Ordinal))
            .SelectMany(entry => used.Keys.Where(value => entry.Message.Contains(value, StringComparison.Ordinal)).Select(value => $"{entry.Category}: '{value}' in \"{entry.Message}\""))];
        Assert.True(logged.Count == 0, "A library log line holds a destination, content or person reference the test used: " + string.Join("; ", logged));
    }
}