using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Channels;
using Hermes.Core;

namespace Hermes.Testing;

/// <summary>
/// An SMS provider that keeps every message it is given and answers each as the test sets,
/// answers the balance and reads the reports as the test sets, and judges a sender name by
/// the rule of <c>02</c> MSG-SMS-002 (<c>04</c> MSG-LIB-008).
/// </summary>
public sealed class InMemorySmsProvider : ISmsProvider
{
    private readonly Lock gate = new();

    private readonly List<SmsMessage> received = [];

    private Func<SmsMessage, Attempt> answer = _ => new Attempt.Accepted(null);

    private BalanceReading balance = new BalanceReading.NotSupported();

    private Func<SmsReportRequest, SmsReportReading> reader = _ => new SmsReportReading
    {
        Reads = false,
        Response = new SmsReportResponse { Status = 200, Body = ReadOnlyMemory<byte>.Empty },
    };

    /// <summary>Every message the provider was given, in order, whatever it answered.</summary>
    public IReadOnlyList<SmsMessage> Received
    {
        get
        {
            lock (gate)
            {
                return [.. received];
            }
        }
    }

    /// <summary>Sets how each later message is answered; by default every message is <see cref="Attempt.Accepted"/> with no reference.</summary>
    /// <param name="answer">Answers a message.</param>
    /// <exception cref="ArgumentNullException"><paramref name="answer"/> is null.</exception>
    public void AnswerWith(Func<SmsMessage, Attempt> answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        lock (gate)
        {
            this.answer = answer;
        }
    }

    /// <summary>Sets the balance each later reading answers; by default <see cref="BalanceReading.NotSupported"/>.</summary>
    /// <param name="balance">The reading.</param>
    /// <exception cref="ArgumentNullException"><paramref name="balance"/> is null.</exception>
    public void SetBalance(BalanceReading balance)
    {
        ArgumentNullException.ThrowIfNull(balance);
        lock (gate)
        {
            this.balance = balance;
        }
    }

    /// <summary>Sets how each later report is read; by default a reading that does not read, answered 200 with an empty body.</summary>
    /// <param name="reader">Reads a report.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    public void ReadReportWith(Func<SmsReportRequest, SmsReportReading> reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        lock (gate)
        {
            this.reader = reader;
        }
    }

    /// <inheritdoc />
    public ValueTask<Attempt> SendAsync(SmsMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();
        Func<SmsMessage, Attempt> current;
        lock (gate)
        {
            received.Add(message);
            current = answer;
        }

        return ValueTask.FromResult(current(message));
    }

    /// <inheritdoc />
    public bool IsValidSender(string sender)
    {
        ArgumentNullException.ThrowIfNull(sender);
        return SenderRule.IsSender(sender);
    }

    /// <inheritdoc />
    public ValueTask<BalanceReading> BalanceAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            return ValueTask.FromResult(balance);
        }
    }

    /// <inheritdoc />
    public ValueTask<SmsReportReading> ReadReportAsync(SmsReportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        Func<SmsReportRequest, SmsReportReading> current;
        lock (gate)
        {
            current = reader;
        }

        return ValueTask.FromResult(current(request));
    }
}