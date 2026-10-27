using System;

namespace Hermes.Sending;

/// <summary>One reading of the SMS balance (<c>02</c> MSG-SMS-006): its instant and the balance, none where the read failed.</summary>
internal sealed class BalanceRow
{
    public required DateTimeOffset ReadAt { get; set; }

    /// <summary>The balance in the provider's own unit, or none for a failed read.</summary>
    public decimal? Units { get; set; }
}