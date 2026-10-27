using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hermes;

/// <summary>Runs the dispatcher's passes on demand (<c>01</c> MSG-SEND-007), whether or not the hosted dispatcher runs.</summary>
public interface IDispatcher
{
    /// <summary>
    /// Runs one pass of each channel, side by side: each claims its due deliveries within its
    /// rate, checks them, calls the provider and records each answer.
    /// </summary>
    /// <param name="cancellationToken">
    /// Stops the passes: each claims nothing more, hands back at once every delivery whose call
    /// has not begun, and lets a call in progress finish within <c>Hermes:Dispatch:Timeout</c>.
    /// </param>
    /// <returns>The number of deliveries the passes took.</returns>
    /// <exception cref="InvalidOperationException">This application does not dispatch: <c>Hermes:Dispatch:Enabled</c> is off.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled, after the passes stopped.</exception>
    ValueTask<int> RunPassAsync(CancellationToken cancellationToken);
}