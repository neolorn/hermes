using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

namespace Hermes.Testing;

/// <summary>An email provider that keeps every message it is given and answers each as the test sets (<c>04</c> MSG-LIB-008).</summary>
public sealed class InMemoryEmailProvider : IEmailProvider
{
    private readonly Lock gate = new();

    private readonly List<EmailMessage> received = [];

    private Func<EmailMessage, Attempt> answer = _ => new Attempt.Accepted(null);

    /// <summary>Every message the provider was given, in order, whatever it answered.</summary>
    public IReadOnlyList<EmailMessage> Received
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
    public void AnswerWith(Func<EmailMessage, Attempt> answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        lock (gate)
        {
            this.answer = answer;
        }
    }

    /// <inheritdoc />
    public ValueTask<IEmailSession> OpenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<IEmailSession>(Open());
    }

    /// <summary>A session over this provider, holding nothing of its own, which the engine disposes.</summary>
    private Session Open()
    {
        return new Session(this);
    }

    private Attempt Take(EmailMessage message)
    {
        Func<EmailMessage, Attempt> current;
        lock (gate)
        {
            received.Add(message);
            current = answer;
        }

        return current(message);
    }

    private sealed class Session(InMemoryEmailProvider provider) : IEmailSession
    {
        public ValueTask<Attempt> SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(message);
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(provider.Take(message));
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}