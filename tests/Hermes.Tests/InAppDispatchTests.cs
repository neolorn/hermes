using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Sending;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "integration")]
public sealed class InAppDispatchTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task MSG_INAPP_001_AC1_ADispatchedInAppDeliveryLeavesOneEntryInItsPersonsListEndsSentAndHoldsNoRenderedPartOfItsOwn()
    {
        await using DispatchEngine engine = await StartAsync();
        SendReceipt receipt = (await engine.Host.SendAsync(InApp.Send(language: "ar"))).Value!;
        Guid delivery = receipt.Deliveries[0].Delivery.Value;

        Assert.Equal(1, await engine.Host.DispatchAsync());

        DeliveryRecord record = Assert.Single((await engine.Host.ReadAsync(receipt.Send)).Deliveries);
        Assert.Equal(DeliveryStatus.Sent, record.Status);
        Assert.Equal(AttemptClass.Accepted, Assert.Single(record.Attempts).AnswerClass);
        Assert.Null(await engine.Host.ColumnAsync<string>(delivery, "parts"));
        Assert.Equal(1, await InApp.EntriesAsync(engine.Host, InApp.Person));
        (Guid id, string language, string? target, Guid? from) = await EntryAsync(engine.Host);
        Assert.Equal(id.ToString("D"), await engine.Host.ColumnAsync<string>(delivery, "provider_reference"));
        Assert.Equal("ar", language);
        Assert.Equal(InApp.Target, target);
        Assert.Equal(delivery, from);
        Dictionary<string, string> opened = await OpenEntryAsync(engine.Host, id);
        Assert.Equal("Order A-1001 en en", opened["title.en"]);
        Assert.Equal("Body ar rtl", opened["body.ar"]);
        Assert.Equal("""{"orderId":{"text":"A-1001"}}""", opened["target"]);
    }

    [Fact]
    public async Task MSG_SEND_007_AC1_AnInAppPassTakesNoPlaceInARateCounter()
    {
        await using DispatchEngine engine = await StartAsync(configure: configuration => configuration["Hermes:Dispatch:BatchSize"] = "2");
        for (int index = 0; index < 3; index++)
        {
            await engine.Host.SendAsync(InApp.Send(order: $"A-{index}"));
        }

        Assert.Equal(2, await engine.Host.DispatchAsync());
        Assert.Equal(1, await engine.Host.DispatchAsync());

        Assert.Equal(3, await InApp.EntriesAsync(engine.Host));
        Assert.Equal(0L, await CountersAsync(engine.Host));
    }

    [Fact]
    public async Task MSG_SEND_010_AC1_AnInAppDeliveryOfACategoryThePersonTurnedOffOnInappEndsOptedOut()
    {
        await using DispatchEngine engine = await StartAsync();
        await SetAsync(engine.Host, Channel.InApp, isOn: false);
        SendReceipt receipt = (await engine.Host.SendAsync(InApp.Send())).Value!;

        await engine.Host.DispatchAsync();

        Assert.Equal(DeliveryStatus.OptedOut, Assert.Single((await engine.Host.ReadAsync(receipt.Send)).Deliveries).Status);
        Assert.Equal(0, await InApp.EntriesAsync(engine.Host));
    }

    [Fact]
    public async Task MSG_INAPP_004_AC1_WithTheKeyOnAnInAppDeliveryOfACategoryThePersonTurnedOffIsWrittenAndTheirSmsIsNotSent()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        await using (DispatchEngine before = await StartAsync(connection: connection))
        {
            await SetAsync(before.Host, Channel.InApp, isOn: false);
            await SetAsync(before.Host, Channel.Sms, isOn: false);
        }

        await using DispatchEngine engine = await StartAsync(connection: connection, configure: configuration => configuration["Hermes:InApp:AlwaysOn"] = "true");
        SendReceipt receipt = (await engine.Host.SendAsync(InApp.Send(channels: [Channel.Sms, Channel.InApp]))).Value!;

        await engine.Host.DispatchAsync();

        IReadOnlyList<DeliveryRecord> deliveries = (await engine.Host.ReadAsync(receipt.Send)).Deliveries;
        Assert.Equal(DeliveryStatus.OptedOut, deliveries.Single(delivery => delivery.Channel == Channel.Sms).Status);
        Assert.Equal(DeliveryStatus.Sent, deliveries.Single(delivery => delivery.Channel == Channel.InApp).Status);
        Assert.Equal(1, await InApp.EntriesAsync(engine.Host, InApp.Person));
        Assert.Empty(engine.Sms.Received);
    }

    [Fact]
    public async Task MSG_INAPP_004_AC2_WithTheKeyOnAnInAppDeliveryOfACategoryRequiringConsentToAPersonWhoDoesNotConsentEndsNoConsent()
    {
        ConsentSourceFake consent = new() { Answer = (_, _) => Task.FromResult(ConsentAnswer.NotGiven) };
        await using DispatchEngine engine = await StartAsync(
            configure: configuration => configuration["Hermes:InApp:AlwaysOn"] = "true",
            declare: declare =>
            {
                declare.Category(InApp.Category).RequiresConsent("marketing");
                declare.Kind(InApp.Kind, InApp.Category).Sms().InApp(InApp.Target, ["orderId"]);
            },
            services: services => services.AddSingleton<IConsentSource>(consent));
        SendReceipt receipt = (await engine.Host.SendAsync(InApp.Send())).Value!;

        await engine.Host.DispatchAsync();

        Assert.Equal(DeliveryStatus.NoConsent, Assert.Single((await engine.Host.ReadAsync(receipt.Send)).Deliveries).Status);
        ConsentQuery query = Assert.Single(consent.Queries);
        Assert.Equal(Channel.InApp, query.Channel);
        Assert.Null(query.Destination);
        Assert.Equal(0, await InApp.EntriesAsync(engine.Host));
    }

    [Fact]
    public async Task MSG_SEND_013_AC1_QuietHoursNeverHoldAnInAppDelivery()
    {
        await using DispatchEngine engine = await StartAsync(
            configure: configuration =>
            {
                configuration["Hermes:QuietHours:Start"] = "00:00";
                configuration["Hermes:QuietHours:End"] = "23:59";
            },
            declare: declare =>
            {
                declare.Category(InApp.Category).QuietHours(true);
                declare.Kind(InApp.Kind, InApp.Category).Sms().InApp(InApp.Target, ["orderId"]);
            });
        SendReceipt receipt = (await engine.Host.SendAsync(InApp.Send(channels: [Channel.Sms, Channel.InApp]))).Value!;

        await engine.Host.DispatchAsync();

        IReadOnlyList<DeliveryRecord> deliveries = (await engine.Host.ReadAsync(receipt.Send)).Deliveries;
        Assert.Equal(DeliveryStatus.Waiting, deliveries.Single(delivery => delivery.Channel == Channel.Sms).Status);
        Assert.Equal(DeliveryStatus.Sent, deliveries.Single(delivery => delivery.Channel == Channel.InApp).Status);
    }

    [Fact]
    public async Task MSG_SEND_014_AC1_AnInAppLimitCountsPerPerson()
    {
        await using DispatchEngine engine = await StartAsync(declare: declare =>
        {
            declare.Category(InApp.Category).Limit(Channel.InApp, 1, TimeSpan.FromHours(1));
            declare.Kind(InApp.Kind, InApp.Category).Sms().InApp(InApp.Target, ["orderId"]);
        });
        SendReceipt first = (await engine.Host.SendAsync(InApp.Send(order: "A-1"))).Value!;
        await engine.Host.DispatchAsync();
        SendReceipt second = (await engine.Host.SendAsync(InApp.Send(order: "A-2"))).Value!;
        SendReceipt other = (await engine.Host.SendAsync(InApp.Send(person: "person-8", order: "A-3"))).Value!;

        await engine.Host.DispatchAsync();

        Assert.Equal(DeliveryStatus.Sent, (await engine.Host.ReadAsync(first.Send)).Deliveries[0].Status);
        Assert.Equal(DeliveryStatus.Limited, (await engine.Host.ReadAsync(second.Send)).Deliveries[0].Status);
        Assert.Equal(DeliveryStatus.Sent, (await engine.Host.ReadAsync(other.Send)).Deliveries[0].Status);
    }

    [Fact]
    public async Task MSG_SEND_017_AC1_TheObserverIsToldOfAnInAppDeliverysMoveToSent()
    {
        DeliveryObserverFake observer = new();
        await using DispatchEngine engine = await StartAsync(
            configure: configuration => configuration["Hermes:Observer:Enabled"] = "true",
            services: services => services.AddSingleton<IDeliveryObserver>(observer));
        SendReceipt receipt = (await engine.Host.SendAsync(InApp.Send())).Value!;

        await engine.Host.DispatchAsync();

        Assert.Equal(1L, await NoticesAsync(engine.Host, receipt.Deliveries[0].Delivery.Value, "sent"));
    }

    internal static async Task<(Guid Id, string Language, string? Target, Guid? Delivery)> EntryAsync(EngineHost host)
    {
        await using NpgsqlConnection database = new(host.Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("SELECT id, language, target, delivery_id FROM messaging.entries", database);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        string? target = await reader.IsDBNullAsync(2, TestContext.Current.CancellationToken) ? null : reader.GetString(2);
        Guid? delivery = await reader.IsDBNullAsync(3, TestContext.Current.CancellationToken) ? null : reader.GetGuid(3);
        return (reader.GetGuid(0), reader.GetString(1), target, delivery);
    }

    /// <summary>An entry's stored values opened under the entry's own row, by field.</summary>
    internal static async Task<Dictionary<string, string>> OpenEntryAsync(EngineHost host, Guid entry)
    {
        string parts;
        string person;
        await using (NpgsqlConnection database = new(host.Connection))
        {
            await database.OpenAsync(TestContext.Current.CancellationToken);
            await using NpgsqlCommand command = new("SELECT parts::text, person FROM messaging.entries WHERE id = @id", database);
            command.Parameters.AddWithValue("id", entry);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
            Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
            parts = reader.GetString(0);
            person = reader.GetString(1);
        }

        Dictionary<string, string> opened = new(StringComparer.Ordinal);
        AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using (scope)
        {
            ContentProtection protection = scope.ServiceProvider.GetRequiredService<ContentProtection>();
            foreach ((string field, byte[] stored) in InAppParts.Read(parts)!)
            {
                ContentOpening opening = await protection.OpenAsync(person, stored, ContentContext.Of(ContentContext.Entries, entry, field), TestContext.Current.CancellationToken);
                Assert.Equal(ContentOpeningStatus.Opened, opening.Status);
                opened[field] = Encoding.UTF8.GetString(opening.Plaintext.Span);
            }
        }

        return opened;
    }

    internal static async Task SetAsync(EngineHost host, Channel channel, bool isOn, string person = InApp.Person)
    {
        AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using (scope)
        {
            Result result = await scope.ServiceProvider.GetRequiredService<IPreferences>().SetAsync(person, InApp.Category, channel, isOn, TestContext.Current.CancellationToken);
            Assert.True(result.IsSuccess, result.Failure?.Code);
        }
    }

    internal static async Task<long> NoticesAsync(EngineHost host, Guid delivery, string status)
    {
        await using NpgsqlConnection database = new(host.Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("SELECT count(*) FROM messaging.notices WHERE delivery_id = @delivery AND status = @status", database);
        command.Parameters.AddWithValue("delivery", delivery);
        command.Parameters.AddWithValue("status", status);
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private static async Task<long> CountersAsync(EngineHost host)
    {
        await using NpgsqlConnection database = new(host.Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("SELECT count(*) FROM messaging.rate_counters WHERE channel = 'inapp'", database);
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private Task<DispatchEngine> StartAsync(
        Action<Dictionary<string, string?>>? configure = null,
        Action<DeclarationBuilder>? declare = null,
        Action<IServiceCollection>? services = null,
        string? connection = null)
    {
        return DispatchEngine.StartAsync(postgres, configure, declare ?? InApp.Declare, InApp.Templates, services, connection);
    }
}