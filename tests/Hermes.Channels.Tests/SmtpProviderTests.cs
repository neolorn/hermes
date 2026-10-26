using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using MimeKit;

using Xunit;

namespace Hermes.Channels.Tests;

[Trait("Category", "unit")]
public sealed class SmtpProviderTests
{
    public static TheoryData<SmtpTls, string, string, string, AttemptReason, int?> Answers { get; } = new()
    {
        { SmtpTls.Implicit, "RCPT", "550 5.1.1 No such user", nameof(Attempt.Permanent), AttemptReason.RecipientRefused, 550 },
        { SmtpTls.Implicit, "RCPT", "450 4.2.1 Mailbox busy", nameof(Attempt.Transient), AttemptReason.Temporary, 450 },
        { SmtpTls.Implicit, "MAIL", "553 5.7.1 Sender not allowed", nameof(Attempt.Transient), AttemptReason.SenderRefused, 553 },
        { SmtpTls.Implicit, "MAIL", "451 4.3.0 Try again later", nameof(Attempt.Transient), AttemptReason.Temporary, 451 },
        { SmtpTls.Implicit, "MAIL", "530 5.7.0 Authentication required", nameof(Attempt.Transient), AttemptReason.AuthFailed, null },
        { SmtpTls.Implicit, "DATA", "554 5.6.0 Message refused", nameof(Attempt.Permanent), AttemptReason.MessageRefused, 554 },
        { SmtpTls.Implicit, "END", "552 5.3.4 Message too big", nameof(Attempt.Permanent), AttemptReason.MessageRefused, 552 },
        { SmtpTls.Implicit, "END", "451 4.3.0 Try again later", nameof(Attempt.Transient), AttemptReason.Temporary, 451 },
        { SmtpTls.Implicit, "AUTH", "535 5.7.8 Authentication credentials invalid", nameof(Attempt.Transient), AttemptReason.AuthFailed, 535 },
        { SmtpTls.Implicit, "AUTH", "504 5.5.4 Mechanism not recognised", nameof(Attempt.Transient), AttemptReason.AuthFailed, 504 },
        { SmtpTls.Implicit, "AUTH", "454 4.7.0 Temporary authentication failure", nameof(Attempt.Transient), AttemptReason.Temporary, 454 },
        { SmtpTls.Implicit, "greeting", "554 5.3.2 No service here", nameof(Attempt.Transient), AttemptReason.ServerRefused, 554 },
        { SmtpTls.Implicit, "greeting", "421 4.3.2 Busy", nameof(Attempt.Transient), AttemptReason.Temporary, 421 },
        { SmtpTls.Implicit, "greeting", "Hello there", nameof(Attempt.Transient), AttemptReason.ConnectionFailed, null },
        { SmtpTls.Implicit, "EHLO", "550 5.0.0 Not you", nameof(Attempt.Transient), AttemptReason.ServerRefused, 550 },
        { SmtpTls.StartTls, "STARTTLS", "554 5.7.3 No TLS for you", nameof(Attempt.Transient), AttemptReason.ServerRefused, 554 },
        { SmtpTls.StartTls, "STARTTLS", "454 4.7.0 TLS not available", nameof(Attempt.Transient), AttemptReason.Temporary, 454 },
    };

    [Fact]
    public async Task MSG_EMAIL_005_AC1_PassOfTenDeliveriesAuthenticatesOnce()
    {
        using TestAuthority authority = TestAuthority.Create();
        await using SmtpServerFake server = SmtpServerFake.Start(SmtpTls.Implicit, authority.Issue(SmtpHarness.Host, TimeSpan.FromDays(6)));
        using IHost host = await SmtpHarness.StartAsync(SmtpHarness.Configuration(server.Port, SmtpTls.Implicit, authority));

        List<Attempt> attempts = await SendAllAsync(host, 10);

        Assert.All(attempts, attempt => Assert.Equal(new Attempt.Accepted(null), attempt));
        Assert.Equal(1, server.Connections);
        Assert.Single(server.Authentications);
        Assert.Equal(10, server.Messages.Count);
    }

    [Fact]
    public async Task MSG_EMAIL_005_AC2_ConnectionBrokenAfterTheThirdDeliveryIsOpenedAgainForTheFourth()
    {
        using TestAuthority authority = TestAuthority.Create();
        await using SmtpServerFake server = SmtpServerFake.Start(SmtpTls.Implicit, authority.Issue(SmtpHarness.Host, TimeSpan.FromDays(6)));
        server.CloseAfter = 3;
        using IHost host = await SmtpHarness.StartAsync(SmtpHarness.Configuration(server.Port, SmtpTls.Implicit, authority));
        IEmailSession session = await host.Services.GetRequiredService<IEmailProvider>().OpenAsync(TestContext.Current.CancellationToken);
        await using (session)
        {
            for (int index = 0; index < 3; index++)
            {
                Assert.Equal(new Attempt.Accepted(null), await session.SendAsync(SmtpHarness.Message(), TestContext.Current.CancellationToken));
            }

            await server.WaitClosedAsync(1);

            Assert.Equal(new Attempt.Accepted(null), await session.SendAsync(SmtpHarness.Message(), TestContext.Current.CancellationToken));
        }

        Assert.Equal(2, server.Connections);
        Assert.Equal(2, server.Authentications.Count);
        Assert.Equal(4, server.Messages.Count);
    }

    [Fact]
    public async Task SendAsync_ServerClosingWithA421_OpensAgainForTheNextMessage()
    {
        using TestAuthority authority = TestAuthority.Create();
        await using SmtpServerFake server = SmtpServerFake.Start(SmtpTls.Implicit, authority.Issue(SmtpHarness.Host, TimeSpan.FromDays(6)));
        server.CloseAfter = 1;
        server.ClosingReply = "421 4.4.2 Closing the connection";
        using IHost host = await SmtpHarness.StartAsync(SmtpHarness.Configuration(server.Port, SmtpTls.Implicit, authority));
        IEmailSession session = await host.Services.GetRequiredService<IEmailProvider>().OpenAsync(TestContext.Current.CancellationToken);
        await using (session)
        {
            Assert.Equal(new Attempt.Accepted(null), await session.SendAsync(SmtpHarness.Message(), TestContext.Current.CancellationToken));
            await server.WaitClosedAsync(1);

            Assert.Equal(new Attempt.Accepted(null), await session.SendAsync(SmtpHarness.Message(), TestContext.Current.CancellationToken));
        }

        Assert.Equal(2, server.Connections);
    }

    [Fact]
    public async Task OpenAsync_Opening_ConnectsOnlyAtTheFirstMessage()
    {
        using TestAuthority authority = TestAuthority.Create();
        await using SmtpServerFake server = SmtpServerFake.Start(SmtpTls.Implicit, authority.Issue(SmtpHarness.Host, TimeSpan.FromDays(6)));
        using IHost host = await SmtpHarness.StartAsync(SmtpHarness.Configuration(server.Port, SmtpTls.Implicit, authority));

        IEmailSession session = await host.Services.GetRequiredService<IEmailProvider>().OpenAsync(TestContext.Current.CancellationToken);
        await using (session)
        {
            Assert.Equal(0, server.Connections);

            await session.SendAsync(SmtpHarness.Message(), TestContext.Current.CancellationToken);
        }

        Assert.Equal(1, server.Connections);
    }

    [Fact]
    public async Task OpenAsync_BeforeTheStart_Throws()
    {
        using IHost host = ChannelsHost.Build(ChannelsHost.Used());

        IEmailProvider provider = host.Services.GetRequiredService<IEmailProvider>();

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await provider.OpenAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MSG_EMAIL_001_AC4_AnotherSubmissionServerIsUsedByConfigurationAlone()
    {
        using TestAuthority first = TestAuthority.Create();
        using TestAuthority second = TestAuthority.Create();
        await using SmtpServerFake implicitTls = SmtpServerFake.Start(SmtpTls.Implicit, first.Issue(SmtpHarness.Host, TimeSpan.FromDays(6)));
        await using SmtpServerFake startTls = SmtpServerFake.Start(SmtpTls.StartTls, second.Issue(SmtpHarness.Host, TimeSpan.FromDays(6)));
        using IHost one = await SmtpHarness.StartAsync(SmtpHarness.Configuration(implicitTls.Port, SmtpTls.Implicit, first));
        using IHost other = await SmtpHarness.StartAsync(SmtpHarness.Configuration(startTls.Port, SmtpTls.StartTls, second));

        Assert.Equal(new Attempt.Accepted(null), await SmtpHarness.SendAsync(one));
        Assert.Equal(new Attempt.Accepted(null), await SmtpHarness.SendAsync(other));

        Assert.Single(implicitTls.Messages);
        Assert.Single(startTls.Messages);
        Assert.Contains("STARTTLS", startTls.Commands);
    }

    [Fact]
    public async Task MSG_EMAIL_001_AC2_CertificateOfTheTrustedAuthorityIsTakenAndOneOfASecondAuthorityIsNot()
    {
        using TestAuthority trusted = TestAuthority.Create();
        using TestAuthority second = TestAuthority.Create();
        await using SmtpServerFake issued = SmtpServerFake.Start(SmtpTls.Implicit, trusted.Issue(SmtpHarness.Host, TimeSpan.FromDays(6)));
        await using SmtpServerFake foreign = SmtpServerFake.Start(SmtpTls.Implicit, second.Issue(SmtpHarness.Host, TimeSpan.FromDays(6)));
        using IHost toIssued = await SmtpHarness.StartAsync(SmtpHarness.Configuration(issued.Port, SmtpTls.Implicit, trusted));
        using IHost toForeign = await SmtpHarness.StartAsync(SmtpHarness.Configuration(foreign.Port, SmtpTls.Implicit, trusted));

        Assert.Equal(new Attempt.Accepted(null), await SmtpHarness.SendAsync(toIssued));
        Assert.Equal(new Attempt.Transient(AttemptReason.TlsFailed, null, null), await SmtpHarness.SendAsync(toForeign));
        Assert.Empty(foreign.Commands);
    }

    [Theory]
    [InlineData(SmtpTls.StartTls, SmtpTls.StartTls)]
    [InlineData(SmtpTls.Implicit, SmtpTls.None)]
    public async Task SendAsync_ServerOfferingNoTls_SendsNothing(SmtpTls configured, SmtpTls offered)
    {
        using TestAuthority authority = TestAuthority.Create();
        await using SmtpServerFake server = SmtpServerFake.Start(offered, authority.Issue(SmtpHarness.Host, TimeSpan.FromDays(6)));
        server.OffersStartTls = false;
        using IHost host = await SmtpHarness.StartAsync(SmtpHarness.Configuration(server.Port, configured, authority));

        Attempt attempt = await SmtpHarness.SendAsync(host);

        Assert.Equal(new Attempt.Transient(AttemptReason.TlsFailed, null, null), attempt);
        Assert.DoesNotContain("AUTH", server.Commands);
        Assert.DoesNotContain("MAIL", server.Commands);
    }

    [Fact]
    public async Task SendAsync_CertificateForAnotherHost_SendsNothing()
    {
        using TestAuthority authority = TestAuthority.Create();
        await using SmtpServerFake server = SmtpServerFake.Start(SmtpTls.Implicit, authority.Issue("smtp.example.net", TimeSpan.FromDays(6)));
        using IHost host = await SmtpHarness.StartAsync(SmtpHarness.Configuration(server.Port, SmtpTls.Implicit, authority));

        Assert.Equal(new Attempt.Transient(AttemptReason.TlsFailed, null, null), await SmtpHarness.SendAsync(host));
        Assert.Empty(server.Commands);
    }

    [Fact]
    public async Task SendAsync_ChainToAnAuthorityNotTrusted_SendsNothing()
    {
        using TestAuthority authority = TestAuthority.Create();
        await using SmtpServerFake server = SmtpServerFake.Start(SmtpTls.Implicit, authority.Issue(SmtpHarness.Host, TimeSpan.FromDays(6)));
        using IHost host = await SmtpHarness.StartAsync(SmtpHarness.Configuration(server.Port, SmtpTls.Implicit, trusted: null));

        Assert.Equal(new Attempt.Transient(AttemptReason.TlsFailed, null, null), await SmtpHarness.SendAsync(host));
        Assert.Empty(server.Commands);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SendAsync_CertificateNamingARevocationSource_IsTakenOnlyWhereItsStatusIsReadGood(bool published)
    {
        await using CrlServerFake crls = await CrlServerFake.StartAsync();
        using TestAuthority authority = TestAuthority.Create();
        Uri crl = crls.Reserve();
        X509Certificate2 leaf = authority.Issue(SmtpHarness.Host, TimeSpan.FromDays(90), crl);
        if (published)
        {
            crls.Publish(crl, authority.Crl());
        }

        await using SmtpServerFake server = SmtpServerFake.Start(SmtpTls.Implicit, leaf);
        using IHost host = await SmtpHarness.StartAsync(SmtpHarness.Configuration(server.Port, SmtpTls.Implicit, authority));

        Attempt attempt = await SmtpHarness.SendAsync(host);

        Assert.Equal(published ? new Attempt.Accepted(null) : new Attempt.Transient(AttemptReason.TlsFailed, null, null), attempt);
        Assert.Equal(published ? 1 : 0, server.Messages.Count);
    }

    [Fact]
    public async Task SendAsync_ChainSentWithAnIntermediate_IsTaken()
    {
        await using CrlServerFake crls = await CrlServerFake.StartAsync();
        using TestAuthority authority = TestAuthority.Create();
        Uri intermediateCrl = crls.Reserve();
        using TestAuthority intermediate = authority.Intermediate(intermediateCrl);
        crls.Publish(intermediateCrl, authority.Crl());
        await using SmtpServerFake server = SmtpServerFake.Start(SmtpTls.Implicit, intermediate.Issue(SmtpHarness.Host, TimeSpan.FromDays(6)), intermediate.Certificate);
        using IHost host = await SmtpHarness.StartAsync(SmtpHarness.Configuration(server.Port, SmtpTls.Implicit, authority));

        Assert.Equal(new Attempt.Accepted(null), await SmtpHarness.SendAsync(host));
    }

    [Theory]
    [MemberData(nameof(Answers))]
    public async Task SendAsync_Answer_IsClassedAsTheTableGives(SmtpTls tls, string command, string reply, string kind, AttemptReason reason, int? status)
    {
        using TestAuthority authority = TestAuthority.Create();
        await using SmtpServerFake server = SmtpServerFake.Start(tls, authority.Issue(SmtpHarness.Host, TimeSpan.FromDays(6)));
        server.Replies[command] = reply;
        using IHost host = await SmtpHarness.StartAsync(SmtpHarness.Configuration(server.Port, tls, authority));

        Attempt attempt = await SmtpHarness.SendAsync(host);

        Attempt expected = kind == nameof(Attempt.Permanent) ? new Attempt.Permanent(reason, status) : new Attempt.Transient(reason, status, null);
        Assert.Equal(expected, attempt);
        Assert.Empty(server.Messages);
    }

    [Fact]
    public async Task SendAsync_ServerOfferingNoAuthentication_AnswersAuthFailedAndSendsNothing()
    {
        using TestAuthority authority = TestAuthority.Create();
        await using SmtpServerFake server = SmtpServerFake.Start(SmtpTls.Implicit, authority.Issue(SmtpHarness.Host, TimeSpan.FromDays(6)));
        server.OffersAuthentication = false;
        using IHost host = await SmtpHarness.StartAsync(SmtpHarness.Configuration(server.Port, SmtpTls.Implicit, authority));

        Assert.Equal(new Attempt.Transient(AttemptReason.AuthFailed, null, null), await SmtpHarness.SendAsync(host));
        Assert.DoesNotContain("MAIL", server.Commands);
    }

    [Fact]
    public async Task SendAsync_NoAnswerToTheEndOfTheData_AnswersTimeout()
    {
        using TestAuthority authority = TestAuthority.Create();
        await using SmtpServerFake server = SmtpServerFake.Start(SmtpTls.Implicit, authority.Issue(SmtpHarness.Host, TimeSpan.FromDays(6)));
        server.HangsAtEnd = true;
        Dictionary<string, string?> configuration = SmtpHarness.Configuration(server.Port, SmtpTls.Implicit, authority);
        configuration["Hermes:Dispatch:Timeout"] = "00:00:01";
        using IHost host = await SmtpHarness.StartAsync(configuration);

        Assert.Equal(new Attempt.Transient(AttemptReason.Timeout, null, null), await SmtpHarness.SendAsync(host));
    }

    [Fact]
    public async Task SendAsync_ConnectionRefused_AnswersConnectionFailed()
    {
        using TestAuthority authority = TestAuthority.Create();
        using IHost host = await SmtpHarness.StartAsync(SmtpHarness.Configuration(SmtpServerFake.UnusedPort(), SmtpTls.Implicit, authority));

        Assert.Equal(new Attempt.Transient(AttemptReason.ConnectionFailed, null, null), await SmtpHarness.SendAsync(host));
    }

    [Fact]
    public async Task SendAsync_RecipientRefused_LeavesTheConnectionForTheNextMessage()
    {
        using TestAuthority authority = TestAuthority.Create();
        await using SmtpServerFake server = SmtpServerFake.Start(SmtpTls.Implicit, authority.Issue(SmtpHarness.Host, TimeSpan.FromDays(6)));
        server.Replies["RCPT"] = "550 5.1.1 No such user";
        using IHost host = await SmtpHarness.StartAsync(SmtpHarness.Configuration(server.Port, SmtpTls.Implicit, authority));
        IEmailSession session = await host.Services.GetRequiredService<IEmailProvider>().OpenAsync(TestContext.Current.CancellationToken);
        await using (session)
        {
            Assert.Equal(new Attempt.Permanent(AttemptReason.RecipientRefused, 550), await session.SendAsync(SmtpHarness.Message(), TestContext.Current.CancellationToken));
            server.Replies.TryRemove("RCPT", out _);

            Assert.Equal(new Attempt.Accepted(null), await session.SendAsync(SmtpHarness.Message(), TestContext.Current.CancellationToken));
        }

        Assert.Equal(1, server.Connections);
        Assert.Single(server.Messages);
    }

    [Fact]
    public async Task SendAsync_Account_AuthenticatesWithItsHighestVersion()
    {
        using TestAuthority authority = TestAuthority.Create();
        await using SmtpServerFake server = SmtpServerFake.Start(SmtpTls.Implicit, authority.Issue(SmtpHarness.Host, TimeSpan.FromDays(6)));
        SecretSourceFake secrets = ChannelsHost.Secrets().WithVersions(
            "emailSubmission",
            (2, ChannelsHost.Account(ChannelsHost.User, ChannelsHost.Password)),
            (1, ChannelsHost.Account("former", ChannelsHost.Password + "0")));
        using IHost host = await SmtpHarness.StartAsync(SmtpHarness.Configuration(server.Port, SmtpTls.Implicit, authority), secrets);

        await SmtpHarness.SendAsync(host);

        Assert.Equal((ChannelsHost.User, ChannelsHost.Password), Assert.Single(server.Authentications));
    }

    [Fact]
    public async Task SendAsync_Message_GoesFromItsSenderWithTheHeadersOfMsgEmail003()
    {
        using TestAuthority authority = TestAuthority.Create();
        await using SmtpServerFake server = SmtpServerFake.Start(SmtpTls.Implicit, authority.Issue(SmtpHarness.Host, TimeSpan.FromDays(6)));
        using IHost host = await SmtpHarness.StartAsync(SmtpHarness.Configuration(server.Port, SmtpTls.Implicit, authority));
        EmailMessage message = SmtpHarness.Message() with
        {
            ReplyTo = new EmailAddress { Address = "support@example.com" },
            Subject = "Ihre Bestellung ist unterwegs: Grüße",
        };

        await SmtpHarness.SendAsync(host, message);

        SmtpReceived received = Assert.Single(server.Messages);
        using MimeMessage mime = SmtpHarness.Read(received);
        Assert.Equal("<" + ChannelsHost.FromAddress + ">", received.MailFrom.Split(' ')[0]);
        Assert.Equal(["<" + SmtpHarness.ToAddress + ">"], received.Recipients);
        MailboxAddress from = Assert.IsType<MailboxAddress>(Assert.Single(mime.From));
        Assert.Equal(ChannelsHost.FromAddress, from.Address);
        Assert.Equal("Order notices", from.Name);
        Assert.Equal("support@example.com", Assert.IsType<MailboxAddress>(Assert.Single(mime.ReplyTo)).Address);
        Assert.Equal(SmtpHarness.ToAddress, Assert.IsType<MailboxAddress>(Assert.Single(mime.To)).Address);
        Assert.Equal(message.Subject, mime.Subject);
        Assert.Equal(message.MessageId, mime.MessageId);
        Assert.Equal("auto-generated", mime.Headers["Auto-Submitted"]);
    }

    [Fact]
    public async Task SendAsync_EachAttempt_DatesTheMessageAsItIsSubmitted()
    {
        using TestAuthority authority = TestAuthority.Create();
        await using SmtpServerFake server = SmtpServerFake.Start(SmtpTls.Implicit, authority.Issue(SmtpHarness.Host, TimeSpan.FromDays(6)));
        DateTimeOffset start = new(DateTimeOffset.UtcNow.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, TimeSpan.Zero);
        ManualClock clock = new(start);
        using IHost host = await SmtpHarness.StartAsync(SmtpHarness.Configuration(server.Port, SmtpTls.Implicit, authority), time: clock);
        EmailMessage message = SmtpHarness.Message();

        await SmtpHarness.SendAsync(host, message);
        clock.Advance(TimeSpan.FromHours(2));
        await SmtpHarness.SendAsync(host, message);

        DateTimeOffset[] dates = [.. server.Messages.Select(received =>
        {
            using MimeMessage mime = SmtpHarness.Read(received);
            return mime.Date;
        })];
        Assert.Equal([start, start.AddHours(2)], dates);
    }

    [Fact]
    public async Task SendAsync_WithHtml_SendsTheTextFirstAndTheHtmlAsItsAlternative()
    {
        using TestAuthority authority = TestAuthority.Create();
        await using SmtpServerFake server = SmtpServerFake.Start(SmtpTls.Implicit, authority.Issue(SmtpHarness.Host, TimeSpan.FromDays(6)));
        using IHost host = await SmtpHarness.StartAsync(SmtpHarness.Configuration(server.Port, SmtpTls.Implicit, authority));
        const string Html = "<p>Your order <b>1042</b> has shipped. <a href=\"https://shop.example.com/orders/1042\">Track it</a></p>";

        await SmtpHarness.SendAsync(host, SmtpHarness.Message(Html));

        using MimeMessage mime = SmtpHarness.Read(Assert.Single(server.Messages));
        MultipartAlternative alternative = Assert.IsType<MultipartAlternative>(mime.Body);
        Assert.Equal(2, alternative.Count);
        TextPart text = Assert.IsType<TextPart>(alternative[0]);
        TextPart html = Assert.IsType<TextPart>(alternative[1]);
        Assert.True(text.IsPlain);
        Assert.True(html.IsHtml);
        Assert.Equal("utf-8", text.ContentType.Charset, ignoreCase: true);
        Assert.Equal("utf-8", html.ContentType.Charset, ignoreCase: true);
        Assert.Equal("Your order 1042 has shipped.", text.Text);
        Assert.Equal(Html, html.Text);
    }

    [Fact]
    public async Task SendAsync_WithoutHtml_SendsTheTextAlone()
    {
        using TestAuthority authority = TestAuthority.Create();
        await using SmtpServerFake server = SmtpServerFake.Start(SmtpTls.Implicit, authority.Issue(SmtpHarness.Host, TimeSpan.FromDays(6)));
        using IHost host = await SmtpHarness.StartAsync(SmtpHarness.Configuration(server.Port, SmtpTls.Implicit, authority));

        await SmtpHarness.SendAsync(host, SmtpHarness.Message() with { Text = "Ihre Bestellung 1042 ist unterwegs. Grüße" });

        using MimeMessage mime = SmtpHarness.Read(Assert.Single(server.Messages));
        TextPart text = Assert.IsType<TextPart>(mime.Body);
        Assert.True(text.IsPlain);
        Assert.Equal("utf-8", text.ContentType.Charset, ignoreCase: true);

        // SMTP ends the data with a line end, which a part alone keeps.
        Assert.Equal("Ihre Bestellung 1042 ist unterwegs. Grüße\r\n", text.Text);
    }

    private static async Task<List<Attempt>> SendAllAsync(IHost host, int count)
    {
        List<Attempt> attempts = [];
        IEmailSession session = await host.Services.GetRequiredService<IEmailProvider>().OpenAsync(TestContext.Current.CancellationToken);
        await using (session)
        {
            for (int index = 0; index < count; index++)
            {
                attempts.Add(await session.SendAsync(SmtpHarness.Message(), TestContext.Current.CancellationToken));
            }
        }

        return attempts;
    }
}