using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Operations;
using Hermes.Sending;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using NpgsqlTypes;

using Xunit;

namespace Hermes.Tests;

/// <summary>
/// The hooks by which a table the engine does not know joins key retirement, its
/// re-protection, erasure and the person locks (M-031), proven with tables made by the tests
/// alone.
/// </summary>
[Trait("Category", "integration")]
public sealed class KeyedTableHookTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Person = "person-keyed-7f3a";

    private const string Other = "person-keyed-91c2";

    private const string Tables = """
        CREATE TABLE messaging.keyed_probes (id uuid PRIMARY KEY, person text, secret bytea, note bytea, hash bytea, hash_version integer, content_key_version integer);
        CREATE TABLE messaging.keyed_kept (id uuid PRIMARY KEY, person text, secret bytea, kind text NOT NULL, content_key_version integer);
        """;

    private static readonly KeyedTable Probes = new(
        "keyed_probes",
        "probes",
        person: "person",
        values: [new KeyedValue("secret", "secret"), new KeyedValue("note", "note")],
        contentKeyVersion: "content_key_version",
        hashVersion: "hash_version");

    private static readonly KeyedTable Kept = new(
        "keyed_kept",
        "kept",
        person: "person",
        values: [new KeyedValue("secret", "secret")],
        contentKeyVersion: "content_key_version",
        erasure: KeyedErasure.Clear);

    [Fact]
    public async Task ProtectAgain_ARowOfAKeyedTableUnderAnOlderVersion_IsProtectedAgainUnderTheWriteVersionAndTheOldVersionRetired()
    {
        string connection = await DatabaseAsync();
        Guid row = Guid.CreateVersion7();
        await using (EngineHost one = await StartAsync(connection))
        {
            await InsertAsync(one, row, Person, "a kept secret", "a kept note");
        }

        await using EngineHost two = await StartRotatedAsync(connection);
        RetentionKeys keys = two.Services.GetRequiredService<RetentionKeys>();
        Assert.True(await keys.ProtectAgainAsync(Renewed, TestContext.Current.CancellationToken));
        Assert.True(await keys.RetireAsync(Renewed, TestContext.Current.CancellationToken));

        Assert.Equal(2, await ScalarAsync<int>(two, database => With(new NpgsqlCommand("SELECT content_key_version FROM messaging.keyed_probes WHERE id = @id", database), row)));
        Assert.Equal(2, Messages.LibraryVersion(await ScalarAsync<byte[]>(two, database => With(new NpgsqlCommand("SELECT secret FROM messaging.keyed_probes WHERE id = @id", database), row))));
        Assert.Equal("a kept secret", await OpenAsync(two, row, note: false));
        Assert.Equal("a kept note", await OpenAsync(two, row, note: true));
        Assert.Contains("contentKeys:1", await RetiredAsync(two));
    }

    [Fact]
    public async Task Retire_AContentKeysVersionAKeyedTableRowStillUses_IsNotRetiredUntilTheRowIsGone()
    {
        string connection = await DatabaseAsync();
        Guid row = Guid.CreateVersion7();
        await using (EngineHost one = await StartAsync(connection))
        {
            await InsertAsync(one, row, Person, "a kept secret", null);
        }

        await using EngineHost two = await StartRotatedAsync(connection);

        // A value moved to another row no longer opens, so the job leaves the row under version 1.
        await two.RunAsync(database =>
        {
            NpgsqlCommand command = With(new NpgsqlCommand("UPDATE messaging.keyed_probes SET id = @moved WHERE id = @id", database), row);
            command.Parameters.AddWithValue("moved", Guid.CreateVersion7());
            return command;
        });
        RetentionKeys keys = two.Services.GetRequiredService<RetentionKeys>();
        Assert.True(await keys.ProtectAgainAsync(Renewed, TestContext.Current.CancellationToken));
        Assert.True(await keys.RetireAsync(Renewed, TestContext.Current.CancellationToken));
        Assert.Equal(1, await ScalarAsync<int>(two, database => new NpgsqlCommand("SELECT content_key_version FROM messaging.keyed_probes", database)));
        Assert.DoesNotContain("contentKeys:1", await RetiredAsync(two));

        await two.RunAsync(database => new NpgsqlCommand("DELETE FROM messaging.keyed_probes", database));
        Assert.True(await keys.RetireAsync(Renewed, TestContext.Current.CancellationToken));
        Assert.Contains("contentKeys:1", await RetiredAsync(two));
    }

    [Fact]
    public async Task Retire_AHashKeysVersionAKeyedTableRowRecords_IsNotRetiredUntilTheRowIsGone()
    {
        string connection = await DatabaseAsync();
        await using (EngineHost one = await StartAsync(connection))
        {
            await one.RunAsync(database => With(new NpgsqlCommand("INSERT INTO messaging.keyed_probes (id, hash, hash_version) VALUES (@id, '\\x01', 1)", database), Guid.CreateVersion7()));
        }

        await using EngineHost two = await StartRotatedAsync(connection);
        await two.RunAsync(database =>
        {
            NpgsqlCommand command = new("UPDATE messaging.key_versions SET first_made_at = @made, last_made_at = @made WHERE key = 'hashKeys' AND version = 1", database);
            command.Parameters.AddWithValue("made", EngineHost.Start);
            return command;
        });
        RetentionKeys keys = two.Services.GetRequiredService<RetentionKeys>();
        Assert.True(await keys.RetireAsync(Renewed, TestContext.Current.CancellationToken));
        Assert.DoesNotContain("hashKeys:1", await RetiredAsync(two));

        await two.RunAsync(database => new NpgsqlCommand("DELETE FROM messaging.keyed_probes", database));
        Assert.True(await keys.RetireAsync(Renewed, TestContext.Current.CancellationToken));
        Assert.Contains("hashKeys:1", await RetiredAsync(two));
    }

    [Fact]
    public async Task Erase_APersonsRowsOfKeyedTables_AreDeletedOrEmptiedAsEachTableSaysAndOtherPeoplesRowsStay()
    {
        string connection = await DatabaseAsync();
        await using EngineHost host = await StartAsync(connection);
        Guid erased = Guid.CreateVersion7();
        Guid kept = Guid.CreateVersion7();
        Guid emptied = Guid.CreateVersion7();
        Guid others = Guid.CreateVersion7();
        await InsertAsync(host, erased, Person, "the person's secret", null);
        await InsertAsync(host, kept, Other, "another person's secret", null);
        await InsertKeptAsync(host, emptied, Person, "kept row's secret");
        await InsertKeptAsync(host, others, Other, "another kept secret");

        Result erasure = await host.WithSubjectsAsync(subjects => subjects.EraseAsync(Person, TestContext.Current.CancellationToken).AsTask());

        Assert.True(erasure.IsSuccess);
        Assert.Equal(0L, await ScalarAsync<long>(host, database => With(new NpgsqlCommand("SELECT count(*) FROM messaging.keyed_probes WHERE id = @id", database), erased)));
        Assert.Equal("another person's secret", await OpenAsync(host, kept, note: false));
        Assert.Equal(1L, await ScalarAsync<long>(host, database => With(new NpgsqlCommand("SELECT count(*) FROM messaging.keyed_kept WHERE id = @id AND person IS NULL AND secret IS NULL AND content_key_version IS NULL AND kind = 'probe'", database), emptied)));
        Assert.Equal(1L, await ScalarAsync<long>(host, database => With(new NpgsqlCommand("SELECT count(*) FROM messaging.keyed_kept WHERE id = @id AND person IS NOT NULL AND secret IS NOT NULL", database), others)));
    }

    [Fact]
    public async Task Erase_WhileAWriterHoldsThePersonsLocksShared_WaitsForItAndErasesTheRowItWrote()
    {
        string connection = await DatabaseAsync();
        await using EngineHost host = await StartAsync(connection);
        Guid row = Guid.CreateVersion7();
        KeyedHashes hashes = host.Services.GetRequiredService<KeyedHashes>();
        MessagingContext writer = host.Services.GetRequiredService<MessagingDatabase>().Context();
        await using (writer)
        {
            await using IDbContextTransaction transaction = await writer.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
            await AdvisoryLocks.TakeAsync(writer, AdvisoryLocks.Person(hashes, Person), shared: true, TestContext.Current.CancellationToken);
            await writer.Database.ExecuteSqlRawAsync(
                "INSERT INTO messaging.keyed_probes (id, person) VALUES (@id, @person)",
                [new NpgsqlParameter("id", row), new NpgsqlParameter("person", Person)],
                TestContext.Current.CancellationToken);

            Task<Result> erasure = host.WithSubjectsAsync(subjects => subjects.EraseAsync(Person, TestContext.Current.CancellationToken).AsTask());
            await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
            Assert.False(erasure.IsCompleted);

            await transaction.CommitAsync(TestContext.Current.CancellationToken);
            Assert.True((await erasure).IsSuccess);
        }

        Assert.Equal(0L, await ScalarAsync<long>(host, database => With(new NpgsqlCommand("SELECT count(*) FROM messaging.keyed_probes WHERE id = @id", database), row)));
    }

    private static Task<bool> Renewed()
    {
        return Task.FromResult(true);
    }

    private async Task<string> DatabaseAsync()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        await using NpgsqlConnection database = new(connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new(Tables, database);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        return connection;
    }

    private static Task<EngineHost> StartAsync(string connection)
    {
        return EngineHost.StartAsync(connection, services: Register);
    }

    /// <summary>A process eight days on, holding versions 1 and 2 of the keys and writing under the second.</summary>
    private static Task<EngineHost> StartRotatedAsync(string connection)
    {
        return EngineHost.StartAsync(
            connection,
            configure: configuration =>
            {
                configuration["Hermes:Secrets:ContentKeys:WriteVersion"] = "2";
                configuration["Hermes:Secrets:HashKeys:WriteVersion"] = "2";
                configuration["Hermes:Secrets:TokenKeys:WriteVersion"] = "2";
            },
            services: Register,
            secrets: Secrets.Held(connection)
                .With("contentKeys", Secrets.Bytes(32, 1), Secrets.Bytes(32, 4))
                .With("hashKeys", Secrets.Bytes(32, 2), Secrets.Bytes(32, 5))
                .With("tokenKeys", Secrets.Bytes(32, 3), Secrets.Bytes(32, 6)),
            clock: new ManualClock(EngineHost.Start.AddDays(8)));
    }

    private static void Register(IServiceCollection services)
    {
        services.AddSingleton(Probes);
        services.AddSingleton(Kept);
    }

    private static NpgsqlCommand With(NpgsqlCommand command, Guid id)
    {
        command.Parameters.AddWithValue("id", id);
        return command;
    }

    /// <summary>Writes a probe row whose values are protected for <paramref name="person"/> as a writer of the table would.</summary>
    private static async Task InsertAsync(EngineHost host, Guid row, string person, string secret, string? note)
    {
        host.Uses(person, secret, note);
        (byte[] stored, int version) = await ProtectAsync(host, person, "probes", row, "secret", secret);
        byte[]? noted = note is null ? null : (await ProtectAsync(host, person, "probes", row, "note", note)).Stored;
        await host.RunAsync(database =>
        {
            NpgsqlCommand command = With(new NpgsqlCommand("INSERT INTO messaging.keyed_probes (id, person, secret, note, content_key_version) VALUES (@id, @person, @secret, @note, @version)", database), row);
            command.Parameters.AddWithValue("person", person);
            command.Parameters.AddWithValue("secret", stored);
            command.Parameters.Add(new NpgsqlParameter("note", NpgsqlDbType.Bytea) { Value = (object?)noted ?? DBNull.Value });
            command.Parameters.AddWithValue("version", version);
            return command;
        });
    }

    private static async Task InsertKeptAsync(EngineHost host, Guid row, string person, string secret)
    {
        host.Uses(person, secret);
        (byte[] stored, int version) = await ProtectAsync(host, person, "kept", row, "secret", secret);
        await host.RunAsync(database =>
        {
            NpgsqlCommand command = With(new NpgsqlCommand("INSERT INTO messaging.keyed_kept (id, person, secret, kind, content_key_version) VALUES (@id, @person, @secret, 'probe', @version)", database), row);
            command.Parameters.AddWithValue("person", person);
            command.Parameters.AddWithValue("secret", stored);
            command.Parameters.AddWithValue("version", version);
            return command;
        });
    }

    private static async Task<(byte[] Stored, int Version)> ProtectAsync(EngineHost host, string person, string table, Guid row, string field, string value)
    {
        AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using (scope)
        {
            ContentProtecting made = await scope.ServiceProvider.GetRequiredService<ContentProtection>()
                .ProtectAsync(person, Encoding.UTF8.GetBytes(value), ContentContext.Of(table, row, field), TestContext.Current.CancellationToken);
            return (made.Stored!, Messages.LibraryVersion(made.Stored!)!.Value);
        }
    }

    /// <summary>A probe row's secret, or its note, opened as its writer would open it.</summary>
    private static async Task<string> OpenAsync(EngineHost host, Guid row, bool note)
    {
        byte[] stored = note
            ? await ScalarAsync<byte[]>(host, database => With(new NpgsqlCommand("SELECT note FROM messaging.keyed_probes WHERE id = @id", database), row))
            : await ScalarAsync<byte[]>(host, database => With(new NpgsqlCommand("SELECT secret FROM messaging.keyed_probes WHERE id = @id", database), row));
        string person = await ScalarAsync<string>(host, database => With(new NpgsqlCommand("SELECT person FROM messaging.keyed_probes WHERE id = @id", database), row));
        AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using (scope)
        {
            ContentOpening opening = await scope.ServiceProvider.GetRequiredService<ContentProtection>()
                .OpenAsync(person, stored, ContentContext.Of("probes", row, note ? "note" : "secret"), TestContext.Current.CancellationToken);
            Assert.Equal(ContentOpeningStatus.Opened, opening.Status);
            return Encoding.UTF8.GetString(opening.Plaintext.Span);
        }
    }

    private static async Task<T> ScalarAsync<T>(EngineHost host, Func<NpgsqlConnection, NpgsqlCommand> make)
    {
        await using NpgsqlConnection database = new(host.Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = make(database);
        return (T)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    /// <summary>The versions marked retired, each as <c>key:version</c>.</summary>
    private static async Task<List<string>> RetiredAsync(EngineHost host)
    {
        await using NpgsqlConnection database = new(host.Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("SELECT key || ':' || version FROM messaging.key_versions WHERE retired ORDER BY key, version", database);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        List<string> retired = [];
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            retired.Add(reader.GetString(0));
        }

        return retired;
    }
}