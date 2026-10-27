using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

namespace Hermes.Tests;

/// <summary>
/// An SMS provider that keeps each message it was given and answers as a test sets, after the
/// test's step before each call where it sets one. It keeps each report request it is passed
/// and reads it from the form fields <c>ref</c> (a delivery reference), <c>pid</c> (a provider
/// reference) and <c>status</c>, answering 200 with the body <c>OK</c> whatever it reads.
/// </summary>
internal sealed class RecordingSmsProvider : ISmsProvider
{
    internal static readonly SmsReportResponse Expected = new() { Status = 200, ContentType = "text/plain", Body = Encoding.ASCII.GetBytes("OK") };

    private readonly Lock gate = new();

    private readonly List<SmsMessage> received = [];

    private readonly List<SmsReportRequest> reports = [];

    internal Func<SmsMessage, Attempt> Answer { get; set; } = _ => new Attempt.Accepted(null);

    internal Func<SmsMessage, CancellationToken, Task>? Before { get; set; }

    internal Func<BalanceReading> Balance { get; set; } = () => new BalanceReading.NotSupported();

    internal IReadOnlyList<SmsMessage> Received
    {
        get
        {
            lock (gate)
            {
                return [.. received];
            }
        }
    }

    internal IReadOnlyList<SmsReportRequest> Reports
    {
        get
        {
            lock (gate)
            {
                return [.. reports];
            }
        }
    }

    public bool IsValidSender(string sender)
    {
        return true;
    }

    public async ValueTask<Attempt> SendAsync(SmsMessage message, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            received.Add(message);
        }

        if (Before is { } before)
        {
            await before(message, cancellationToken);
        }

        return Answer(message);
    }

    public ValueTask<BalanceReading> BalanceAsync(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(Balance());
    }

    public ValueTask<SmsReportReading> ReadReportAsync(SmsReportRequest request, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            reports.Add(request);
        }

        string? Field(string name)
        {
            return request.Form.Concat(request.Query).Where(field => field.Key == name).Select(field => field.Value).FirstOrDefault();
        }

        SmsReportOutcome? outcome = Field("status") switch
        {
            "delivered" => SmsReportOutcome.Delivered,
            "failed" => SmsReportOutcome.Failed,
            "notfinal" => SmsReportOutcome.NotFinal,
            _ => null,
        };
        return ValueTask.FromResult(new SmsReportReading
        {
            Reads = outcome is not null,
            DeliveryReference = Field("ref"),
            ProviderReference = Field("pid"),
            Outcome = outcome,
            Response = Expected,
        });
    }
}