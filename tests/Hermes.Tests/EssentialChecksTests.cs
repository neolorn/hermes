using System;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Xunit;

namespace Hermes.Tests;

/// <summary>An essential category's delivery through the checks that only an optional one meets (<c>01</c> MSG-SEND-006).</summary>
[Trait("Category", "integration")]
public sealed class EssentialChecksTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Cairo = "Africa/Cairo";

    [Fact]
    public async Task MSG_SEND_006_AC3_AnEssentialDeliveryPassesTheSuppressionPreferenceQuietHoursAndConsentChecksWhateverTheyHold()
    {
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(Cairo);
        DateTime day = TimeZoneInfo.ConvertTime(EngineHost.Start, zone).Date.AddHours(23).AddMinutes(30);
        ManualClock clock = new(new DateTimeOffset(day, zone.GetUtcOffset(day)).ToUniversalTime());
        string connection = await EngineHost.DatabaseAsync(postgres);
        await using (DispatchEngine optional = await DispatchEngine.StartAsync(
            postgres,
            declare: declare =>
            {
                declare.Category(EngineHost.Category);
                declare.Kind(EngineHost.Kind, EngineHost.Category).Sms();
            },
            connection: connection,
            clock: clock))
        {
            Assert.True((await SuppressionTests.AddAsync(optional.Host, Channel.Sms, SendTests.Phone, EngineHost.Category)).IsSuccess);
            await using NpgsqlConnection database = new(connection);
            await database.OpenAsync(TestContext.Current.CancellationToken);
            await using NpgsqlCommand command = new("INSERT INTO messaging.preferences (person, category, channel, \"on\", set_at) VALUES (@person, @category, 'sms', false, @now)", database);
            command.Parameters.AddWithValue("person", SendTests.Person);
            command.Parameters.AddWithValue("category", EngineHost.Category);
            command.Parameters.AddWithValue("now", clock.GetUtcNow());
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        ConsentSourceFake consent = new() { Answer = (_, _) => Task.FromResult(ConsentAnswer.NotGiven) };
        await using DispatchEngine engine = await DispatchEngine.StartAsync(
            postgres,
            declare: declare => ConsentTests.Declare(declare, quietHours: false),
            services: services => services.AddSingleton<IConsentSource>(consent),
            connection: connection,
            clock: clock);
        SendReceipt receipt = (await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]) with { TimeZone = Cairo })).Value!;

        await engine.Host.DispatchAsync();

        Assert.Equal(DeliveryStatus.Sent, await SuppressionTests.StatusAsync(engine.Host, receipt));
        Assert.Single(engine.Sms.Received);
        Assert.Empty(consent.Queries);
    }
}