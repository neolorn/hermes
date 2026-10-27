using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hermes.Testing;

/// <summary>Runs the library's passes on demand for a test (<c>04</c> MSG-LIB-008).</summary>
public sealed class TestDispatcher
{
    private readonly IDispatcher dispatcher;

    /// <summary>Runs the passes of <paramref name="dispatcher"/>, whose deliveries fall due by <paramref name="timeProvider"/>.</summary>
    /// <param name="dispatcher">The library's dispatcher.</param>
    /// <param name="timeProvider">The clock the library is registered with, which a test moves to bring a retry or a rate's next second.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public TestDispatcher(IDispatcher dispatcher, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(timeProvider);
        this.dispatcher = dispatcher;
    }

    /// <summary>
    /// Runs passes until a round of them, one of each channel, takes no delivery, so a channel
    /// whose rate the clock's current second has used up waits for the test to move the clock.
    /// </summary>
    /// <param name="cancellationToken">Stops the passes.</param>
    /// <returns>The number of deliveries the passes took.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public async ValueTask<int> RunUntilIdleAsync(CancellationToken cancellationToken)
    {
        int taken = 0;
        while (true)
        {
            int round = await dispatcher.RunPassAsync(cancellationToken).ConfigureAwait(false);
            if (round == 0)
            {
                return taken;
            }

            taken += round;
        }
    }
}