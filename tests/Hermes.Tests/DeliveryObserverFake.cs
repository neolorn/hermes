using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

namespace Hermes.Tests;

/// <summary>An observer that keeps every notice it is called with and every notice it takes.</summary>
internal sealed class DeliveryObserverFake : IDeliveryObserver
{
    /// <summary>Every notice it was called with, taken or not.</summary>
    internal ConcurrentQueue<DeliveryNotice> Calls { get; } = new();

    /// <summary>The notices it took.</summary>
    internal ConcurrentQueue<DeliveryNotice> Notices { get; } = new();

    /// <summary>Runs before a notice is taken; a throw leaves it not taken.</summary>
    internal Func<DeliveryNotice, CancellationToken, Task>? Before { get; set; }

    public async ValueTask ObserveAsync(DeliveryNotice notice, CancellationToken cancellationToken)
    {
        Calls.Enqueue(notice);
        if (Before is { } before)
        {
            await before(notice, cancellationToken);
        }

        Notices.Enqueue(notice);
    }
}