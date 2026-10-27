using System;

using Hermes.Channels;

namespace Hermes.Sending;

/// <summary>The values of <c>Hermes:Sms</c> the balance is judged by (<c>02</c> MSG-SMS-006), as <see cref="Of"/> reads them.</summary>
/// <param name="DrainWindow">The window whose spend is judged.</param>
/// <param name="DrainFactor">How many times the baseline's mean spend per window a drain exceeds.</param>
/// <param name="DrainBaseline">The trailing span the mean is taken over.</param>
/// <param name="DrainMinHistory">How long the readings span before a drain is judged.</param>
/// <param name="DrainMinSpend">The least spend a drain has.</param>
/// <param name="BalanceFloor">The floor.</param>
/// <param name="FloorWarning">How soon the floor would be reached for the balance to be low.</param>
internal sealed record BalanceRules(
    TimeSpan DrainWindow,
    double DrainFactor,
    TimeSpan DrainBaseline,
    TimeSpan DrainMinHistory,
    decimal DrainMinSpend,
    decimal BalanceFloor,
    TimeSpan FloorWarning)
{
    /// <summary>The rules <paramref name="sms"/> gives.</summary>
    internal static BalanceRules Of(SmsChannelOptions sms)
    {
        return new BalanceRules(sms.DrainWindow, sms.DrainFactor, sms.DrainBaseline, sms.DrainMinHistory, sms.DrainMinSpend, sms.BalanceFloor, sms.FloorWarning);
    }
}