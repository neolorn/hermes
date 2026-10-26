using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Operations;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Npgsql;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "integration")]
public sealed class KeyTableTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Start = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task MSG_LIB_003_AC1_AHostWithValidDeclarationsAndConfigurationStartsWithTheOneCall()
    {
        string connection = await MigratedAsync();
        using IHost host = Host(Secrets.Held(connection), new ManualClock(Start));

        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Start_HeldVersions_RecordEachCheckValue()
    {
        string connection = await MigratedAsync();
        using IHost host = Host(Secrets.Held(connection), new ManualClock(Start), Pushed);

        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        IReadOnlyDictionary<(string, int), Row> rows = await RowsAsync(connection);
        Assert.Equal(HMACSHA256.HashData(Secrets.Bytes(32, 2), "messaging-key-check"u8)[..16], rows[("hashKeys", 1)].CheckValue);
        Assert.Equal(HMACSHA256.HashData(Secrets.Bytes(32, 1), "messaging-key-check"u8)[..16], rows[("contentKeys", 1)].CheckValue);
        Assert.Equal(VapidPublicKey(Convert.FromHexString(Secrets.VapidScalar)), rows[("pushVapidKeys", 1)].CheckValue);
        Assert.Equal(65, rows[("pushVapidKeys", 1)].CheckValue.Length);
        Assert.DoesNotContain(("tokenKeys", 1), rows.Keys);
    }

    [Fact]
    public async Task MSG_OPS_008_AC2_AVersionWhoseBytesChangedSinceFirstUsedStopsTheStart()
    {
        string connection = await MigratedAsync();
        using (IHost first = Host(Secrets.Held(connection), new ManualClock(Start)))
        {
            await first.StartAsync(TestContext.Current.CancellationToken);
            await first.StopAsync(TestContext.Current.CancellationToken);
        }

        using IHost changed = Host(Secrets.Held(connection).With("hashKeys", Secrets.Bytes(32, 9)), new ManualClock(Start));

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => changed.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.secretinvalid", failure.Code);
        Assert.Equal("hashKeys", failure.Details["key"]);
    }

    [Fact]
    public async Task MSG_OPS_008_AC2_APasswordChangedInEmailSubmissionDoesNotStopTheStart()
    {
        string connection = await MigratedAsync();
        using (IHost first = Host(Secrets.Held(connection), new ManualClock(Start), Newsletter))
        {
            await first.StartAsync(TestContext.Current.CancellationToken);
            await first.StopAsync(TestContext.Current.CancellationToken);
        }

        using IHost changed = Host(Secrets.Held(connection).With("emailSubmission", Secrets.Account(Secrets.Password + "0")), new ManualClock(Start), Newsletter);

        await changed.StartAsync(TestContext.Current.CancellationToken);
        await changed.StopAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(await RowsAsync(connection), row => row.Key.Item1 == "emailSubmission");
    }

    [Fact]
    public async Task Start_WriteVersions_SetTheirFirstAndLastMadeInstantsAndClearTheirRetiredMark()
    {
        string connection = await MigratedAsync();
        DateTimeOffset earlier = Start.AddDays(-30);
        await RecordHashKeyAsync(connection, HMACSHA256.HashData(Secrets.Bytes(32, 2), "messaging-key-check"u8)[..16], earlier, retired: true);
        SecretSourceFake secrets = Secrets.Held(connection).With("contentKeys", Secrets.Bytes(32, 1), Secrets.Bytes(32, 4));
        using IHost host = Host(secrets, new ManualClock(Start), configure: configuration => configuration["Hermes:Secrets:ContentKeys:WriteVersion"] = "2");

        await host.StartAsync(TestContext.Current.CancellationToken);

        IReadOnlyDictionary<(string, int), Row> rows = await RowsAsync(connection);
        Assert.Equal(new Row(rows[("hashKeys", 1)].CheckValue, earlier, Start, false), rows[("hashKeys", 1)]);
        Assert.Equal((Start, Start, false), (rows[("contentKeys", 2)].FirstMadeAt, rows[("contentKeys", 2)].LastMadeAt, rows[("contentKeys", 2)].Retired));
        Assert.Equal((null, null, false), (rows[("contentKeys", 1)].FirstMadeAt, rows[("contentKeys", 1)].LastMadeAt, rows[("contentKeys", 1)].Retired));
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Start_VersionInUseNotHeld_StopsTheStartAsUnavailable()
    {
        string connection = await MigratedAsync();
        await RecordHashKeyAsync(connection, [0x00], Start.AddDays(-1), retired: false);
        using IHost host = Host(Secrets.Held(connection).WithVersions("hashKeys", (2, Secrets.Bytes(32, 2))), new ManualClock(Start));

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.secretunavailable", failure.Code);
        Assert.Equal("hashKeys", failure.Details["key"]);
    }

    [Fact]
    public async Task Start_VersionRetiredNotHeld_Starts()
    {
        string connection = await MigratedAsync();
        await RecordHashKeyAsync(connection, [0x00], Start.AddDays(-30), retired: true);
        using IHost host = Host(Secrets.Held(connection).WithVersions("hashKeys", (2, Secrets.Bytes(32, 2))), new ManualClock(Start));

        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Start_ProcessReadingTokenKeys_RecordsTheLongestTokenLifetime()
    {
        string connection = await MigratedAsync();

        await StartAndStopAsync(connection, "1825.00:00:00");
        TimeSpan? afterLonger = await TokenLifetimeAsync(connection);
        await StartAndStopAsync(connection, "730.00:00:00");
        TimeSpan? afterShorter = await TokenLifetimeAsync(connection);

        Assert.Equal(TimeSpan.FromDays(1825), afterLonger);
        Assert.Equal(TimeSpan.FromDays(1825), afterShorter);
    }

    [Fact]
    public async Task Start_ProcessNotReadingTokenKeys_RecordsNoTokenLifetime()
    {
        string connection = await MigratedAsync();
        using IHost host = Host(Secrets.Held(connection), new ManualClock(Start));

        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        Assert.Null(await TokenLifetimeAsync(connection));
    }

    [Fact]
    public async Task Timer_EachIntervalAndTheStop_WriteTheWriteVersionsLastMadeInstant()
    {
        string connection = await MigratedAsync();
        ManualClock clock = new(Start);
        using IHost host = Host(Secrets.Held(connection), clock);
        await host.StartAsync(TestContext.Current.CancellationToken);

        clock.Advance(TimeSpan.FromSeconds(5));
        DateTimeOffset? ticked = await WaitForLastMadeAsync(connection, Start.AddSeconds(5));
        clock.Advance(TimeSpan.FromSeconds(2));
        await host.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(Start.AddSeconds(5), ticked);
        Assert.Equal(Start.AddSeconds(7), (await RowsAsync(connection))[("hashKeys", 1)].LastMadeAt);
    }

    private static void Essential(DeclarationBuilder declare)
    {
        declare.Category("security").Essential();
        declare.Kind("sign.in", "security").Sms();
    }

    private static void Pushed(DeclarationBuilder declare)
    {
        declare.Category("security").Essential();
        declare.Kind("sign.in", "security").Push();
    }

    private static void Newsletter(DeclarationBuilder declare)
    {
        declare.Category("news").Label("en", "News");
        declare.Kind("news.issued", "news").Email();
    }

    private static IHost Host(SecretSourceFake secrets, TimeProvider clock, Action<DeclarationBuilder>? declare = null, Action<Dictionary<string, string?>>? configure = null)
    {
        Dictionary<string, string?> configuration = TestHost.Languages();
        configuration["Hermes:PublicBaseAddress"] = "https://example.test/";
        configure?.Invoke(configuration);
        return TestHost.Build(
            configuration,
            declare ?? Essential,
            services => services.AddSingleton<ISecretSource>(secrets).AddSingleton(clock));
    }

    private async Task<string> MigratedAsync()
    {
        string connection = await postgres.NewDatabaseAsync();
        Assert.Null(await SchemaMigration.ApplyAsync(connection, [], TestContext.Current.CancellationToken));
        return connection;
    }

    private static async Task StartAndStopAsync(string connection, string tokenLifetime)
    {
        using IHost host = Host(Secrets.Held(connection), new ManualClock(Start), Newsletter, configuration => configuration["Hermes:Unsubscribe:TokenLifetime"] = tokenLifetime);
        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<DateTimeOffset?> WaitForLastMadeAsync(string connection, DateTimeOffset expected)
    {
        DateTimeOffset? lastMade = null;
        for (int attempt = 0; attempt < 100 && lastMade != expected; attempt++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
            lastMade = (await RowsAsync(connection))[("hashKeys", 1)].LastMadeAt;
        }

        return lastMade;
    }

    private static byte[] VapidPublicKey(byte[] scalar)
    {
        using ECDsa key = ECDsa.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, D = scalar });
        ECParameters parameters = key.ExportParameters(false);
        return [0x04, .. parameters.Q.X!, .. parameters.Q.Y!];
    }

    private static async Task RecordHashKeyAsync(string connection, byte[] check, DateTimeOffset made, bool retired)
    {
        await using NpgsqlConnection database = new(connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("INSERT INTO messaging.key_versions (key, version, check_value, first_made_at, last_made_at, retired, value_count) VALUES ('hashKeys', 1, @check, @made, @made, @retired, 0)", database);
        command.Parameters.AddWithValue("check", check);
        command.Parameters.AddWithValue("made", made);
        command.Parameters.AddWithValue("retired", retired);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<IReadOnlyDictionary<(string, int), Row>> RowsAsync(string connection)
    {
        await using NpgsqlConnection database = new(connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("SELECT key, version, check_value, first_made_at, last_made_at, retired FROM messaging.key_versions", database);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Dictionary<(string, int), Row> rows = [];
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            rows[(reader.GetString(0), reader.GetInt32(1))] = new Row(
                (byte[])reader[2],
                await reader.IsDBNullAsync(3, TestContext.Current.CancellationToken) ? null : await reader.GetFieldValueAsync<DateTimeOffset>(3, TestContext.Current.CancellationToken),
                await reader.IsDBNullAsync(4, TestContext.Current.CancellationToken) ? null : await reader.GetFieldValueAsync<DateTimeOffset>(4, TestContext.Current.CancellationToken),
                reader.GetBoolean(5));
        }

        return rows;
    }

    private static async Task<TimeSpan?> TokenLifetimeAsync(string connection)
    {
        await using NpgsqlConnection database = new(connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("SELECT longest FROM messaging.token_lifetime", database);
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) is TimeSpan longest ? longest : null;
    }

    private sealed record Row(byte[] CheckValue, DateTimeOffset? FirstMadeAt, DateTimeOffset? LastMadeAt, bool Retired)
    {
        public bool Equals(Row? other)
        {
            return other is not null && CheckValue.AsSpan().SequenceEqual(other.CheckValue) && FirstMadeAt == other.FirstMadeAt && LastMadeAt == other.LastMadeAt && Retired == other.Retired;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(FirstMadeAt, LastMadeAt, Retired);
        }
    }
}