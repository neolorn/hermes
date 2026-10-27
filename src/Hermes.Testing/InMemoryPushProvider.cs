using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

namespace Hermes.Testing;

/// <summary>A push provider for <c>webpush</c> that keeps every request it is given, its payload unencrypted, and answers each as the test sets (<c>04</c> MSG-LIB-008).</summary>
public sealed class InMemoryPushProvider : IPushProvider
{
    private readonly Lock gate = new();

    private readonly List<PushRequest> received = [];

    private Func<PushRequest, Attempt> answer = _ => new Attempt.Accepted(null);

    /// <inheritdoc />
    public PushPlatform Platform => PushPlatform.WebPush;

    /// <summary>Every request the provider was given, in order, whatever it answered.</summary>
    public IReadOnlyList<PushRequest> Received
    {
        get
        {
            lock (gate)
            {
                return [.. received];
            }
        }
    }

    /// <summary>Sets how each later request is answered; by default every request is <see cref="Attempt.Accepted"/> with no reference.</summary>
    /// <param name="answer">Answers a request.</param>
    /// <exception cref="ArgumentNullException"><paramref name="answer"/> is null.</exception>
    public void AnswerWith(Func<PushRequest, Attempt> answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        lock (gate)
        {
            this.answer = answer;
        }
    }

    /// <inheritdoc />
    public ValueTask<Attempt> SendAsync(PushRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        Func<PushRequest, Attempt> current;
        lock (gate)
        {
            received.Add(request);
            current = answer;
        }

        return ValueTask.FromResult(current(request));
    }
}