using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
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

[Trait("Category", "integration")]
public sealed class ContentProtectionTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Person = "person-1";

    private static readonly byte[] Destination = Encoding.UTF8.GetBytes("someone@example.test");

    private static readonly string Row = ContentContext.Of(ContentContext.Deliveries, new Guid("0f8fad5b-d9cb-469f-a165-70867728950e"), "destination");

    [Fact]
    public async Task MSG_SEND_018_AProtectedValueOpensInItsRowForItsPersonAndNowhereElse()
    {
        await using Started started = await StartAsync();
        byte[] stored = await started.ProtectAsync(Person);

        ContentOpening inItsRow = await started.Protection.OpenAsync(Person, stored, Row, TestContext.Current.CancellationToken);
        ContentOpening inAnotherRow = await started.Protection.OpenAsync(Person, stored, ContentContext.Of(ContentContext.Deliveries, Guid.NewGuid(), "destination"), TestContext.Current.CancellationToken);
        ContentOpening inAnotherField = await started.Protection.OpenAsync(Person, stored, ContentContext.Of(ContentContext.Deliveries, new Guid("0f8fad5b-d9cb-469f-a165-70867728950e"), "subject"), TestContext.Current.CancellationToken);
        ContentOpening forAnotherPerson = await started.Protection.OpenAsync("person-2", stored, Row, TestContext.Current.CancellationToken);
        ContentOpening forNoPerson = await started.Protection.OpenAsync(null, stored, Row, TestContext.Current.CancellationToken);

        Assert.Equal(ContentOpeningStatus.Opened, inItsRow.Status);
        Assert.Equal(Destination, inItsRow.Plaintext.ToArray());
        Assert.Equal(
            [ContentOpeningStatus.Rejected, ContentOpeningStatus.Rejected, ContentOpeningStatus.Rejected, ContentOpeningStatus.Rejected],
            new[] { inAnotherRow, inAnotherField, forAnotherPerson, forNoPerson }.Select(opening => opening.Status));
    }

    [Fact]
    public async Task MSG_SEND_018_AValueOfAHostProtectorOpensInItsRowForItsPersonAndNowhereElse()
    {
        await using Started started = await StartAsync(new ContentProtectorFake("vault"));
        byte[] stored = await started.ProtectAsync(Person);

        ContentOpening inItsRow = await started.Protection.OpenAsync(Person, stored, Row, TestContext.Current.CancellationToken);
        ContentOpening inAnotherRow = await started.Protection.OpenAsync(Person, stored, ContentContext.Of(ContentContext.Deliveries, Guid.NewGuid(), "destination"), TestContext.Current.CancellationToken);
        ContentOpening forAnotherPerson = await started.Protection.OpenAsync("person-2", stored, Row, TestContext.Current.CancellationToken);
        ContentOpening inAnAnonymousRow = await started.Protection.OpenAsync(null, stored, Row, TestContext.Current.CancellationToken);

        Assert.Equal(Destination, inItsRow.Plaintext.ToArray());
        Assert.Equal(
            [ContentOpeningStatus.Rejected, ContentOpeningStatus.Rejected, ContentOpeningStatus.Rejected],
            new[] { inAnotherRow, forAnotherPerson, inAnAnonymousRow }.Select(opening => opening.Status));
    }

    [Fact]
    public async Task Protect_NoHostProtector_WritesTheLibrarysEnvelopeAndLayout()
    {
        await using Started started = await StartAsync();

        byte[] stored = await started.ProtectAsync(Person);

        Assert.Equal([0x01, 9, .. "messaging"u8], stored[..11]);
        byte[] output = stored[11..];
        Assert.Equal(1, BinaryPrimitives.ReadInt32BigEndian(output.AsSpan(0, 4)));
        Assert.Equal(4 + 12 + Destination.Length + 16, output.Length);
        byte[] opened = new byte[Destination.Length];
        using AesGcm aes = new(Secrets.Bytes(32, 1), 16);
        aes.Decrypt(output.AsSpan(4, 12), output.AsSpan(16, Destination.Length), output.AsSpan(16 + Destination.Length), opened, [.. Encoding.UTF8.GetBytes(Row), 0x00, .. Encoding.UTF8.GetBytes(Person)]);
        Assert.Equal(Destination, opened);
    }

    [Fact]
    public async Task Protect_AnonymousRecipient_BindsTheLibrarysValueToAnEmptyPerson()
    {
        await using Started started = await StartAsync();

        byte[] stored = await started.ProtectAsync(null);

        byte[] output = stored[11..];
        byte[] opened = new byte[Destination.Length];
        using AesGcm aes = new(Secrets.Bytes(32, 1), 16);
        aes.Decrypt(output.AsSpan(4, 12), output.AsSpan(16, Destination.Length), output.AsSpan(16 + Destination.Length), opened, [.. Encoding.UTF8.GetBytes(Row), 0x00]);
        Assert.Equal(Destination, opened);
    }

    [Fact]
    public async Task Protect_HostProtectorRegistered_WritesItsEnvelope()
    {
        await using Started started = await StartAsync(new ContentProtectorFake("vault"));

        byte[] stored = await started.ProtectAsync(Person);

        Assert.Equal([0x01, 5, .. "vault"u8, .. Encoding.UTF8.GetBytes($"vault|{Person}|{Row}|"), .. Destination], stored);
    }

    [Fact]
    public async Task Protect_AnonymousRecipient_UsesTheLibrarysProtectorWhateverIsRegistered()
    {
        await using Started started = await StartAsync(new ContentProtectorFake("vault"));

        byte[] stored = await started.ProtectAsync(null);

        Assert.Equal([0x01, 9, .. "messaging"u8], stored[..11]);
        Assert.Equal(Destination, (await started.Protection.OpenAsync(null, stored, Row, TestContext.Current.CancellationToken)).Plaintext.ToArray());
    }

    [Fact]
    public async Task MSG_LIB_005_AC2_WithTwoHostProtectorsValuesAreWrittenByTheLastAndValuesOfTheFirstStillOpen()
    {
        string connection = await MigratedAsync();
        byte[] first;
        await using (Started alone = await StartAsync(connection, [new ContentProtectorFake("first")]))
        {
            first = await alone.ProtectAsync(Person);
        }

        await using Started both = await StartAsync(connection, [new ContentProtectorFake("first"), new ContentProtectorFake("second")]);
        byte[] written = await both.ProtectAsync(Person);
        ContentOpening opened = await both.Protection.OpenAsync(Person, first, Row, TestContext.Current.CancellationToken);

        Assert.Equal([0x01, 6, .. "second"u8], written[..8]);
        Assert.Equal(ContentOpeningStatus.Opened, opened.Status);
        Assert.Equal(Destination, opened.Plaintext.ToArray());
    }

    [Fact]
    public async Task Open_ValueOfTheLibrarysProtectorAfterAHostProtectorIsRegistered_Opens()
    {
        string connection = await MigratedAsync();
        byte[] stored;
        await using (Started before = await StartAsync(connection, []))
        {
            stored = await before.ProtectAsync(Person);
        }

        await using Started after = await StartAsync(connection, [new ContentProtectorFake("vault")]);
        ContentOpening opened = await after.Protection.OpenAsync(Person, stored, Row, TestContext.Current.CancellationToken);

        Assert.Equal(Destination, opened.Plaintext.ToArray());
    }

    [Theory]
    [InlineData(0x02)]
    [InlineData(0x0F)]
    public async Task Open_FormatKeptForALaterRelease_IsHeldNamingNoKey(byte format)
    {
        await using Started started = await StartAsync();

        ContentOpening opening = await started.Protection.OpenAsync(Person, new byte[] { format, 1, 2, 3 }, Row, TestContext.Current.CancellationToken);

        Assert.Equal(new ContentOpening(ContentOpeningStatus.Held, ReadOnlyMemory<byte>.Empty, null), opening);
    }

    [Theory]
    [InlineData("")]
    [InlineData("00")]
    [InlineData("10")]
    [InlineData("FF0102")]
    [InlineData("01")]
    [InlineData("0100")]
    [InlineData("01056D6573")]
    [InlineData("0103564155010203")]
    public async Task Open_ValueInNoFormatOrAMalformedEnvelope_IsRejected(string hex)
    {
        await using Started started = await StartAsync();

        ContentOpening opening = await started.Protection.OpenAsync(Person, Convert.FromHexString(hex), Row, TestContext.Current.CancellationToken);

        Assert.Equal(ContentOpeningStatus.Rejected, opening.Status);
    }

    [Fact]
    public async Task Open_EnvelopeNamingAProtectorNotRegistered_IsHeldNamingIt()
    {
        await using Started started = await StartAsync();

        ContentOpening opening = await started.Protection.OpenAsync(Person, Convert.FromHexString("01057661756C74010203"), Row, TestContext.Current.CancellationToken);

        Assert.Equal(new ContentOpening(ContentOpeningStatus.Held, ReadOnlyMemory<byte>.Empty, "vault"), opening);
    }

    [Fact]
    public async Task Open_LibraryValueAltered_IsRejected()
    {
        await using Started started = await StartAsync();
        byte[] stored = await started.ProtectAsync(Person);
        stored[30] ^= 0x01;

        ContentOpening opening = await started.Protection.OpenAsync(Person, stored, Row, TestContext.Current.CancellationToken);

        Assert.Equal(ContentOpeningStatus.Rejected, opening.Status);
    }

    [Fact]
    public async Task Open_LibraryValueUnderAVersionNotHeld_IsHeldNamingTheContentKeys()
    {
        string connection = await MigratedAsync();
        byte[] stored;
        await using (Started rotated = await StartAsync(connection, [], Secrets.Held(connection).With("contentKeys", Secrets.Bytes(32, 1), Secrets.Bytes(32, 4)), WriteVersion("2")))
        {
            stored = await rotated.ProtectAsync(Person);
        }

        await RetireAsync(connection, 2);
        await using Started older = await StartAsync(connection, []);
        ContentOpening opening = await older.Protection.OpenAsync(Person, stored, Row, TestContext.Current.CancellationToken);

        Assert.Equal(new ContentOpening(ContentOpeningStatus.Held, ReadOnlyMemory<byte>.Empty, "contentKeys"), opening);
    }

    [Fact]
    public async Task MSG_OPS_008_AC4_NewValuesAreMadeUnderTheWriteVersionAndValuesUnderAnOlderOneStillOpen()
    {
        string connection = await MigratedAsync();
        SecretSourceFake twoVersions = Secrets.Held(connection).With("contentKeys", Secrets.Bytes(32, 1), Secrets.Bytes(32, 4));
        byte[] underOne;
        await using (Started atOne = await StartAsync(connection, [], twoVersions, WriteVersion("1")))
        {
            underOne = await atOne.ProtectAsync(Person);
        }

        await using Started atTwo = await StartAsync(connection, [], twoVersions, WriteVersion("2"));
        byte[] underTwo = await atTwo.ProtectAsync(Person);
        ContentOpening opened = await atTwo.Protection.OpenAsync(Person, underOne, Row, TestContext.Current.CancellationToken);

        Assert.Equal(1, BinaryPrimitives.ReadInt32BigEndian(underOne.AsSpan(11, 4)));
        Assert.Equal(2, BinaryPrimitives.ReadInt32BigEndian(underTwo.AsSpan(11, 4)));
        Assert.Equal(Destination, opened.Plaintext.ToArray());
    }

    [Fact]
    public async Task Protect_ContentKeyValues_AreReservedInBlocksInTheKeyTable()
    {
        string connection = await MigratedAsync();
        await using Started started = await StartAsync(connection, [], Secrets.Held(connection), configuration => configuration["Hermes:Secrets:ContentKeys:ReserveBlock"] = "100");

        await started.ProtectAsync(Person);
        long afterOne = await ValueCountAsync(connection, 1);
        for (int value = 1; value < 101; value++)
        {
            await started.ProtectAsync(Person);
        }

        Assert.Equal(100, afterOne);
        Assert.Equal(200, await ValueCountAsync(connection, 1));
    }

    [Fact]
    public async Task Protect_ReservationThatWouldPassTwoToThe32_IsRefusedAsKeyUnavailable()
    {
        string connection = await MigratedAsync();
        await using Started started = await StartAsync(connection, []);
        await SetValueCountAsync(connection, 1, 4_294_967_296L - 9_999);

        ContentProtecting refused = await started.Protection.ProtectAsync(Person, Destination, Row, TestContext.Current.CancellationToken);

        Assert.Equal(ContentProtecting.Unavailable("contentKeys", 1), refused);
        Assert.Equal(4_294_967_296L - 9_999, await ValueCountAsync(connection, 1));
    }

    private static Action<Dictionary<string, string?>> WriteVersion(string version)
    {
        return configuration => configuration["Hermes:Secrets:ContentKeys:WriteVersion"] = version;
    }

    private async Task<Started> StartAsync(params IContentProtector[] protectors)
    {
        return await StartAsync(await MigratedAsync(), protectors);
    }

    private static async Task<Started> StartAsync(string connection, IContentProtector[] protectors, SecretSourceFake? secrets = null, Action<Dictionary<string, string?>>? configure = null)
    {
        Dictionary<string, string?> configuration = TestHost.Languages();
        configure?.Invoke(configuration);
        IHost host = TestHost.Build(
            configuration,
            declare =>
            {
                declare.Category("security").Essential();
                declare.Kind("sign.in", "security").Sms();
            },
            services =>
            {
                services.AddSingleton<ISecretSource>(secrets ?? Secrets.Held(connection));
                foreach (IContentProtector protector in protectors)
                {
                    services.AddSingleton(protector);
                }
            });
        await host.StartAsync(TestContext.Current.CancellationToken);
        AsyncServiceScope scope = host.Services.CreateAsyncScope();
        return new Started(host, scope, scope.ServiceProvider.GetRequiredService<ContentProtection>());
    }

    private async Task<string> MigratedAsync()
    {
        string connection = await postgres.NewDatabaseAsync();
        Assert.Null(await SchemaMigration.ApplyAsync(connection, [], TestContext.Current.CancellationToken));
        return connection;
    }

    private static async Task<long> ValueCountAsync(string connection, int version)
    {
        await using NpgsqlConnection database = new(connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("SELECT value_count FROM messaging.key_versions WHERE key = 'contentKeys' AND version = @version", database);
        command.Parameters.AddWithValue("version", version);
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private static async Task SetValueCountAsync(string connection, int version, long count)
    {
        await using NpgsqlConnection database = new(connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("UPDATE messaging.key_versions SET value_count = @count WHERE key = 'contentKeys' AND version = @version", database);
        command.Parameters.AddWithValue("version", version);
        command.Parameters.AddWithValue("count", count);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task RetireAsync(string connection, int version)
    {
        await using NpgsqlConnection database = new(connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("UPDATE messaging.key_versions SET retired = true WHERE key = 'contentKeys' AND version = @version", database);
        command.Parameters.AddWithValue("version", version);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private sealed class Started(IHost host, AsyncServiceScope scope, ContentProtection protection) : IAsyncDisposable
    {
        internal ContentProtection Protection => protection;

        /// <summary>Protects the destination for <paramref name="person"/> in the test's row, which must succeed.</summary>
        internal async Task<byte[]> ProtectAsync(string? person)
        {
            ContentProtecting made = await protection.ProtectAsync(person, Destination, Row, TestContext.Current.CancellationToken);
            Assert.NotNull(made.Stored);
            return made.Stored;
        }

        public async ValueTask DisposeAsync()
        {
            await scope.DisposeAsync();
            await host.StopAsync(TestContext.Current.CancellationToken);
            host.Dispose();
        }
    }
}