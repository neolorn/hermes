using System.Collections.Generic;
using System.Threading.Tasks;

using Hermes.Core;

using Xunit;

namespace Hermes.Sms.Cequens.Tests;

[Trait("Category", "unit")]
public sealed class CequensReportTests
{
    private const string MessageId = "0461f8f4-4c4f-4dc0-b7ed-097f4f9f85f4";

    [Theory]
    [InlineData("1", SmsReportOutcome.Delivered)]
    [InlineData("2", SmsReportOutcome.Failed)]
    [InlineData("3", SmsReportOutcome.NotFinal)]
    public async Task MSG_SMS_007_AC4_ReportReadsItsOutcomeWithTheProviderReferenceMsgid(string status, SmsReportOutcome outcome)
    {
        await using CequensHarness harness = await CequensHarness.StartAsync();

        SmsReportReading reading = await harness.Provider.ReadReportAsync(Report("GET", Query(MessageId, status)), TestContext.Current.CancellationToken);

        Assert.True(reading.Reads);
        Assert.Equal(MessageId, reading.ProviderReference);
        Assert.Null(reading.DeliveryReference);
        Assert.Equal(outcome, reading.Outcome);
        AssertEmpty200(reading.Response);
    }

    [Fact]
    public async Task MSG_SMS_007_AC4_ReportWithoutMsgidDoesNotRead()
    {
        await using CequensHarness harness = await CequensHarness.StartAsync();

        SmsReportReading reading = await harness.Provider.ReadReportAsync(Report("GET", Query(null, "1")), TestContext.Current.CancellationToken);

        Assert.False(reading.Reads);
        AssertEmpty200(reading.Response);
    }

    [Fact]
    public async Task MSG_SMS_007_AC4_ReportWithoutStatusidDoesNotRead()
    {
        await using CequensHarness harness = await CequensHarness.StartAsync();

        SmsReportReading reading = await harness.Provider.ReadReportAsync(Report("GET", Query(MessageId, null)), TestContext.Current.CancellationToken);

        Assert.False(reading.Reads);
        AssertEmpty200(reading.Response);
    }

    [Fact]
    public async Task ReadReportAsync_PostedReport_ReadsItsQuery()
    {
        await using CequensHarness harness = await CequensHarness.StartAsync();

        SmsReportReading reading = await harness.Provider.ReadReportAsync(Report("POST", Query(MessageId, "1")), TestContext.Current.CancellationToken);

        Assert.True(reading.Reads);
        Assert.Equal(SmsReportOutcome.Delivered, reading.Outcome);
        AssertEmpty200(reading.Response);
    }

    [Fact]
    public async Task ReadReportAsync_MsgidGivenTwice_DoesNotRead()
    {
        await using CequensHarness harness = await CequensHarness.StartAsync();
        List<KeyValuePair<string, string>> query = [.. Query(MessageId, "1"), new("msgid", "another")];

        SmsReportReading reading = await harness.Provider.ReadReportAsync(Report("GET", query), TestContext.Current.CancellationToken);

        Assert.False(reading.Reads);
    }

    [Fact]
    public async Task ReadReportAsync_ReportRead_MakesNoRequestToTheGateway()
    {
        await using CequensHarness harness = await CequensHarness.StartAsync();

        await harness.Provider.ReadReportAsync(Report("GET", Query(MessageId, "1")), TestContext.Current.CancellationToken);

        Assert.Empty(harness.Gateway.Requests);
    }

    /// <summary>A report's query as Cequens's DLR webhook documents it, <c>msgid</c> or <c>statusid</c> left out where none.</summary>
    private static List<KeyValuePair<string, string>> Query(string? messageId, string? status)
    {
        List<KeyValuePair<string, string>> query = [new("ClientMsgID", "0")];
        if (messageId is not null)
        {
            query.Add(new("msgid", messageId));
        }

        if (status is not null)
        {
            query.Add(new("statusid", status));
        }

        query.Add(new("from", CequensHarness.Sender));
        query.Add(new("to", "447700900123"));
        query.Add(new("accountid", "1"));
        query.Add(new("SubmitDate", "2026-11-05T09:00:00.000"));
        query.Add(new("statusmsg", "DELIVRD"));
        return query;
    }

    private static SmsReportRequest Report(string method, IReadOnlyList<KeyValuePair<string, string>> query)
    {
        return new SmsReportRequest { Method = method, Query = query, Form = [], Headers = [], Body = System.ReadOnlyMemory<byte>.Empty };
    }

    private static void AssertEmpty200(SmsReportResponse response)
    {
        Assert.Equal(200, response.Status);
        Assert.Null(response.ContentType);
        Assert.True(response.Body.IsEmpty);
    }
}