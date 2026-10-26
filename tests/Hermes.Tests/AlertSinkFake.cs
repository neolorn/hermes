using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hermes.Tests;

/// <summary>An alert sink that keeps every call it takes, in order.</summary>
internal sealed class AlertSinkFake : Core.IAlertSink
{
    internal ConcurrentQueue<AlertCall> Calls { get; } = new();

    public ValueTask RaiseAsync(string condition, string? scope, IReadOnlyDictionary<string, string> details, CancellationToken cancellationToken)
    {
        Calls.Enqueue(new AlertCall(true, condition, scope, details));
        return ValueTask.CompletedTask;
    }

    public ValueTask ClearAsync(string condition, string? scope, CancellationToken cancellationToken)
    {
        Calls.Enqueue(new AlertCall(false, condition, scope, null));
        return ValueTask.CompletedTask;
    }

    /// <summary>One call.</summary>
    /// <param name="Raised">Whether it raised the condition, or cleared it.</param>
    /// <param name="Condition">The condition.</param>
    /// <param name="Scope">The scope, or none.</param>
    /// <param name="Details">A raise's details.</param>
    internal sealed record AlertCall(bool Raised, string Condition, string? Scope, IReadOnlyDictionary<string, string>? Details);
}