using System;
using System.Linq;
using System.Threading.Tasks;

using Hermes.Core;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "integration")]
public sealed class InAppSubjectTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task MSG_INAPP_005_AC2_AfterErasureThePersonsListIsEmptyAndTheirEarlierExportHeldEachEntry()
    {
        await using DispatchEngine engine = await DispatchEngine.StartAsync(postgres, declare: InApp.Declare, templates: InApp.Templates);
        await engine.Host.SendAsync(InApp.Send(language: "ar"));
        await engine.Host.SendAsync(InApp.Send(person: "person-8"));
        await engine.Host.DispatchAsync();
        SendReceipt waiting = (await engine.Host.SendAsync(InApp.Send(order: "A-1002"))).Value!;
        Assert.Equal(1, await InAppListTests.UseAsync(engine.Host, list => list.MarkAllReadAsync(InApp.Person, TestContext.Current.CancellationToken).AsTask()));

        SubjectExport export = (await SubjectTests.ExportAsync(engine.Host, InApp.Person)).Value!;
        Result erased = await SubjectTests.EraseAsync(engine.Host, InApp.Person);

        InAppEntryExport entry = Assert.Single(export.Entries);
        Assert.Equal((InApp.Kind, InApp.Category, InApp.Target, false, false), (entry.Kind, entry.Category, entry.Target, entry.Erased, entry.Rejected));
        Assert.NotNull(entry.ReadAt);
        Assert.Equal(["body.ar", "body.en", "title.ar", "title.en"], entry.Content.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("Order A-1001 ar ar", entry.Content["title.ar"]);
        Assert.Equal(new SendValue.PlainText("A-1001"), entry.TargetValues["orderId"]);
        DeliveryExport pending = Assert.Single(export.Deliveries, delivery => delivery.Status == DeliveryStatus.Waiting);
        Assert.Equal("Order A-1002 en en", pending.Content["title.en"]);
        Assert.True(erased.IsSuccess);
        Assert.Empty((await InAppListTests.ListAsync(engine.Host, InApp.Person)).Value!.Entries);
        Assert.Equal(0, await InApp.EntriesAsync(engine.Host, InApp.Person));
        Assert.Equal(1, await InApp.EntriesAsync(engine.Host));
        Assert.Null(await engine.Host.ColumnAsync<string>(waiting.Deliveries[0].Delivery.Value, "parts"));
        Assert.Equal(DeliveryStatus.Cancelled, Assert.Single((await engine.Host.ReadAsync(waiting.Send)).Deliveries).Status);
    }
}