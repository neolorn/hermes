using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Sending;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "integration")]
public sealed class UnsubscribeLinkTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    internal const string News = "news";

    internal const string Letter = "news.letter";

    internal const string BaseAddress = "https://notices.example.com/notifications";

    [Fact]
    public async Task MSG_EMAIL_006_AC1_AnOptionalCategorysEmailCarriesTheOneClickAddressAndAVisibleLinkAndAnEssentialCategorysCarriesNeither()
    {
        await using DispatchEngine engine = await StartAsync();
        await engine.Host.SendAsync(LetterTo("someone@example.com"));
        await engine.Host.SendAsync(SendTests.Code(channels: [Channel.Email]));

        await engine.Host.DispatchAsync();

        EmailMessage optional = Assert.Single(engine.Email.Received, message => message.Unsubscribe is not null);
        EmailMessage essential = Assert.Single(engine.Email.Received, message => message.Unsubscribe is null);
        Assert.StartsWith(BaseAddress + "/unsubscribe/", optional.Unsubscribe!.AbsoluteUri, StringComparison.Ordinal);
        Assert.Contains(optional.Unsubscribe.AbsoluteUri, optional.Text, StringComparison.Ordinal);
        Assert.Contains("href=\"" + optional.Unsubscribe.AbsoluteUri + "\"", optional.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("unsubscribe", essential.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MSG_EMAIL_006_AC2_AnOptionalCategorysEmailTemplateWithoutUnsubscribeAndAnEssentialCategorysWithItStopTheStart()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);

        MessagingStartupException optional = await Assert.ThrowsAsync<MessagingStartupException>(() => StartAsync(connection, key => key is { Kind: Letter, Channel: Channel.Email, Part: "text" } ? "Text {{ code }}" : Templates(key)));
        MessagingStartupException essential = await Assert.ThrowsAsync<MessagingStartupException>(() => StartAsync(connection, key => key is { Kind: EngineHost.Kind, Channel: Channel.Email, Part: "text" } ? "Text {{ code }} {{ unsubscribe }}" : Templates(key)));

        Assert.Equal(("messaging.startup.declarationinvalid", Letter), (optional.Code, optional.Details["declaration"]));
        Assert.Equal(("messaging.startup.declarationinvalid", EngineHost.Kind), (essential.Code, essential.Details["declaration"]));
    }

    [Fact]
    public async Task MSG_EMAIL_006_AC3_AnEmailWhoseNotBeforeLies300DaysAheadUnderA180DayTokenLifetimeGoesOutWithALinkThatVerifies()
    {
        await using DispatchEngine engine = await StartAsync(configure: configuration => configuration["Hermes:Unsubscribe:TokenLifetime"] = "180.00:00:00");
        await engine.Host.SendAsync(LetterTo("someone@example.com") with { NotBefore = EngineHost.Start.AddDays(300) });

        engine.Host.Clock.Advance(TimeSpan.FromDays(300));
        await engine.Host.DispatchAsync();

        Uri address = Assert.Single(engine.Email.Received).Unsubscribe!;
        TokenReading reading = Read(engine.Host, address, TimeSpan.FromDays(180));
        Assert.Equal(TokenStatus.Valid, reading.Status);
        Assert.Equal((News, "en"), (reading.Category, reading.Language));
    }

    [Fact]
    public async Task MSG_SEND_018_AC1_TheUnsubscribeAddressIsStoredOnlyAsAProtectedValue()
    {
        await using DispatchEngine engine = await StartAsync();
        SendReceipt receipt = (await engine.Host.SendAsync(LetterTo("someone@example.com"))).Value!;
        Guid delivery = receipt.Deliveries[0].Delivery.Value;

        string address = (await engine.Host.OpenAsync(delivery, "unsubscribe"))!;
        byte[] stored = (await engine.Host.ColumnAsync<byte[]>(delivery, "unsubscribe"))!;

        Assert.StartsWith(BaseAddress + "/unsubscribe/", address, StringComparison.Ordinal);
        string token = address[(BaseAddress.Length + "/unsubscribe/".Length)..];
        Assert.False(stored.AsSpan().IndexOf(Encoding.UTF8.GetBytes(token)) >= 0);
        Assert.False(stored.AsSpan().IndexOf(Encoding.UTF8.GetBytes("notices.example.com")) >= 0);
    }

    [Fact]
    public async Task Token_MadeForAnAddress_CarriesItsKeyedHashAndReadsAsValidOnlyInItsCanonicalForm()
    {
        await using DispatchEngine engine = await StartAsync();
        await engine.Host.SendAsync(LetterTo("Someone@Example.com"));
        await engine.Host.DispatchAsync();
        Uri address = Assert.Single(engine.Email.Received).Unsubscribe!;
        KeyedHashes hashes = engine.Host.Services.GetRequiredService<KeyedHashes>();

        TokenReading reading = Read(engine.Host, address, TimeSpan.FromDays(730));
        string token = address.AbsoluteUri[(BaseAddress.Length + "/unsubscribe/".Length)..];

        Assert.Equal((1, 1), (reading.TokenVersion, reading.HashVersion));
        Assert.Equal(hashes.Under(1, KeyedHashes.Email, Encoding.UTF8.GetBytes("someone@example.com")), reading.Hash);
        Assert.Equal(TokenStatus.Invalid, Read(engine.Host, token + "=", TimeSpan.FromDays(730)).Status);
        Assert.Equal(TokenStatus.Invalid, Read(engine.Host, token[..^1], TimeSpan.FromDays(730)).Status);
    }

    /// <summary>An essential category of every channel, and the optional category <c>news</c> with an email and SMS kind.</summary>
    internal static void Declare(DeclarationBuilder declare)
    {
        ArgumentNullException.ThrowIfNull(declare);
        EngineHost.Essential(declare);
        declare.Category(News).Label("en", "News").Label("ar", "الأخبار");
        declare.Kind(Letter, News).Email().Sms();
    }

    /// <summary>The engine's templates, an optional category's email reading <c>unsubscribe</c> in its text and HTML.</summary>
    internal static string? Templates(TemplateKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return key switch
        {
            { Kind: Letter, Channel: Channel.Email, Part: "text" } => "Text {{ code }} {{ unsubscribe }}",
            { Kind: Letter, Channel: Channel.Email, Part: "html" } => "<p>{{ code }}</p><a href=\"{{ unsubscribe }}\">Unsubscribe</a>",
            _ => EngineHost.Templates(key),
        };
    }

    internal static void Configure(Dictionary<string, string?> configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        configuration["Hermes:PublicBaseAddress"] = BaseAddress;
    }

    internal static Send LetterTo(string address)
    {
        return SendTests.Code(email: address, channels: [Channel.Email]) with { Kind = Letter };
    }

    private Task<DispatchEngine> StartAsync(Action<Dictionary<string, string?>>? configure = null)
    {
        return DispatchEngine.StartAsync(
            postgres,
            configure: configuration =>
            {
                Configure(configuration);
                configure?.Invoke(configuration);
            },
            declare: Declare,
            templates: Templates);
    }

    private static async Task StartAsync(string connection, Func<TemplateKey, string?> templates)
    {
        await using EngineHost host = await EngineHost.StartAsync(connection, Declare, templates, Configure);
    }

    private static TokenReading Read(EngineHost host, Uri address, TimeSpan lifetime)
    {
        return Read(host, address.AbsoluteUri[(BaseAddress.Length + "/unsubscribe/".Length)..], lifetime);
    }

    private static TokenReading Read(EngineHost host, string token, TimeSpan lifetime)
    {
        return host.Services.GetRequiredService<UnsubscribeTokens>().Read(token, host.Clock.GetUtcNow(), lifetime, category => category == News);
    }
}