namespace Hermes.Core;

/// <summary>What reading an SMS account's balance answers (<c>02</c> MSG-SMS-006).</summary>
public abstract record BalanceReading
{
    private BalanceReading()
    {
    }

    /// <summary>The balance.</summary>
    /// <param name="Units">The balance in the provider's own unit, messages or money as it counts.</param>
    public sealed record Balance(decimal Units) : BalanceReading;

    /// <summary>The provider reports no balance, so it has no floor and raises no balance alert.</summary>
    public sealed record NotSupported() : BalanceReading;

    /// <summary>The reading failed.</summary>
    /// <param name="Reason">Why, a reason of <c>08</c> section 6.</param>
    public sealed record Failed(AttemptReason Reason) : BalanceReading;
}