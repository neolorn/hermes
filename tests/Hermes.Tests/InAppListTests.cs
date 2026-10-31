using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "integration")]
public sealed class InAppListTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task MSG_INAPP_003_AC1_TwentyFiveEntriesReadWithSizeTwentyAnswerTheNewestTwentyAndACursorWhosePageAnswersTheOtherFiveAndNoCursor()
    {
        await using DispatchEngine engine = await StartAsync();
        for (int index = 0; index < 25; index++)
        {
            await engine.Host.SendAsync(InApp.Send(order: $"A-{index:D2}"));
            Assert.Equal(1, await engine.Host.DispatchAsync());
            engine.Host.Clock.Advance(TimeSpan.FromSeconds(1));
        }

        InAppPage first = (await ListAsync(engine.Host, InApp.Person, size: 20)).Value!;
        InAppPage second = (await ListAsync(engine.Host, InApp.Person, cursor: first.Next, size: 20)).Value!;

        Assert.Equal(Enumerable.Range(5, 20).Reverse().Select(index => $"Order A-{index:D2} en en"), first.Entries.Select(entry => entry.Title));
        Assert.NotNull(first.Next);
        Assert.Equal(Enumerable.Range(0, 5).Reverse().Select(index => $"Order A-{index:D2} en en"), second.Entries.Select(entry => entry.Title));
        Assert.Null(second.Next);
        Assert.Equal(20, (await ListAsync(engine.Host, InApp.Person)).Value!.Entries.Count);
    }

    [Fact]
    public async Task MSG_INAPP_002_AC1_AnEntrySentInArabicReadAskingEnAnswersItsEnglishPartsAndAskingAnUndeclaredLanguageItsDefaultLanguagesParts()
    {
        await using DispatchEngine engine = await StartAsync();
        await engine.Host.SendAsync(InApp.Send(language: "ar"));
        await engine.Host.DispatchAsync();

        InAppEntry english = Assert.Single((await ListAsync(engine.Host, InApp.Person, language: "en")).Value!.Entries);
        InAppEntry other = Assert.Single((await ListAsync(engine.Host, InApp.Person, language: "fr")).Value!.Entries);
        InAppEntry none = Assert.Single((await ListAsync(engine.Host, InApp.Person)).Value!.Entries);
        InAppEntry arabic = Assert.Single((await ListAsync(engine.Host, InApp.Person, language: "AR-eg")).Value!.Entries);

        Assert.Equal(("en", "Order A-1001 en en", "Body en ltr"), (english.Language, english.Title, english.Body));
        Assert.Equal(("en", "Order A-1001 en en"), (other.Language, other.Title));
        Assert.Equal("en", none.Language);
        Assert.Equal(("ar", "Order A-1001 ar ar", "Body ar rtl"), (arabic.Language, arabic.Title, arabic.Body));
        Assert.Equal((InApp.Kind, InApp.Category, InApp.Target, (DateTimeOffset?)null), (english.Kind, english.Category, english.Target, english.ReadAt));
        Assert.Equal(new SendValue.PlainText("A-1001"), Assert.Single(english.TargetValues, value => value.Key == "orderId").Value);
    }

    [Fact]
    public async Task MSG_INAPP_003_AC2_MarkingAnEntryReadLowersTheUnreadCountByOneAndTellsTheObserverMarkingItAgainChangesNothing()
    {
        await using DispatchEngine engine = await StartAsync(
            configure: configuration => configuration["Hermes:Observer:Enabled"] = "true",
            services: services => services.AddSingleton<IDeliveryObserver>(new DeliveryObserverFake()));
        SendReceipt receipt = (await engine.Host.SendAsync(InApp.Send())).Value!;
        await engine.Host.SendAsync(InApp.Send(order: "A-1002"));
        await engine.Host.DispatchAsync();
        Guid delivery = receipt.Deliveries[0].Delivery.Value;
        EntryId entry = new(Guid.Parse((await engine.Host.ColumnAsync<string>(delivery, "provider_reference"))!));

        Assert.Equal(2, await UseAsync(engine.Host, list => list.UnreadCountAsync(InApp.Person, TestContext.Current.CancellationToken).AsTask()));
        Result first = await UseAsync(engine.Host, list => list.MarkReadAsync(InApp.Person, entry, TestContext.Current.CancellationToken).AsTask());
        DateTimeOffset? readAt = (await ListAsync(engine.Host, InApp.Person)).Value!.Entries.Single(each => each.Entry == entry).ReadAt;
        Result again = await UseAsync(engine.Host, list => list.MarkReadAsync(InApp.Person, entry, TestContext.Current.CancellationToken).AsTask());

        Assert.True(first.IsSuccess);
        Assert.True(again.IsSuccess);
        Assert.NotNull(readAt);
        Assert.Equal(readAt, (await ListAsync(engine.Host, InApp.Person)).Value!.Entries.Single(each => each.Entry == entry).ReadAt);
        Assert.Equal(1, await UseAsync(engine.Host, list => list.UnreadCountAsync(InApp.Person, TestContext.Current.CancellationToken).AsTask()));
        Assert.Equal(DeliveryStatus.Read, Assert.Single((await engine.Host.ReadAsync(receipt.Send)).Deliveries).Status);
        Assert.Equal(1L, await InAppDispatchTests.NoticesAsync(engine.Host, delivery, "read"));
    }

    [Fact]
    public async Task MSG_INAPP_003_AC3_MarkingAnotherPersonsEntryReadAnswersInappUnknownAndChangesNothing()
    {
        await using DispatchEngine engine = await StartAsync();
        SendReceipt receipt = (await engine.Host.SendAsync(InApp.Send())).Value!;
        await engine.Host.DispatchAsync();
        Guid delivery = receipt.Deliveries[0].Delivery.Value;
        EntryId entry = new(Guid.Parse((await engine.Host.ColumnAsync<string>(delivery, "provider_reference"))!));

        Result other = await UseAsync(engine.Host, list => list.MarkReadAsync("person-8", entry, TestContext.Current.CancellationToken).AsTask());
        Result unknown = await UseAsync(engine.Host, list => list.MarkReadAsync(InApp.Person, new EntryId(Guid.CreateVersion7()), TestContext.Current.CancellationToken).AsTask());

        Assert.Equal("messaging.inapp.unknown", other.Failure!.Code);
        Assert.Equal("messaging.inapp.unknown", unknown.Failure!.Code);
        Assert.Equal(1, await UseAsync(engine.Host, list => list.UnreadCountAsync(InApp.Person, TestContext.Current.CancellationToken).AsTask()));
        Assert.Equal(DeliveryStatus.Sent, Assert.Single((await engine.Host.ReadAsync(receipt.Send)).Deliveries).Status);
    }

    [Fact]
    public async Task MSG_INAPP_003_AC2_MarkingEveryEntryReadAnswersHowManyItMarkedAndMovesTheirDeliveriesToRead()
    {
        await using DispatchEngine engine = await StartAsync();
        SendReceipt first = (await engine.Host.SendAsync(InApp.Send())).Value!;
        SendReceipt second = (await engine.Host.SendAsync(InApp.Send(order: "A-1002"))).Value!;
        await engine.Host.SendAsync(InApp.Send(person: "person-8"));
        await engine.Host.DispatchAsync();

        int marked = await UseAsync(engine.Host, list => list.MarkAllReadAsync(InApp.Person, TestContext.Current.CancellationToken).AsTask());
        int again = await UseAsync(engine.Host, list => list.MarkAllReadAsync(InApp.Person, TestContext.Current.CancellationToken).AsTask());

        Assert.Equal((2, 0), (marked, again));
        Assert.Equal(0, await UseAsync(engine.Host, list => list.UnreadCountAsync(InApp.Person, TestContext.Current.CancellationToken).AsTask()));
        Assert.Equal(1, await UseAsync(engine.Host, list => list.UnreadCountAsync("person-8", TestContext.Current.CancellationToken).AsTask()));
        Assert.Equal(DeliveryStatus.Read, Assert.Single((await engine.Host.ReadAsync(first.Send)).Deliveries).Status);
        Assert.Equal(DeliveryStatus.Read, Assert.Single((await engine.Host.ReadAsync(second.Send)).Deliveries).Status);
    }

    [Theory]
    [InlineData("not a cursor")]
    [InlineData("")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("________________________________")]
    public async Task MSG_INAPP_003_ACursorThatDoesNotReadAnswersInappCursorInvalid(string cursor)
    {
        await using DispatchEngine engine = await StartAsync();

        Result<InAppPage> result = await ListAsync(engine.Host, InApp.Person, cursor: cursor);

        Assert.Equal("messaging.inapp.cursorinvalid", result.Failure!.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task MSG_INAPP_003_ASizeOutsideOneToAHundredIsAFaultOfTheCallingCode(int size)
    {
        await using DispatchEngine engine = await StartAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => ListAsync(engine.Host, InApp.Person, size: size));
    }

    [Fact]
    public async Task MSG_INAPP_003_AnEntryWhoseValueDoesNotOpenUnderItsBindingIsLeftOutAndRaisesContentRejected()
    {
        AlertSinkFake alerts = new();
        await using DispatchEngine engine = await StartAsync(services: services => services.AddSingleton<IAlertSink>(alerts));
        SendReceipt moved = (await engine.Host.SendAsync(InApp.Send())).Value!;
        await engine.Host.SendAsync(InApp.Send(order: "A-1002"));
        await engine.Host.DispatchAsync();
        Guid entry = Guid.Parse((await engine.Host.ColumnAsync<string>(moved.Deliveries[0].Delivery.Value, "provider_reference"))!);
        await engine.Host.RunAsync(database =>
        {
            NpgsqlCommand command = new("UPDATE messaging.entries SET parts = (SELECT parts FROM messaging.entries WHERE id <> @id) WHERE id = @id", database);
            command.Parameters.AddWithValue("id", entry);
            return command;
        });

        InAppEntry listed = Assert.Single((await ListAsync(engine.Host, InApp.Person)).Value!.Entries);

        Assert.Equal("Order A-1002 en en", listed.Title);
        Assert.Contains(alerts.Calls, call => call is { Raised: true, Condition: "content-rejected", Scope: "entries" } && call.Details!["table"] == "entries");
    }

    internal static Task<Result<InAppPage>> ListAsync(EngineHost host, string person, string? language = null, string? cursor = null, int? size = null)
    {
        return UseAsync(host, list => list.ListAsync(person, language, cursor, size, TestContext.Current.CancellationToken).AsTask());
    }

    internal static async Task<T> UseAsync<T>(EngineHost host, Func<IInApp, Task<T>> use)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(use);
        AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using (scope)
        {
            return await use(scope.ServiceProvider.GetRequiredService<IInApp>());
        }
    }

    private Task<DispatchEngine> StartAsync(Action<Dictionary<string, string?>>? configure = null, Action<IServiceCollection>? services = null)
    {
        return DispatchEngine.StartAsync(postgres, configure, InApp.Declare, InApp.Templates, services);
    }
}