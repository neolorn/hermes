using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Operations;

using Microsoft.Extensions.Options;

namespace Hermes.Sending;

/// <summary>
/// Runs one pass of each dispatched channel, side by side, answering the deliveries they took
/// (<c>01</c> MSG-SEND-007; <c>04</c> MSG-LIB-002); under <c>AddHermesTesting</c>, where the
/// <c>observer</c> job does not run, it then delivers the pending notices (<c>04</c> MSG-LIB-008).
/// </summary>
internal sealed class Dispatcher(MessagingReady ready, IOptionsMonitor<MessagingOptions> options, DispatchPasses passes, NoticeDelivery notices, IEnumerable<TestPasses> testing) : IDispatcher
{
    private readonly bool delivers = testing.Any();

    public async ValueTask<int> RunPassAsync(CancellationToken cancellationToken)
    {
        ready.ThrowIfNotReady();
        MessagingOptions current = options.CurrentValue;
        if (!current.Dispatch.Enabled)
        {
            throw new InvalidOperationException("This application does not dispatch: Hermes:Dispatch:Enabled is off.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        PassOutcome[] outcomes = await Task.WhenAll(DispatchPasses.Dispatched.Select(channel => passes.RunAsync(channel, cancellationToken))).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (delivers && current.Observer.Enabled)
        {
            await notices.DeliverAsync(null, cancellationToken).ConfigureAwait(false);
        }

        return outcomes.Sum(outcome => outcome.Took);
    }
}