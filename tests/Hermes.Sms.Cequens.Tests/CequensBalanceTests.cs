using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Hermes.Core;

using Xunit;

namespace Hermes.Sms.Cequens.Tests;

[Trait("Category", "unit")]
public sealed class CequensBalanceTests
{
    public static TheoryData<int, string?, BalanceReading> Answers { get; } = new()
    {
        { 401, GatewayAnswers.Unauthorized, new BalanceReading.Failed(AttemptReason.AuthFailed) },
        { 403, GatewayAnswers.Unauthorized, new BalanceReading.Failed(AttemptReason.AuthFailed) },
        { 429, null, new BalanceReading.Failed(AttemptReason.RateLimited) },
        { 200, "an answer that is not JSON", new BalanceReading.Failed(AttemptReason.ConnectionFailed) },
        { 400, GatewayAnswers.Refused(-2, "Invalid Data."), new BalanceReading.Failed(AttemptReason.Temporary) },
        { 500, null, new BalanceReading.Failed(AttemptReason.Temporary) },
    };

    [Fact]
    public async Task MSG_SMS_007_AC5_BalanceReadsTheAccountsCredit()
    {
        await using CequensHarness harness = await CequensHarness.StartAsync();
        harness.Gateway.Body = GatewayAnswers.Balance(1250.75m);

        BalanceReading reading = await harness.Provider.BalanceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new BalanceReading.Balance(1250.75m), reading);
        GatewayRequest request = Assert.Single(harness.Gateway.Requests);
        Assert.Equal("GET", request.Method);
        Assert.Equal("https://apis.cequens.com/sms/v1/account/balance", request.Address);
        Assert.StartsWith("Bearer ", request.Headers["Authorization"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task MSG_SMS_007_AC5_UnauthorizedBalanceIsFailedAuthFailed()
    {
        await using CequensHarness harness = await CequensHarness.StartAsync();
        harness.Gateway.Status = 401;
        harness.Gateway.Body = GatewayAnswers.Unauthorized;

        BalanceReading reading = await harness.Provider.BalanceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new BalanceReading.Failed(AttemptReason.AuthFailed), reading);
    }

    [Theory]
    [MemberData(nameof(Answers))]
    public async Task BalanceAsync_GatewayAnswer_IsReadAsMsgSms007Gives(int status, string? answer, BalanceReading expected)
    {
        await using CequensHarness harness = await CequensHarness.StartAsync();
        harness.Gateway.Status = status;
        harness.Gateway.Body = answer;

        BalanceReading reading = await harness.Provider.BalanceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(expected, reading);
    }

    [Theory]
    [InlineData(HttpRequestError.ConnectionError, AttemptReason.ConnectionFailed)]
    [InlineData(HttpRequestError.SecureConnectionError, AttemptReason.TlsFailed)]
    public async Task BalanceAsync_RequestFailsBeforeAnAnswer_IsFailedWithTheTablesReason(HttpRequestError error, AttemptReason reason)
    {
        await using CequensHarness harness = await CequensHarness.StartAsync(primary: () => new FailingHandler(error));

        BalanceReading reading = await harness.Provider.BalanceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new BalanceReading.Failed(reason), reading);
    }

    [Fact]
    public async Task BalanceAsync_ClientGivesUpBeforeAnAnswer_IsFailedTimeout()
    {
        await using CequensHarness harness = await CequensHarness.StartAsync(client: client => client.Timeout = TimeSpan.FromSeconds(1));
        harness.Gateway.Hangs = true;

        BalanceReading reading = await harness.Provider.BalanceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new BalanceReading.Failed(AttemptReason.Timeout), reading);
    }

    [Fact]
    public async Task BalanceAsync_SecretNoLongerAnswered_IsFailedKeyUnavailableAndMakesNoRequest()
    {
        await using CequensHarness harness = await CequensHarness.StartAsync();
        harness.Secrets.WithVersions(CequensToken.Name);

        BalanceReading reading = await harness.Provider.BalanceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new BalanceReading.Failed(AttemptReason.KeyUnavailable), reading);
        Assert.Empty(harness.Gateway.Requests);
    }

    [Fact]
    public async Task BalanceAsync_HighestVersionOutsideTheForm_IsFailedKeyUnavailableAndMakesNoRequest()
    {
        await using CequensHarness harness = await CequensHarness.StartAsync();
        harness.Secrets.WithVersions(CequensToken.Name, (1, []));

        BalanceReading reading = await harness.Provider.BalanceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new BalanceReading.Failed(AttemptReason.KeyUnavailable), reading);
        Assert.Empty(harness.Gateway.Requests);
    }

    [Theory]
    [InlineData(CequensSmsProvider.MaxBody, true)]
    [InlineData(CequensSmsProvider.MaxBody + 1, false)]
    public async Task BalanceAsync_AnswerBodyOfALength_ReadsOnlyUpTo64KiB(int length, bool reads)
    {
        await using CequensHarness harness = await CequensHarness.StartAsync();
        harness.Gateway.Body = GatewayAnswers.Padded(GatewayAnswers.Balance(12m), length);

        BalanceReading reading = await harness.Provider.BalanceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(reads ? new BalanceReading.Balance(12m) : new BalanceReading.Failed(AttemptReason.ConnectionFailed), reading);
    }

    [Fact]
    public async Task BalanceAsync_HighestVersion_IsTheBearerToken()
    {
        await using CequensHarness harness = await CequensHarness.StartAsync();
        byte[] highest = CequensHarness.Token();
        harness.Secrets.WithVersions(CequensToken.Name, (3, highest), (1, CequensHarness.Token()));
        harness.Gateway.Body = GatewayAnswers.Balance(0m);

        await harness.Provider.BalanceAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Bearer " + Encoding.ASCII.GetString(highest), Assert.Single(harness.Gateway.Requests).Headers["Authorization"]);
    }
}