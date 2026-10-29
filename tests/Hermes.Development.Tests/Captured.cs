using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Channels;
using Hermes.Core;
using Hermes.Testing;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Xunit;

namespace Hermes.Development.Tests;

/// <summary>Reads what an inbox folder holds, and makes hosts that hand messages to the providers directly.</summary>
internal static class Captured
{
    /// <summary>The destinations, content values and person references the tests use, which no library log line may hold (<c>06</c> MSG-OPS-005).</summary>
    internal static IReadOnlyList<string> Used { get; } =
    [
        "someone@example.com", "other@example.com", "earlier@example.com", "+201000000001", "person-1", "4521",
        "Your receipt", "Your order", "Your code", "رمز الدخول", "bakery.example/receipts", "bakery.example/sign-in",
    ];

    /// <summary>The messages <paramref name="folder"/> holds, newest first.</summary>
    internal static IReadOnlyList<InboxMessage> In(string folder)
    {
        return InboxFiles.List(folder, new InboxFilter(), int.MaxValue);
    }

    /// <summary>Waits until <paramref name="folder"/> holds <paramref name="count"/> messages, and answers them, newest first.</summary>
    internal static async Task<IReadOnlyList<InboxMessage>> WaitAsync(string folder, int count)
    {
        Stopwatch waited = Stopwatch.StartNew();
        while (true)
        {
            IReadOnlyList<InboxMessage> messages = In(folder);
            if (messages.Count >= count || waited.Elapsed > TimeSpan.FromSeconds(30))
            {
                Assert.True(messages.Count == count, $"The inbox holds {messages.Count} messages, not {count}.");
                return messages;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// A host that hands messages to the providers directly, through <c>AddHermesChannels</c>
    /// alone, its inbox in <paramref name="folder"/>, with <paramref name="register"/> run around
    /// the inbox's call as <paramref name="order"/> says.
    /// </summary>
    internal static IHost Direct(string? environment, string folder, HostOrder order, Action<IServiceCollection> register)
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = environment });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Hermes:Email:From:Address"] = "orders@bakery.example",
            ["Hermes:Sms:Sender"] = "Bakery",
            ["Hermes:Sms:AllowedPrefixes:0"] = "+20",
            ["Hermes:DevelopmentInbox:Folder"] = folder,
        });
        builder.Services.AddHermesChannels(builder.Configuration);
        builder.Services.AddSingleton<ISecretSource>(new FixedSecretSource());
        if (order == HostOrder.ProvidersBefore)
        {
            register(builder.Services);
        }

        builder.AddHermesDevelopmentInbox();
        if (order == HostOrder.ProvidersAfter)
        {
            register(builder.Services);
        }

        return Checked(builder);
    }

    /// <summary>The host <paramref name="builder"/> builds, its log lines checked against <see cref="Used"/> when it is disposed.</summary>
    internal static IHost Checked(HostApplicationBuilder builder)
    {
        return new CheckedHost(builder);
    }

    /// <summary>An email to <c>someone@example.com</c> holding a link, as a host hands it to the provider.</summary>
    internal static EmailMessage Email()
    {
        return new EmailMessage
        {
            From = new EmailAddress { Address = "orders@bakery.example", DisplayName = "Bakery" },
            ToAddress = "someone@example.com",
            Subject = "Your receipt",
            Text = "Your receipt: https://bakery.example/receipts/7",
            Html = "<p><a href=\"https://bakery.example/receipts/7\">Your receipt</a></p>",
            MessageId = Guid.NewGuid().ToString("N") + "@bakery.example",
            Language = "en",
        };
    }

    /// <summary>An SMS to <c>+201000000001</c>.</summary>
    internal static SmsMessage Sms()
    {
        return new SmsMessage
        {
            ToNumber = "+201000000001",
            Sender = "Bakery",
            Text = "Your order is ready.",
            DeliveryReference = Guid.NewGuid().ToString("N"),
            Encoding = SmsEncoding.Gsm7,
            Segments = 1,
        };
    }

    /// <summary>A push request to an FCM subscription, with a payload of its title, body and address.</summary>
    internal static PushRequest Push(string endpoint)
    {
        byte[] payload = """{"title":"Your order","body":"It is ready.","url":"https://bakery.example/orders/7","lang":"en","dir":"ltr"}"""u8.ToArray();
        return new PushRequest.WebPush(endpoint, "p256dh", "auth", 1, payload, TimeSpan.FromHours(1), PushUrgency.High, "order-7");
    }
}

/// <summary>A host whose library log lines are checked when it is disposed (<c>06</c> MSG-OPS-005).</summary>
internal sealed class CheckedHost : IHost
{
    private readonly LogSink logs = new();

    private readonly IHost host;

    internal CheckedHost(HostApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(logs);
        host = builder.Build();
    }

    public IServiceProvider Services => host.Services;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        return host.StartAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return host.StopAsync(cancellationToken);
    }

    public void Dispose()
    {
        host.Dispose();
        logs.Dispose();
        logs.Check();
    }
}