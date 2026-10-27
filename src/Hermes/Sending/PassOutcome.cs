namespace Hermes.Sending;

/// <summary>What one pass of a channel did (<c>01</c> MSG-SEND-007).</summary>
/// <param name="Took">The deliveries it claimed.</param>
/// <param name="Limit">The most it could claim: the lesser of the batch and what the second's rate left.</param>
/// <param name="HeldForKey">Whether it held any of them for a key.</param>
/// <param name="Stopped">Whether the host stopped it.</param>
internal sealed record PassOutcome(int Took, int Limit, bool HeldForKey, bool Stopped)
{
    internal static PassOutcome None { get; } = new(0, 0, HeldForKey: false, Stopped: false);

    /// <summary>Whether it took its whole limit and held none for a key, so the channel's next pass starts at the next second.</summary>
    internal bool TookAll => Limit > 0 && Took == Limit && !HeldForKey;
}