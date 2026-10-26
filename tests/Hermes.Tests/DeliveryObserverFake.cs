using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

namespace Hermes.Tests;

/// <summary>An observer that keeps every notice it takes.</summary>
internal sealed class DeliveryObserverFake : IDeliveryObserver
{
    internal ConcurrentQueue<DeliveryNotice> Notices { get; } = new();

    public ValueTask ObserveAsync(DeliveryNotice notice, CancellationToken cancellationToken)
    {
        Notices.Enqueue(notice);
        return ValueTask.CompletedTask;
    }
}