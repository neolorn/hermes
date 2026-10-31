using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Sending;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Xunit;

namespace Hermes.Tests;

/// <summary>
/// An in-app kind, <see cref="Kind"/>, of the optional category <see cref="Category"/>, which
/// also goes out by SMS and leads to <see cref="Target"/> with the value <c>orderId</c>; its
/// templates name their language, and its title reads <c>orderId</c>.
/// </summary>
internal static class InApp
{
    internal const string Kind = "order.placed";

    internal const string Category = "orders";

    internal const string Target = "orders.view";

    internal const string Person = "person-7";

    internal static void Declare(DeclarationBuilder declare)
    {
        ArgumentNullException.ThrowIfNull(declare);
        declare.Category(Category);
        declare.Kind(Kind, Category).Sms().InApp(Target, ["orderId"]);
    }

    internal static string? Templates(TemplateKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        string language = key.Language;
        return key switch
        {
            { Page: not null } => null,
            { Channel: Channel.Sms, Part: "text" } => $"Sms {language} {{{{ orderId }}}}",
            { Channel: Channel.InApp, Part: "title" } => $"Order {{{{ orderId }}}} {language} {{{{ language }}}}",
            { Channel: Channel.InApp, Part: "body" } => $"Body {language} {{{{ direction }}}}",
            _ => null,
        };
    }

    /// <summary>A send of <see cref="Kind"/> to <paramref name="person"/> on the in-app channel alone.</summary>
    internal static Send Send(string person = Person, string language = "en", string order = "A-1001", IReadOnlyList<Channel>? channels = null)
    {
        return new Send
        {
            Kind = Kind,
            Recipient = new Recipient.Person(person),
            Phone = "+201000000002",
            Language = language,
            Values = new Dictionary<string, SendValue> { ["orderId"] = new SendValue.PlainText(order) },
            Channels = channels ?? [Channel.InApp],
        };
    }

    /// <summary>A host that records sends of <see cref="Kind"/>, in <c>en</c> and <c>ar</c>.</summary>
    internal static Task<EngineHost> StartAsync(PostgresFixture postgres, Action<Dictionary<string, string?>>? configure = null, Action<IServiceCollection>? services = null)
    {
        ArgumentNullException.ThrowIfNull(postgres);
        return StartOnAsync(postgres, configure, services);
    }

    /// <summary>An in-app delivery's stored values opened, by field, as the dispatcher opens them.</summary>
    internal static async Task<Dictionary<string, string>> OpenAsync(EngineHost host, Guid delivery)
    {
        ArgumentNullException.ThrowIfNull(host);
        string? column = await host.ColumnAsync<string>(delivery, "parts");
        Assert.NotNull(column);
        string? person = await host.ColumnAsync<string>(delivery, "person");
        Dictionary<string, string> opened = new(StringComparer.Ordinal);
        AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using (scope)
        {
            ContentProtection protection = scope.ServiceProvider.GetRequiredService<ContentProtection>();
            foreach ((string field, byte[] stored) in InAppParts.Read(column)!)
            {
                ContentOpening opening = await protection.OpenAsync(person, stored, ContentContext.Of(ContentContext.Deliveries, delivery, field), TestContext.Current.CancellationToken);
                Assert.Equal(ContentOpeningStatus.Opened, opening.Status);
                opened[field] = Encoding.UTF8.GetString(opening.Plaintext.Span);
            }
        }

        return opened;
    }

    /// <summary>How many entries the test database holds, for <paramref name="person"/> where one is given.</summary>
    internal static async Task<long> EntriesAsync(EngineHost host, string? person = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        await using NpgsqlConnection database = new(host.Connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("SELECT count(*) FROM messaging.entries WHERE @person IS NULL OR person = @person", database);
        command.Parameters.AddWithValue("person", NpgsqlTypes.NpgsqlDbType.Text, (object?)person ?? DBNull.Value);
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private static async Task<EngineHost> StartOnAsync(PostgresFixture postgres, Action<Dictionary<string, string?>>? configure, Action<IServiceCollection>? services)
    {
        return await EngineHost.StartAsync(await EngineHost.DatabaseAsync(postgres), Declare, Templates, configure, services);
    }
}