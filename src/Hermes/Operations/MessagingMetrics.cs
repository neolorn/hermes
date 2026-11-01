using System;
using System.Diagnostics.Metrics;

namespace Hermes.Operations;

/// <summary>The meter <c>Hermes</c> and its instruments (<c>06</c> MSG-OPS-005; <c>08</c> section 8).</summary>
internal sealed class MessagingMetrics : IDisposable
{
    internal const string MeterName = "Hermes";

    private readonly Meter meter;

    public MessagingMetrics(IMeterFactory factory)
    {
        meter = factory.Create(MeterName);
        Deliveries = meter.CreateCounter<long>("hermes.deliveries", "{delivery}", "Each final status a delivery reaches, and each move from sent");
        Attempts = meter.CreateCounter<long>("hermes.attempts", "{attempt}", "Each attempt, by its class and reason, an attempt answered at a check included");
        OldestDueAge = meter.CreateGauge<double>("hermes.dispatch.oldest_due_age", "s", "How long ago the oldest due delivery waiting or inflight became due");
        ProviderDuration = meter.CreateHistogram<double>("hermes.provider.duration", "s", "Each provider call's duration");
        ConversationsStarted = meter.CreateCounter<long>("hermes.conversations.started", "{conversation}", "Each support conversation created, offline ones included");
        ConversationsClosed = meter.CreateCounter<long>("hermes.conversations.closed", "{conversation}", "Each close of a support conversation, by staff, by the visitor or automatically");
    }

    /// <summary>Tagged <c>channel</c> and <c>status</c>.</summary>
    internal Counter<long> Deliveries { get; }

    public void Dispose()
    {
        meter.Dispose();
    }

    /// <summary>Tagged <c>channel</c>, <c>class</c> and <c>reason</c>.</summary>
    internal Counter<long> Attempts { get; }

    /// <summary>Tagged <c>channel</c>; recorded by the watch job each time it judges <c>dispatch-lag</c>.</summary>
    internal Gauge<double> OldestDueAge { get; }

    /// <summary>Tagged <c>channel</c> and <c>class</c>.</summary>
    internal Histogram<double> ProviderDuration { get; }

    /// <summary>Tagged <c>topic</c> (<c>09</c> MSG-CONV-005).</summary>
    internal Counter<long> ConversationsStarted { get; }

    /// <summary>Tagged <c>topic</c> (<c>09</c> MSG-CONV-005).</summary>
    internal Counter<long> ConversationsClosed { get; }
}