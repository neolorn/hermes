using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

namespace Hermes.Tests;

/// <summary>A push provider that keeps each request it was given and answers as a test sets, accepting by default.</summary>
internal sealed class RecordingPushProvider : IPushProvider
{
    private readonly Lock gate = new();

    private readonly List<PushRequest.WebPush> received = [];

    public PushPlatform Platform => PushPlatform.WebPush;

    internal Func<PushRequest.WebPush, Attempt> Answer { get; set; } = _ => new Attempt.Accepted(null);

    /// <summary>Runs once a request is recorded and before it is answered.</summary>
    internal Func<PushRequest.WebPush, CancellationToken, Task>? Before { get; set; }

    internal IReadOnlyList<PushRequest.WebPush> Received
    {
        get
        {
            lock (gate)
            {
                return [.. received];
            }
        }
    }

    public async ValueTask<Attempt> SendAsync(PushRequest request, CancellationToken cancellationToken)
    {
        PushRequest.WebPush web = (PushRequest.WebPush)request;
        lock (gate)
        {
            received.Add(web);
        }

        if (Before is { } before)
        {
            await before(web, cancellationToken);
        }

        return Answer(web);
    }
}