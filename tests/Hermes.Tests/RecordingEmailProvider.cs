using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

namespace Hermes.Tests;

/// <summary>
/// An email provider, and the session it opens, that keeps each message it was given and
/// answers as a test sets, after the test's step before each call where it sets one.
/// </summary>
internal sealed class RecordingEmailProvider : IEmailProvider, IEmailSession
{
    private readonly Lock gate = new();

    private readonly List<EmailMessage> received = [];

    internal Func<EmailMessage, Attempt> Answer { get; set; } = _ => new Attempt.Accepted(null);

    internal Func<EmailMessage, CancellationToken, Task>? Before { get; set; }

    internal int Opened { get; private set; }

    internal IReadOnlyList<EmailMessage> Received
    {
        get
        {
            lock (gate)
            {
                return [.. received];
            }
        }
    }

    public ValueTask<IEmailSession> OpenAsync(CancellationToken cancellationToken)
    {
        lock (gate)
        {
            Opened++;
        }

        return ValueTask.FromResult<IEmailSession>(this);
    }

    public async ValueTask<Attempt> SendAsync(EmailMessage message, CancellationToken cancellationToken)
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

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}