using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using MimeKit;

using Xunit;

namespace Hermes.Channels.Tests;

/// <summary>The shipped email provider over a scripted SMTP server in the test process.</summary>
internal static class SmtpHarness
{
    /// <summary>The name the test servers' certificates are for, which names the loopback addresses.</summary>
    internal const string Host = "localhost";

    internal const string ToAddress = "person@example.com";

    /// <summary>
    /// The keys every channel requires, with the SMTP server at <paramref name="port"/> taking
    /// TLS as <paramref name="tls"/> gives, <paramref name="trusted"/> its only authority where
    /// given, and a bound of ten seconds on each call.
    /// </summary>
    internal static Dictionary<string, string?> Configuration(int port, SmtpTls tls, TestAuthority? trusted)
    {
        Dictionary<string, string?> values = ChannelsHost.Used();
        values["Hermes:Email:Host"] = Host;
        values["Hermes:Email:Port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        values["Hermes:Email:Security"] = tls == SmtpTls.StartTls ? "StartTls" : "ImplicitTls";
        values["Hermes:Dispatch:Timeout"] = "00:00:10";
        if (trusted is not null)
        {
            values["Hermes:Email:TrustedAuthority"] = trusted.Pem;
        }

        return values;
    }

    /// <summary>A started host over <paramref name="values"/>, holding <paramref name="secrets"/> or <see cref="ChannelsHost.Secrets"/>, on <paramref name="time"/> where given.</summary>
    internal static async Task<IHost> StartAsync(Dictionary<string, string?> values, SecretSourceFake? secrets = null, TimeProvider? time = null)
    {
        IHost host = ChannelsHost.Build(values, services =>
        {
            services.AddSingleton<ISecretSource>(secrets ?? ChannelsHost.Secrets());
            if (time is not null)
            {
                services.AddSingleton(time);
            }
        });
        await host.StartAsync(TestContext.Current.CancellationToken);
        return host;
    }

    /// <summary>Sends <paramref name="message"/> or <see cref="Message"/> through a session of its own.</summary>
    internal static async Task<Attempt> SendAsync(IHost host, EmailMessage? message = null)
    {
        IEmailSession session = await host.Services.GetRequiredService<IEmailProvider>().OpenAsync(TestContext.Current.CancellationToken);
        await using (session.ConfigureAwait(false))
        {
            return await session.SendAsync(message ?? Message(), TestContext.Current.CancellationToken);
        }
    }

    /// <summary>An email to <see cref="ToAddress"/>, with an HTML part where <paramref name="html"/> is given.</summary>
    internal static EmailMessage Message(string? html = null)
    {
        return new EmailMessage
        {
            From = new EmailAddress { Address = ChannelsHost.FromAddress, DisplayName = "Order notices" },
            ToAddress = ToAddress,
            Subject = "Your order has shipped",
            Text = "Your order 1042 has shipped.",
            Html = html,
            MessageId = Guid.NewGuid().ToString("N") + "@example.com",
            Language = "en",
        };
    }

    /// <summary>The message <paramref name="received"/> holds, as MIME.</summary>
    internal static MimeMessage Read(SmtpReceived received)
    {
        using MemoryStream data = new(received.Data);
        return MimeMessage.Load(data);
    }
}