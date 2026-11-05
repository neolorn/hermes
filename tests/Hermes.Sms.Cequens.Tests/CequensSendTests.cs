using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Hermes.Sms.Cequens.Tests;

[Trait("Category", "unit")]
public sealed class CequensSendTests
{
    /// <summary>Each row of MSG-SMS-007's answer table, from the gateway's documented answers.</summary>
    public static TheoryData<int, string?, Attempt> Answers { get; } = new()
    {
        { 200, GatewayAnswers.Sent, new Attempt.Accepted(GatewayAnswers.SmsId) },
        { 200, GatewayAnswers.SentToAnInvalidRecipient, new Attempt.Permanent(AttemptReason.RecipientRefused, 200) },
        { 200, GatewayAnswers.SentWithoutAnIdentifier, new Attempt.Permanent(AttemptReason.Permanent, 200) },
        { 400, GatewayAnswers.Refused(-6, "At least one valid recipient required"), new Attempt.Permanent(AttemptReason.RecipientRefused, 400) },
        { 400, GatewayAnswers.Refused(-12, "Invalid Sender Name"), new Attempt.Permanent(AttemptReason.SenderRefused, 400) },
        { 400, GatewayAnswers.Refused(-8, "Invalid Message Type, only [text] and [unicode] are supported."), new Attempt.Permanent(AttemptReason.MessageRefused, 400) },
        { 400, GatewayAnswers.Refused(-9, "Empty Message Text."), new Attempt.Permanent(AttemptReason.MessageRefused, 400) },
        { 400, GatewayAnswers.Refused(-41, "Message Text is greater than 20 parts"), new Attempt.Permanent(AttemptReason.MessageRefused, 400) },
        { 400, GatewayAnswers.Refused(-17, "Not Enough Credits."), new Attempt.Transient(AttemptReason.Temporary, 400, null) },
        { 400, GatewayAnswers.Refused(-16, "Invalid Delivery Time"), new Attempt.Permanent(AttemptReason.Permanent, 400) },
        { 400, GatewayAnswers.Refused(-13, "Flashing should be 0 or 1."), new Attempt.Permanent(AttemptReason.Permanent, 400) },
        { 400, GatewayAnswers.Refused(-14, "Acknowledgment should be 0 or 1."), new Attempt.Permanent(AttemptReason.Permanent, 400) },
        { 400, null, new Attempt.Permanent(AttemptReason.Permanent, 400) },
        { 404, null, new Attempt.Permanent(AttemptReason.Permanent, 404) },
        { 413, null, new Attempt.Permanent(AttemptReason.Permanent, 413) },
        { 401, GatewayAnswers.Unauthorized, new Attempt.Transient(AttemptReason.AuthFailed, 401, null) },
        { 403, GatewayAnswers.Unauthorized, new Attempt.Transient(AttemptReason.AuthFailed, 403, null) },
        { 429, null, new Attempt.Transient(AttemptReason.RateLimited, 429, null) },
        { 500, null, new Attempt.Transient(AttemptReason.Temporary, 500, null) },
        { 503, null, new Attempt.Transient(AttemptReason.Temporary, 503, null) },
        { 200, "an answer that is not JSON", new Attempt.Transient(AttemptReason.ConnectionFailed, 200, null) },
    };

    [Fact]
    public async Task MSG_SMS_007_AC1_SendMakesOnePostWithTheHighestTokenAndTheBodyAndIsAcceptedWithTheSmsId()
    {
        await using CequensHarness harness = await CequensHarness.StartAsync();
        byte[] highest = CequensHarness.Token();
        harness.Secrets.WithVersions(CequensToken.Name, (1, CequensHarness.Token()), (2, highest));
        Uri report = new("https://notices.example.com/notifications/sms/report/f0Jx2mQa9", UriKind.Absolute);

        Attempt attempt = await harness.Provider.SendAsync(CequensHarness.Message(reportAddress: report), TestContext.Current.CancellationToken);

        Assert.Equal(new Attempt.Accepted(GatewayAnswers.SmsId), attempt);
        GatewayRequest request = Assert.Single(harness.Gateway.Requests);
        Assert.Equal("POST", request.Method);
        Assert.Equal("https://apis.cequens.com/sms/v1/messages", request.Address);
        Assert.Equal("Bearer " + Encoding.ASCII.GetString(highest), request.Headers["Authorization"]);
        Assert.Equal("application/json", request.Headers["Accept"]);
        Assert.StartsWith("application/json", request.Headers["Content-Type"], StringComparison.Ordinal);
        using JsonDocument body = JsonDocument.Parse(request.Body);
        Assert.Equal(
            [("messageText", CequensHarness.Text), ("senderName", CequensHarness.Sender), ("messageType", "text"), ("recipients", "447700900123"), ("dlrUrl", report.AbsoluteUri)],
            body.RootElement.EnumerateObject().Select(member => (member.Name, member.Value.GetString())));
    }

    [Fact]
    public async Task MSG_SMS_007_AC1_SendWithoutAReportAddressCarriesNoDlrUrl()
    {
        await using CequensHarness harness = await CequensHarness.StartAsync();

        Attempt attempt = await harness.Provider.SendAsync(CequensHarness.Message(), TestContext.Current.CancellationToken);

        Assert.Equal(new Attempt.Accepted(GatewayAnswers.SmsId), attempt);
        using JsonDocument body = JsonDocument.Parse(Assert.Single(harness.Gateway.Requests).Body);
        Assert.Equal(["messageText", "senderName", "messageType", "recipients"], body.RootElement.EnumerateObject().Select(member => member.Name));
    }

    [Theory]
    [InlineData(SmsEncoding.Gsm7, "text")]
    [InlineData(SmsEncoding.Ucs2, "unicode")]
    public async Task MSG_SMS_007_AC1_EncodingIsSentAsItsMessageType(SmsEncoding encoding, string messageType)
    {
        await using CequensHarness harness = await CequensHarness.StartAsync();

        await harness.Provider.SendAsync(CequensHarness.Message(encoding: encoding), TestContext.Current.CancellationToken);

        using JsonDocument body = JsonDocument.Parse(Assert.Single(harness.Gateway.Requests).Body);
        Assert.Equal(messageType, body.RootElement.GetProperty("messageType").GetString());
    }

    [Theory]
    [MemberData(nameof(Answers))]
    public async Task MSG_SMS_007_AC2_EachDocumentedAnswerGivesItsAttempt(int status, string? answer, Attempt expected)
    {
        await using CequensHarness harness = await CequensHarness.StartAsync();
        harness.Gateway.Status = status;
        harness.Gateway.Body = answer;

        Attempt attempt = await harness.Provider.SendAsync(CequensHarness.Message(), TestContext.Current.CancellationToken);

        Assert.Equal(expected, attempt);
        Assert.Single(harness.Gateway.Requests);
    }

    [Fact]
    public async Task MSG_SMS_007_AC2_RefusedConnectionIsTransientConnectionFailed()
    {
        await using CequensHarness harness = await CequensHarness.StartAsync(primary: () => new FailingHandler(HttpRequestError.ConnectionError));

        Attempt attempt = await harness.Provider.SendAsync(CequensHarness.Message(), TestContext.Current.CancellationToken);

        Assert.Equal(new Attempt.Transient(AttemptReason.ConnectionFailed, null, null), attempt);
    }

    [Fact]
    public async Task MSG_SMS_007_AC2_TlsNotCompletedIsTransientTlsFailed()
    {
        await using CequensHarness harness = await CequensHarness.StartAsync(primary: () => new FailingHandler(HttpRequestError.SecureConnectionError));

        Attempt attempt = await harness.Provider.SendAsync(CequensHarness.Message(), TestContext.Current.CancellationToken);

        Assert.Equal(new Attempt.Transient(AttemptReason.TlsFailed, null, null), attempt);
    }

    [Fact]
    public async Task MSG_SMS_007_AC3_MessageOfTwentyOneSegmentsMakesNoRequestAndIsRefused()
    {
        await using CequensHarness harness = await CequensHarness.StartAsync();

        Attempt attempt = await harness.Provider.SendAsync(CequensHarness.Message(segments: 21), TestContext.Current.CancellationToken);

        Assert.Equal(new Attempt.Permanent(AttemptReason.MessageRefused, null), attempt);
        Assert.Empty(harness.Gateway.Requests);
    }

    [Fact]
    public async Task SendAsync_MessageOfTwentySegments_IsSent()
    {
        await using CequensHarness harness = await CequensHarness.StartAsync();

        Attempt attempt = await harness.Provider.SendAsync(CequensHarness.Message(segments: 20), TestContext.Current.CancellationToken);

        Assert.Equal(new Attempt.Accepted(GatewayAnswers.SmsId), attempt);
        Assert.Single(harness.Gateway.Requests);
    }

    [Fact]
    public async Task SendAsync_RateLimitedUnderAHostsRetryingHandler_IsAnsweredAfterOneRequest()
    {
        await using CequensHarness harness = await CequensHarness.StartAsync(host: services => services.ConfigureHttpClientDefaults(client => client.AddHttpMessageHandler(() => new RetryingHandler())));
        harness.Gateway.Status = 429;
        harness.Gateway.Body = null;

        Attempt attempt = await harness.Provider.SendAsync(CequensHarness.Message(), TestContext.Current.CancellationToken);

        Assert.Equal(new Attempt.Transient(AttemptReason.RateLimited, 429, null), attempt);
        Assert.Single(harness.Gateway.Requests);
    }

    [Fact]
    public async Task SendAsync_ClientGivesUpBeforeAnAnswer_IsTransientTimeout()
    {
        await using CequensHarness harness = await CequensHarness.StartAsync(client: client => client.Timeout = TimeSpan.FromSeconds(1));
        harness.Gateway.Hangs = true;

        Attempt attempt = await harness.Provider.SendAsync(CequensHarness.Message(), TestContext.Current.CancellationToken);

        Assert.Equal(new Attempt.Transient(AttemptReason.Timeout, null, null), attempt);
    }

    [Fact]
    public async Task SendAsync_SecretNoLongerAnswered_ThrowsAndMakesNoRequest()
    {
        await using CequensHarness harness = await CequensHarness.StartAsync();
        harness.Secrets.WithVersions(CequensToken.Name);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await harness.Provider.SendAsync(CequensHarness.Message(), TestContext.Current.CancellationToken));

        Assert.Empty(harness.Gateway.Requests);
    }

    [Theory]
    [InlineData("Notices")]
    [InlineData("Bakery Co.")]
    [InlineData("447700900123")]
    public async Task IsValidSender_SenderTheSenderRuleAllows_IsTrue(string sender)
    {
        await using CequensHarness harness = await CequensHarness.StartAsync();

        Assert.True(harness.Provider.IsValidSender(sender));
    }
}