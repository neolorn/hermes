using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Operations;

using Microsoft.Extensions.Options;

namespace Hermes.Sending;

/// <summary>Runs one pass of each dispatched channel, side by side, answering the deliveries they took (<c>01</c> MSG-SEND-007; <c>04</c> MSG-LIB-002).</summary>
internal sealed class Dispatcher(MessagingReady ready, IOptionsMonitor<MessagingOptions> options, DispatchPasses passes) : IDispatcher
{
    public async ValueTask<int> RunPassAsync(CancellationToken cancellationToken)
    {
        ready.ThrowIfNotReady();
        if (!options.CurrentValue.Dispatch.Enabled)
        {
            throw new InvalidOperationException("This application does not dispatch: Hermes:Dispatch:Enabled is off.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        PassOutcome[] outcomes = await Task.WhenAll(DispatchPasses.Dispatched.Select(channel => passes.RunAsync(channel, cancellationToken))).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return outcomes.Sum(outcome => outcome.Took);
    }
}