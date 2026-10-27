using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Sending;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "integration")]
public sealed class PreferenceTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Person = "person-1";

    [Fact]
    public async Task MSG_SEND_006_AC2_APreferenceTurnedOffBetweenTheSendAndItsDispatchStopsTheDelivery()
    {
        await using DispatchEngine engine = await DispatchEngine.StartAsync(postgres, declare: SuppressionTests.Declare);
        SendReceipt receipt = (await engine.Host.SendAsync(SuppressionTests.OfferTo("+201000000001"))).Value!;
        SendReceipt other = (await engine.Host.SendAsync(SuppressionTests.OfferTo("+201000000002") with { Recipient = new Recipient.Person("person-2") })).Value!;

        Assert.True((await SetAsync(engine.Host, Person, SuppressionTests.Offers, Channel.Sms, isOn: false)).IsSuccess);
        await engine.Host.DispatchAsync();

        DeliveryRecord delivery = Assert.Single((await engine.Host.ReadAsync(receipt.Send)).Deliveries);
        Assert.Equal(DeliveryStatus.OptedOut, delivery.Status);
        Assert.Empty(delivery.Attempts);
        Assert.Equal(DeliveryStatus.Sent, await SuppressionTests.StatusAsync(engine.Host, other));
        Assert.Equal("+201000000002", Assert.Single(engine.Sms.Received).ToNumber);
    }

    [Fact]
    public async Task Set_ACategoryTurnedBackOn_LetsTheNextDeliveryGoOut()
    {
        await using DispatchEngine engine = await DispatchEngine.StartAsync(postgres, declare: SuppressionTests.Declare);
        Assert.True((await SetAsync(engine.Host, Person, SuppressionTests.Offers, Channel.Sms, isOn: false)).IsSuccess);
        Assert.True((await SetAsync(engine.Host, Person, SuppressionTests.Offers, Channel.Sms, isOn: true)).IsSuccess);

        SendReceipt receipt = (await engine.Host.SendAsync(SuppressionTests.OfferTo("+201000000001"))).Value!;
        await engine.Host.DispatchAsync();

        Assert.Equal(DeliveryStatus.Sent, await SuppressionTests.StatusAsync(engine.Host, receipt));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MSG_SEND_010_AC2_SettingAPreferenceOfAnEssentialCategoryIsRefusedAndChangesNothing(bool isOn)
    {
        await using DispatchEngine engine = await DispatchEngine.StartAsync(postgres, declare: SuppressionTests.Declare);

        Result result = await SetAsync(engine.Host, Person, EngineHost.Category, Channel.Sms, isOn);

        Assert.Equal("messaging.preference.essential", result.Failure!.Code);
        Assert.Equal(EngineHost.Category, result.Failure.Details["category"]);
        Assert.Equal(0, await CountAsync(engine.Host));
        SendReceipt receipt = (await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]))).Value!;
        await engine.Host.DispatchAsync();
        Assert.Equal(DeliveryStatus.Sent, await SuppressionTests.StatusAsync(engine.Host, receipt));
    }

    [Theory]
    [InlineData("unknown", "sms")]
    [InlineData(SuppressionTests.Offers, "email")]
    [InlineData(SuppressionTests.Offers, "push")]
    public async Task Set_ACategoryNotDeclaredOrAChannelItsKindsDoNotUse_IsRefusedUnknown(string category, string channel)
    {
        await using DispatchEngine engine = await DispatchEngine.StartAsync(postgres, declare: SuppressionTests.Declare);

        Result result = await SetAsync(engine.Host, Person, category, Spellings.Channel(channel)!.Value, isOn: false);

        Assert.Equal("messaging.preference.unknown", result.Failure!.Code);
        Assert.Equal((category, channel), (result.Failure.Details["category"], result.Failure.Details["channel"]));
        Assert.Equal(0, await CountAsync(engine.Host));
    }

    [Fact]
    public async Task Read_EveryDeclaredCategoryInDeclarationOrder_WithItsChannelsOnUnlessTurnedOff()
    {
        await using DispatchEngine engine = await DispatchEngine.StartAsync(postgres, declare: SuppressionTests.Declare);
        Assert.True((await SetAsync(engine.Host, Person, SuppressionTests.Offers, Channel.Sms, isOn: false)).IsSuccess);

        IReadOnlyList<CategoryPreferences> read = await WithPreferencesAsync(engine.Host, preferences => preferences.ReadAsync(Person, TestContext.Current.CancellationToken).AsTask());
        IReadOnlyList<CategoryPreferences> other = await WithPreferencesAsync(engine.Host, preferences => preferences.ReadAsync("person-2", TestContext.Current.CancellationToken).AsTask());

        Assert.Equal([EngineHost.Category, SuppressionTests.Offers], read.Select(category => category.Category));
        Assert.Equal([true, false], read.Select(category => category.Essential));
        Assert.Equal([(Channel.Email, true), (Channel.Sms, true), (Channel.Push, true)], read[0].Channels.Select(channel => (channel.Channel, channel.On)));
        Assert.Equal([(Channel.Sms, false)], read[1].Channels.Select(channel => (channel.Channel, channel.On)));
        Assert.Equal([(Channel.Sms, true)], other[1].Channels.Select(channel => (channel.Channel, channel.On)));
    }

    [Fact]
    public async Task Set_ThePersonsLocksNotHadWithinTheDispatchTimeout_LetsTheDatabasesErrorThrough()
    {
        await using DispatchEngine engine = await DispatchEngine.StartAsync(
            postgres,
            configure: configuration => configuration["Hermes:Dispatch:Timeout"] = "00:00:01",
            declare: SuppressionTests.Declare);
        KeyedHashes hashes = engine.Host.Services.GetRequiredService<KeyedHashes>();
        await using NpgsqlConnection holder = new(engine.Host.Connection);
        await holder.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlTransaction transaction = await holder.BeginTransactionAsync(TestContext.Current.CancellationToken);
        foreach (long key in AdvisoryLocks.Person(hashes, Person))
        {
            await using NpgsqlCommand command = new("SELECT pg_advisory_xact_lock(@key)", holder, transaction);
            command.Parameters.AddWithValue("key", key);
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        PostgresException failure = await Assert.ThrowsAsync<PostgresException>(() => SetAsync(engine.Host, Person, SuppressionTests.Offers, Channel.Sms, isOn: false));

        Assert.Equal("55P03", failure.SqlState);
        Assert.Equal(0, await CountAsync(engine.Host));
    }

    internal static Task<Result> SetAsync(EngineHost host, string person, string category, Channel channel, bool isOn)
    {
        return WithPreferencesAsync(host, preferences => preferences.SetAsync(person, category, channel, isOn, TestContext.Current.CancellationToken).AsTask());
    }

    private static async Task<T> WithPreferencesAsync<T>(EngineHost host, Func<IPreferences, Task<T>> use)
    {
        AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using (scope)
        {
            return await use(scope.ServiceProvider.GetRequiredService<IPreferences>());
        }
    }

    private static async Task<long> CountAsync(EngineHost host)
    {
        await using NpgsqlConnection database = new(host.Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("SELECT count(*) FROM messaging.preferences", database);
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }
}