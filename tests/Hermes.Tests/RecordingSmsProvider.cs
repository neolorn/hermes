using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

namespace Hermes.Tests;

/// <summary>An SMS provider that keeps each message it was given and answers as a test sets, after the test's step before each call where it sets one.</summary>
internal sealed class RecordingSmsProvider : ISmsProvider
{
    private readonly Lock gate = new();

    private readonly List<SmsMessage> received = [];

    internal Func<SmsMessage, Attempt> Answer { get; set; } = _ => new Attempt.Accepted(null);

    internal Func<SmsMessage, CancellationToken, Task>? Before { get; set; }

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
        return ValueTask.FromResult<BalanceReading>(new BalanceReading.NotSupported());
    }

    public ValueTask<SmsReportReading> ReadReportAsync(SmsReportRequest request, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("The fake reads no report.");
    }
}