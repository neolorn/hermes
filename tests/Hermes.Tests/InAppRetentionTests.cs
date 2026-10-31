using System;
using System.Linq;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Sending;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "integration")]
public sealed class InAppRetentionTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task MSG_INAPP_005_AC1_AnEntry181DaysOldIsGoneAndOne179DaysOldIsListed()
    {
        await using DispatchEngine engine = await DispatchEngine.StartAsync(postgres, declare: InApp.Declare, templates: InApp.Templates);
        SendReceipt old = (await engine.Host.SendAsync(InApp.Send(order: "A-1"))).Value!;
        await engine.Host.DispatchAsync();
        engine.Host.Clock.Advance(TimeSpan.FromDays(2));
        await engine.Host.SendAsync(InApp.Send(order: "A-2"));
        await engine.Host.DispatchAsync();

        engine.Host.Clock.Advance(TimeSpan.FromDays(179));
        Assert.True(await engine.Host.Services.GetRequiredService<RetentionJob>().RunAsync(TestContext.Current.CancellationToken));

        InAppEntry listed = Assert.Single((await InAppListTests.ListAsync(engine.Host, InApp.Person)).Value!.Entries);
        Assert.Equal("Order A-2 en en", listed.Title);
        Assert.Equal(1, await InApp.EntriesAsync(engine.Host));
        DeliveryRecord record = Assert.Single((await engine.Host.ReadAsync(old.Send)).Deliveries);
        Assert.Equal(DeliveryStatus.Sent, record.Status);
        Assert.Null(await engine.Host.ColumnAsync<string>(old.Deliveries[0].Delivery.Value, "parts"));
    }

    [Fact]
    public async Task MSG_SEND_019_AC1_AFinalInAppDeliveryThatKeptItsPartsLosesThem31DaysPastItsFinalStatus()
    {
        await using DispatchEngine engine = await DispatchEngine.StartAsync(postgres, declare: InApp.Declare, templates: InApp.Templates);
        SendReceipt receipt = (await engine.Host.SendAsync(InApp.Send())).Value!;
        Guid delivery = receipt.Deliveries[0].Delivery.Value;
        Assert.True((await engine.Host.WithMessagesAsync(messages => messages.CancelAsync(receipt.Send, TestContext.Current.CancellationToken).AsTask())).IsSuccess);
        bool held = await engine.Host.ColumnAsync<string>(delivery, "parts") is not null;

        engine.Host.Clock.Advance(TimeSpan.FromDays(31));
        Assert.True(await engine.Host.Services.GetRequiredService<RetentionJob>().RunAsync(TestContext.Current.CancellationToken));

        Assert.True(held);
        Assert.Null(await engine.Host.ColumnAsync<string>(delivery, "parts"));
        Assert.Equal(DeliveryStatus.Cancelled, (await engine.Host.ReadAsync(receipt.Send)).Deliveries.Single().Status);
    }
}