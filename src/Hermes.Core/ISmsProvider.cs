using System.Threading;
using System.Threading.Tasks;

namespace Hermes.Core;

/// <summary>
/// The SMS channel's provider (<c>02</c> MSG-SMS-002 to MSG-SMS-006). A host replaces the
/// shipped one by registering its own.
/// </summary>
public interface ISmsProvider
{
    /// <summary>Sends <paramref name="message"/>.</summary>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The provider's answer; never text the provider answered.</returns>
    ValueTask<Attempt> SendAsync(SmsMessage message, CancellationToken cancellationToken);

    /// <summary>
    /// Whether the provider can send under <paramref name="sender"/>; a provider may narrow
    /// <c>02</c> MSG-SMS-002's rule, never widen it, and the startup checks every declared
    /// sender against it.
    /// </summary>
    /// <param name="sender">A sender name or number.</param>
    /// <returns>Whether the sender is valid for this provider.</returns>
    bool IsValidSender(string sender);

    /// <summary>Reads the account's balance (<c>02</c> MSG-SMS-006).</summary>
    /// <param name="cancellationToken">Cancels the reading.</param>
    /// <returns>The balance in the provider's own unit, that the provider reports none, or that the reading failed.</returns>
    ValueTask<BalanceReading> BalanceAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Reads a delivery report whose key matched (<c>02</c> MSG-SMS-004). It reads none of the
    /// provider's keys or secrets, so it is called where the report group is mapped whether or
    /// not the provider sends there.
    /// </summary>
    /// <param name="request">The report request, whole.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    /// <returns>The reference the report names, its outcome and the response the provider expects, or that the report does not read.</returns>
    ValueTask<SmsReportReading> ReadReportAsync(SmsReportRequest request, CancellationToken cancellationToken);
}