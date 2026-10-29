using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Hermes.Channels;
using Hermes.Core;
using Hermes.Testing;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using MimeKit;

using Xunit;

namespace Hermes.Development.Tests;

/// <summary>What each record holds (<c>02</c> MSG-DEV-003), and an email's <c>.eml</c> against what the SMTP provider submits (MSG-DEV-002).</summary>
[Trait("Category", "integration")]
public sealed class RecordTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task MSG_DEV_003_AC1_ARecordOfEachChannelTheEngineDispatchedHoldsItsMembersAndItsDeliverysKindCategoryLanguageAndPerson()
    {
        using ScratchFolder folder = new();
        await using InboxHost host = await InboxHost.StartAsync(postgres, folder.Path);
        await host.SubscribeAsync();

        await host.SendAsync([Channel.Email, Channel.Sms, Channel.Push], language: "ar");
        IReadOnlyList<InboxMessage> messages = await Captured.WaitAsync(folder.Path, 3);

        string app = host.App.Environment.ApplicationName;
        Assert.All(messages, message =>
        {
            Assert.Equal((app, "person-1", "order.ready", "orders", "ar"), (message.App, message.Person, message.Kind, message.Category, message.Language));
            Assert.Equal(1, new[] { message.Email is not null, message.Sms is not null, message.Push is not null }.Count(set => set));
        });

        InboxMessage email = messages.Single(message => message.Channel == Channel.Email);
        Assert.Equal(("someone@example.com", "Your code", "Your code is 4521. Sign in at https://bakery.example/sign-in?code=4521."), (email.To, email.Summary, email.Text));
        Assert.Equal(["https://bakery.example/sign-in?code=4521", "https://bakery.example/help"], email.Links);
        Assert.Equal(("orders@bakery.example", "Bakery"), (email.Email!.From.Address, email.Email.From.DisplayName));
        Assert.Equal(("Your code", email.Text), (email.Email.Subject, email.Email.Text));
        Assert.Contains("href=\"https://bakery.example/help\"", email.Email.Html, StringComparison.Ordinal);
        Assert.EndsWith("@bakery.example", email.Email.MessageId, StringComparison.Ordinal);
        Assert.Contains(KeyValuePair.Create("Subject", "Your code"), email.Email.Headers);
        Assert.True(File.Exists(email.Email.EmlPath));

        InboxMessage sms = messages.Single(message => message.Channel == Channel.Sms);
        Assert.Equal(("+201000000001", "رمز الدخول 4521", "رمز الدخول 4521"), (sms.To, sms.Summary, sms.Text));
        Assert.Equal(("Bakery", "رمز الدخول 4521", SmsEncoding.Ucs2, 1), (sms.Sms!.Sender, sms.Sms.Text, sms.Sms.Encoding, sms.Sms.Segments));
        Assert.NotEmpty(sms.Sms.DeliveryReference);
        Assert.Empty(sms.Links);

        InboxMessage push = messages.Single(message => message.Channel == Channel.Push);
        Assert.Equal((null, "Your order", "Order 4521 is ready."), (push.To, push.Summary, push.Text));
        Assert.Equal(["https://bakery.example/orders/4521"], push.Links);
        Assert.Equal((PushService.Fcm, PushUrgency.Normal, TimeSpan.FromDays(1)), (push.Push!.Service, push.Push.Urgency, push.Push.Ttl));
        Assert.Equal(("Your order", "Order 4521 is ready.", "https://bakery.example/orders/4521", "ar", "rtl"), (push.Push.Notification.Title, push.Push.Notification.Body, push.Push.Notification.Url, push.Push.Notification.Lang, push.Push.Notification.Dir));
        Assert.Equal(0, host.Providers.Calls);
    }

    [Fact]
    public async Task MSG_DEV_003_AC1_AnEmailHandedToTheProvidersDirectlyHoldsNoKindCategoryOrPersonAndItsOwnLanguage()
    {
        using ScratchFolder folder = new();
        using IHost host = Captured.Direct(Environments.Development, folder.Path, HostOrder.ProvidersBefore, _ => { });
        await host.StartAsync(TestContext.Current.CancellationToken);

        IEmailSession session = await host.Services.GetRequiredService<IEmailProvider>().OpenAsync(TestContext.Current.CancellationToken);
        await session.SendAsync(Captured.Email() with { Language = "fr" }, TestContext.Current.CancellationToken);

        InboxMessage message = Assert.Single(Captured.In(folder.Path));
        Assert.Equal((null, null, null, "fr"), (message.Kind, message.Category, message.Person, message.Language));
        Assert.Equal(host.Services.GetRequiredService<IHostEnvironment>().ApplicationName, message.App);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task MSG_DEV_003_AC3_APushRecordHoldsNoPartOfItsSubscriptionsEndpoint()
    {
        using ScratchFolder folder = new();
        await using InboxHost host = await InboxHost.StartAsync(postgres, folder.Path);
        string endpoint = await host.SubscribeAsync();

        await host.SendAsync([Channel.Push]);
        InboxMessage push = Assert.Single(await Captured.WaitAsync(folder.Path, 1));

        string record = await File.ReadAllTextAsync(Directory.EnumerateFiles(folder.Path, "*.json").Single(), TestContext.Current.CancellationToken);
        Assert.Equal(PushService.Fcm, push.Push!.Service);
        Assert.DoesNotContain(endpoint[(endpoint.LastIndexOf('/') + 1)..], record, StringComparison.Ordinal);
        Assert.DoesNotContain("fcm.googleapis.com", record, StringComparison.Ordinal);
        Assert.DoesNotContain("/fcm/send", record, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MSG_DEV_003_AC1_AnSmssSummaryIsItsFirstHundredCharactersCountedAsScalarValues()
    {
        using ScratchFolder folder = new();
        InboxCapture capture = new(new InboxWriter(new InboxSettings(folder.Path, TimeSpan.FromDays(7), 1_000, false), TimeProvider.System), "Bakery");
        string text = string.Concat(Enumerable.Repeat("😀", 60)) + new string('a', 60);

        await capture.SmsAsync(Captured.Sms() with { Text = text }, TestContext.Current.CancellationToken);

        InboxMessage message = Assert.Single(Captured.In(folder.Path));
        Assert.Equal(string.Concat(Enumerable.Repeat("😀", 60)) + new string('a', 40), message.Summary);
        Assert.Equal(text, message.Text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MSG_DEV_002_AC4_AnEmailsEmlReadsToTheSameHeadersAndPartsAsTheMessageTheSmtpProviderSubmits(bool html)
    {
        EmailMessage message = Captured.Email() with
        {
            Html = html ? "<p>Your receipt: <a href=\"https://bakery.example/receipts/7\">open</a> é ٧</p>" : null,
            ReplyTo = new EmailAddress { Address = "help@bakery.example", DisplayName = "Bakery help" },
            Unsubscribe = new Uri("https://bakery.example/unsubscribe/token"),
        };

        MimeMessage submitted = await SubmittedAsync(message);
        MimeMessage kept = await KeptAsync(message);

        Assert.Equal(Headers(submitted), Headers(kept));
        Assert.Equal(Parts(submitted), Parts(kept));
    }

    /// <summary>The message the shipped SMTP provider submits for <paramref name="message"/>, read from a test SMTP server.</summary>
    private static async Task<MimeMessage> SubmittedAsync(EmailMessage message)
    {
        using TestAuthority authority = TestAuthority.Create();
        await using SmtpServerFake server = SmtpServerFake.Start(SmtpTls.Implicit, authority.Issue("localhost", TimeSpan.FromDays(6)));
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Production });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Hermes:Email:From:Address"] = "orders@bakery.example",
            ["Hermes:Email:Host"] = "localhost",
            ["Hermes:Email:Port"] = server.Port.ToString(CultureInfo.InvariantCulture),
            ["Hermes:Email:Security"] = "ImplicitTls",
            ["Hermes:Email:TrustedAuthority"] = authority.Pem,
            ["Hermes:Dispatch:Timeout"] = "00:00:10",
        });
        builder.Services.AddHermesChannels(builder.Configuration);
        builder.Services.AddSingleton<ISecretSource>(new FixedSecretSource());
        builder.AddHermesDevelopmentInbox();
        using IHost host = Captured.Checked(builder);
        await host.StartAsync(TestContext.Current.CancellationToken);
        IEmailSession session = await host.Services.GetRequiredService<IEmailProvider>().OpenAsync(TestContext.Current.CancellationToken);
        await using (session)
        {
            Assert.IsType<Attempt.Accepted>(await session.SendAsync(message, TestContext.Current.CancellationToken));
        }

        await host.StopAsync(TestContext.Current.CancellationToken);
        using MemoryStream data = new(Assert.Single(server.Messages).Data);
        return await MimeMessage.LoadAsync(data, TestContext.Current.CancellationToken);
    }

    /// <summary>The <c>.eml</c> the inbox keeps for <paramref name="message"/>.</summary>
    private static async Task<MimeMessage> KeptAsync(EmailMessage message)
    {
        using ScratchFolder folder = new();
        using IHost host = Captured.Direct(Environments.Development, folder.Path, HostOrder.ProvidersBefore, _ => { });
        await host.StartAsync(TestContext.Current.CancellationToken);
        IEmailSession session = await host.Services.GetRequiredService<IEmailProvider>().OpenAsync(TestContext.Current.CancellationToken);
        await session.SendAsync(message, TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);
        return await MimeMessage.LoadAsync(Assert.Single(Captured.In(folder.Path)).Email!.EmlPath, TestContext.Current.CancellationToken);
    }

    /// <summary>The message's headers by name and value, save <c>Date</c>, a boundary left out of <c>Content-Type</c>.</summary>
    private static List<(string, string)> Headers(MimeMessage message)
    {
        return [.. message.Headers.Where(header => header.Id != HeaderId.Date).Select(header => (header.Field, header.Field == "Content-Type" ? message.Body!.ContentType.MimeType : header.Value))];
    }

    /// <summary>Each part's type, character set and decoded text.</summary>
    private static List<(string, string?, string)> Parts(MimeMessage message)
    {
        return [.. message.BodyParts.OfType<TextPart>().Select(part => (part.ContentType.MimeType, part.ContentType.Charset, part.Text))];
    }
}