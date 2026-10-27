using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "integration")]
public sealed class SuppressionTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    internal const string Offers = "offers";

    internal const string Offer = "offer.made";

    private const string Number = "+201000000001";

    private const string Other = "+201000000002";

    [Fact]
    public async Task MSG_SEND_012_AC1_AfterAnUnsubscribeADeliveryOfTheCategoryToTheDestinationEndsSuppressedForAPersonAndAnAnonymousRecipientAlike()
    {
        await using DispatchEngine engine = await DispatchEngine.StartAsync(postgres, declare: Declare);
        Assert.True((await AddAsync(engine.Host, Channel.Sms, Number, Offers)).IsSuccess);
        SendReceipt person = (await engine.Host.SendAsync(OfferTo(Number))).Value!;
        SendReceipt anonymous = (await engine.Host.SendAsync(OfferTo(Number) with { Recipient = new Recipient.Anonymous() })).Value!;
        SendReceipt essential = (await engine.Host.SendAsync(SendTests.Code(phone: Number, channels: [Channel.Sms]))).Value!;

        await engine.Host.DispatchAsync();

        Assert.Equal(DeliveryStatus.Suppressed, await StatusAsync(engine.Host, person));
        Assert.Equal(DeliveryStatus.Suppressed, await StatusAsync(engine.Host, anonymous));
        Assert.Equal(DeliveryStatus.Sent, await StatusAsync(engine.Host, essential));
        Assert.Single(engine.Sms.Received);
        Assert.Empty(Assert.Single((await engine.Host.ReadAsync(person.Send)).Deliveries).Attempts);
    }

    [Fact]
    public async Task MSG_SEND_012_AC1_AnEmailSuppressedForTheCategoryEndsSuppressedWhateverTheCaseOfItsDomain()
    {
        await using DispatchEngine engine = await DispatchEngine.StartAsync(postgres, configure: UnsubscribeLinkTests.Configure, declare: UnsubscribeLinkTests.Declare, templates: UnsubscribeLinkTests.Templates);
        Assert.True((await AddAsync(engine.Host, Channel.Email, "someone@example.com", UnsubscribeLinkTests.News)).IsSuccess);
        SendReceipt suppressed = (await engine.Host.SendAsync(UnsubscribeLinkTests.LetterTo("someone@EXAMPLE.com"))).Value!;
        SendReceipt other = (await engine.Host.SendAsync(UnsubscribeLinkTests.LetterTo("other@example.com"))).Value!;

        await engine.Host.DispatchAsync();

        Assert.Equal(DeliveryStatus.Suppressed, await StatusAsync(engine.Host, suppressed));
        Assert.Equal(DeliveryStatus.Sent, await StatusAsync(engine.Host, other));
        Assert.Equal("other@example.com", Assert.Single(engine.Email.Received).ToAddress);
    }

    [Fact]
    public async Task MSG_SEND_012_AC2_TheSuppressionTableHoldsNoDestinationInPlainText()
    {
        await using DispatchEngine engine = await DispatchEngine.StartAsync(postgres, declare: Declare);
        Assert.True((await AddAsync(engine.Host, Channel.Sms, Number, Offers)).IsSuccess);
        Assert.True((await AddAsync(engine.Host, Channel.Email, "Someone@Example.com", Offers)).IsSuccess);

        string rows = await DumpAsync(engine.Host);

        Assert.Equal(2, rows.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.DoesNotContain("201000000001", rows, StringComparison.Ordinal);
        Assert.DoesNotContain("someone", rows, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Convert.ToHexString(Encoding.UTF8.GetBytes("201000000001")), rows, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Convert.ToHexString(Encoding.UTF8.GetBytes("someone")), rows, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("sms", "+201000000001", EngineHost.Category, "messaging.suppression.essential", "category", EngineHost.Category)]
    [InlineData("sms", "+201000000001", "unknown", "messaging.suppression.invalid", "member", "category")]
    [InlineData("push", "+201000000001", Offers, "messaging.suppression.invalid", "member", "channel")]
    [InlineData("sms", "not a number", Offers, "messaging.suppression.invalid", "member", "destination")]
    [InlineData("email", "no address", Offers, "messaging.suppression.invalid", "member", "destination")]
    public async Task Add_ARefusedSuppression_AnswersItsCodeAndChangesNothing(string channel, string destination, string category, string code, string detail, string value)
    {
        await using DispatchEngine engine = await DispatchEngine.StartAsync(postgres, declare: Declare);

        Result result = await AddAsync(engine.Host, Spellings.Channel(channel)!.Value, destination, category);

        Assert.Equal(code, result.Failure!.Code);
        Assert.Equal(value, result.Failure.Details[detail]);
        Assert.Equal(string.Empty, await DumpAsync(engine.Host));
    }

    [Fact]
    public async Task Remove_AWithdrawnSuppression_LetsTheNextDeliveryOfTheCategoryGoOut()
    {
        await using DispatchEngine engine = await DispatchEngine.StartAsync(postgres, declare: Declare);
        Assert.True((await AddAsync(engine.Host, Channel.Sms, Number, Offers)).IsSuccess);
        Assert.True((await AddAsync(engine.Host, Channel.Sms, Number, Offers)).IsSuccess);

        Result removed = await WithSuppressionsAsync(engine.Host, suppressions => suppressions.RemoveAsync(Channel.Sms, Number, Offers, TestContext.Current.CancellationToken).AsTask());
        SendReceipt receipt = (await engine.Host.SendAsync(OfferTo(Number))).Value!;
        await engine.Host.DispatchAsync();

        Assert.True(removed.IsSuccess);
        Assert.Equal(DeliveryStatus.Sent, await StatusAsync(engine.Host, receipt));
        Assert.Equal(string.Empty, await DumpAsync(engine.Host));
    }

    [Fact]
    public async Task MSG_SEND_006_AC4_AProcessWithoutHashKeysVersion2NeverDispatchesToADestinationSuppressedUnderIt()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        ManualClock clock = new(EngineHost.Start);
        SecretSourceFake older = Secrets.Held(connection).With("hashKeys", Secrets.Bytes(32, 2));
        SecretSourceFake newer = Secrets.Held(connection).With("hashKeys", Secrets.Bytes(32, 2), Secrets.Bytes(32, 9));
        await using DispatchEngine without = await DispatchEngine.StartAsync(postgres, declare: Declare, connection: connection, clock: clock, services: services => services.AddSingleton<ISecretSource>(older));
        await using DispatchEngine with = await DispatchEngine.StartAsync(
            postgres,
            configure: configuration => configuration["Hermes:Secrets:HashKeys:WriteVersion"] = "2",
            declare: Declare,
            connection: connection,
            clock: clock,
            services: services => services.AddSingleton<ISecretSource>(newer));
        Assert.True((await AddAsync(with.Host, Channel.Sms, Number, Offers)).IsSuccess);
        SendReceipt suppressed = (await with.Host.SendAsync(OfferTo(Number))).Value!;
        SendReceipt other = (await with.Host.SendAsync(OfferTo(Other))).Value!;

        for (int pass = 0; pass < 3; pass++)
        {
            Assert.Equal(2, await without.Host.DispatchAsync());
            clock.Advance(TimeSpan.FromSeconds(31));
        }

        Assert.Empty(without.Sms.Received);
        Assert.Equal(DeliveryStatus.Waiting, await StatusAsync(with.Host, suppressed));
        Assert.Equal(AttemptReason.KeyUnavailable, Assert.Single((await with.Host.ReadAsync(other.Send)).Deliveries).Reason);

        Assert.Equal(2, await with.Host.DispatchAsync());

        Assert.Equal(DeliveryStatus.Suppressed, await StatusAsync(with.Host, suppressed));
        Assert.Equal(DeliveryStatus.Sent, await StatusAsync(with.Host, other));
        Assert.Equal(Other, Assert.Single(with.Sms.Received).ToNumber);
    }

    /// <summary>An essential category of every channel, and the optional category <c>offers</c> with an SMS kind.</summary>
    internal static void Declare(DeclarationBuilder declare)
    {
        ArgumentNullException.ThrowIfNull(declare);
        EngineHost.Essential(declare);
        declare.Category(Offers);
        declare.Kind(Offer, Offers).Sms();
    }

    internal static Send OfferTo(string number)
    {
        return SendTests.Code(phone: number, channels: [Channel.Sms]) with { Kind = Offer };
    }

    internal static async Task<DeliveryStatus> StatusAsync(EngineHost host, SendReceipt receipt)
    {
        return Assert.Single((await host.ReadAsync(receipt.Send)).Deliveries).Status;
    }

    internal static Task<Result> AddAsync(EngineHost host, Channel channel, string destination, string category)
    {
        return WithSuppressionsAsync(host, suppressions => suppressions.AddAsync(channel, destination, category, TestContext.Current.CancellationToken).AsTask());
    }

    private static async Task<Result> WithSuppressionsAsync(EngineHost host, Func<ISuppressions, Task<Result>> use)
    {
        AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using (scope)
        {
            return await use(scope.ServiceProvider.GetRequiredService<ISuppressions>());
        }
    }

    /// <summary>Every suppression row as text, each column's bytes in hexadecimal.</summary>
    private static async Task<string> DumpAsync(EngineHost host)
    {
        await using NpgsqlConnection database = new(host.Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("SELECT channel, category, encode(hash, 'hex'), hash_version FROM messaging.suppressions", database);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        StringBuilder rows = new();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            rows.Append(string.Join(' ', Enumerable.Range(0, reader.FieldCount).Select(index => Convert.ToString(reader.GetValue(index), System.Globalization.CultureInfo.InvariantCulture)))).Append('\n');
        }

        return rows.ToString();
    }
}