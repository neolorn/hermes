using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "integration")]
public sealed class QuietHoursTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Cairo = "Africa/Cairo";

    [Fact]
    public async Task MSG_SEND_013_AC1_ADeliveryOfACategoryWithQuietHoursCheckedAt2330InCairoIsDispatchedAfter0900There()
    {
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(Cairo);
        ManualClock clock = new(Local(zone, 23, 30));
        await using DispatchEngine engine = await StartAsync(clock);
        SendReceipt receipt = (await engine.Host.SendAsync(ConsentTests.PromotionTo("+201000000001") with { TimeZone = Cairo })).Value!;
        Guid id = receipt.Deliveries[0].Delivery.Value;

        await engine.Host.DispatchAsync();
        DateTimeOffset due = await engine.Host.ColumnAsync<DateTime>(id, "due_at");
        clock.Advance(due - clock.GetUtcNow() - TimeSpan.FromSeconds(1));
        await engine.Host.DispatchAsync();
        DeliveryStatus before = await SuppressionTests.StatusAsync(engine.Host, receipt);
        clock.Advance(TimeSpan.FromSeconds(1));
        await engine.Host.DispatchAsync();

        DateTimeOffset nine = Local(zone, 9, 0).AddDays(1);
        Assert.InRange(due, nine, nine + TimeSpan.FromMinutes(30));
        Assert.Equal(DeliveryStatus.Waiting, before);
        Assert.Equal(DeliveryStatus.Sent, await SuppressionTests.StatusAsync(engine.Host, receipt));
        Assert.Single(engine.Sms.Received);
        Assert.Single(Assert.Single((await engine.Host.ReadAsync(receipt.Send)).Deliveries).Attempts);
    }

    [Fact]
    public async Task MSG_EMAIL_003_AC2_AnEmailHeldByQuietHoursIsSubmittedAtTheInstantTheyEndWhichDatesIt()
    {
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(Cairo);
        ManualClock clock = new(Local(zone, 23, 30));
        await using DispatchEngine engine = await DispatchEngine.StartAsync(
            postgres,
            configure: UnsubscribeLinkTests.Configure,
            declare: declare =>
            {
                EngineHost.Essential(declare);
                declare.Category(UnsubscribeLinkTests.News).Label("en", "News").Label("ar", "News").QuietHours(true);
                declare.Kind(UnsubscribeLinkTests.Letter, UnsubscribeLinkTests.News).Email();
            },
            templates: UnsubscribeLinkTests.Templates,
            clock: clock);
        List<DateTimeOffset> submitted = [];
        engine.Email.Before = (_, _) =>
        {
            submitted.Add(clock.GetUtcNow());
            return Task.CompletedTask;
        };
        SendReceipt receipt = (await engine.Host.SendAsync(UnsubscribeLinkTests.LetterTo("reader@example.com") with { TimeZone = Cairo })).Value!;

        await engine.Host.DispatchAsync();
        DateTimeOffset due = await engine.Host.ColumnAsync<DateTime>(receipt.Deliveries[0].Delivery.Value, "due_at");
        clock.Advance(due - clock.GetUtcNow());
        await engine.Host.DispatchAsync();

        Assert.Equal(DeliveryStatus.Sent, await SuppressionTests.StatusAsync(engine.Host, receipt));
        Assert.Equal(due, Assert.Single(submitted));
        Assert.InRange(due, Local(zone, 9, 0).AddDays(1), Local(zone, 9, 30).AddDays(1));
    }

    [Fact]
    public async Task MSG_SEND_013_AC2_AnEssentialDeliveryCheckedAt2330IsDispatchedAtOnce()
    {
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(Cairo);
        await using DispatchEngine engine = await StartAsync(new ManualClock(Local(zone, 23, 30)));
        SendReceipt receipt = (await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Sms]) with { TimeZone = Cairo })).Value!;

        await engine.Host.DispatchAsync();

        Assert.Equal(DeliveryStatus.Sent, await SuppressionTests.StatusAsync(engine.Host, receipt));
    }

    [Fact]
    public async Task MSG_SEND_013_AC3_ADeliveryHeldForQuietHoursGivesItsPlaceInTheRateBackAndItsConsentSourceIsNotAsked()
    {
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(Cairo);
        ConsentSourceFake consent = new();
        await using DispatchEngine engine = await StartAsync(new ManualClock(Local(zone, 23, 30)), consent);
        SendReceipt receipt = (await engine.Host.SendAsync(ConsentTests.PromotionTo("+201000000001") with { TimeZone = Cairo })).Value!;

        Assert.Equal(1, await engine.Host.DispatchAsync());

        DeliveryRecord delivery = Assert.Single((await engine.Host.ReadAsync(receipt.Send)).Deliveries);
        Assert.Equal(DeliveryStatus.Waiting, delivery.Status);
        Assert.Empty(delivery.Attempts);
        Assert.Empty(consent.Queries);
        Assert.Equal(0L, await PlacesAsync(engine.Host));
    }

    [Fact]
    public async Task MSG_OPS_001_AC2_AQuietHoursStartChangedWhileRunningAppliesToTheNextDeliveryChecked()
    {
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(Cairo);
        await using DispatchEngine engine = await StartAsync(new ManualClock(Local(zone, 23, 30)));
        SendReceipt held = (await engine.Host.SendAsync(ConsentTests.PromotionTo("+201000000001") with { TimeZone = Cairo })).Value!;
        await engine.Host.DispatchAsync();
        IConfigurationRoot configuration = (IConfigurationRoot)engine.Host.Services.GetRequiredService<IConfiguration>();

        configuration["Hermes:QuietHours:Start"] = "23:45";
        configuration.Reload();
        SendReceipt next = (await engine.Host.SendAsync(ConsentTests.PromotionTo("+201000000002") with { TimeZone = Cairo, Recipient = new Recipient.Person("person-2") })).Value!;
        await engine.Host.DispatchAsync();

        Assert.Equal(DeliveryStatus.Waiting, await SuppressionTests.StatusAsync(engine.Host, held));
        Assert.Equal(DeliveryStatus.Sent, await SuppressionTests.StatusAsync(engine.Host, next));
        Assert.Single(engine.Sms.Received);
    }

    [Fact]
    public async Task QuietHours_StartEqualToEnd_TurnsThemOff()
    {
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(Cairo);
        await using DispatchEngine engine = await StartAsync(new ManualClock(Local(zone, 23, 30)), configure: "21:00");
        SendReceipt receipt = (await engine.Host.SendAsync(ConsentTests.PromotionTo("+201000000001") with { TimeZone = Cairo })).Value!;

        await engine.Host.DispatchAsync();

        Assert.Equal(DeliveryStatus.Sent, await SuppressionTests.StatusAsync(engine.Host, receipt));
    }

    /// <summary>The instant of <paramref name="hour"/>:<paramref name="minute"/> in <paramref name="zone"/> on the day <see cref="EngineHost.Start"/> falls in there.</summary>
    private static DateTimeOffset Local(TimeZoneInfo zone, int hour, int minute)
    {
        DateTime day = TimeZoneInfo.ConvertTime(EngineHost.Start, zone).Date;
        DateTime wall = day.AddHours(hour).AddMinutes(minute);
        return new DateTimeOffset(wall, zone.GetUtcOffset(wall)).ToUniversalTime();
    }

    private Task<DispatchEngine> StartAsync(ManualClock clock, ConsentSourceFake? consent = null, string? configure = null)
    {
        return DispatchEngine.StartAsync(
            postgres,
            configure: configuration =>
            {
                if (configure is not null)
                {
                    configuration["Hermes:QuietHours:End"] = configure;
                }
            },
            declare: declare => ConsentTests.Declare(declare, quietHours: true),
            services: services => services.AddSingleton<IConsentSource>(consent ?? new ConsentSourceFake()),
            clock: clock);
    }

    private static async Task<long> PlacesAsync(EngineHost host)
    {
        await using NpgsqlConnection database = new(host.Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("SELECT coalesce(sum(count), 0) FROM messaging.rate_counters", database);
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }
}