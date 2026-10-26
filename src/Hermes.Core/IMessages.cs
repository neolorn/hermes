using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Hermes.Core;

/// <summary>Records sends, cancels their deliveries and reads them (<c>01</c> MSG-SEND-003, MSG-SEND-004, MSG-SEND-008, MSG-SEND-017).</summary>
public interface IMessages
{
    /// <summary>
    /// Records <paramref name="send"/> and its deliveries, one per channel, in a transaction of
    /// the library's own, and returns; nothing is handed to a provider inside the call.
    /// </summary>
    /// <param name="send">The send.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>
    /// The receipt; or a failure, recording nothing: <c>messaging.send.nodestination</c>,
    /// <c>messaging.send.destinationinvalid</c>, <c>messaging.send.destinationnotallowed</c>,
    /// <c>messaging.send.toolong</c>, <c>messaging.send.renderlimit</c>,
    /// <c>messaging.send.renderfailed</c>, <c>messaging.send.idempotencyconflict</c> or
    /// <c>messaging.key.unavailable</c>.
    /// </returns>
    /// <exception cref="System.ArgumentException">A fault of the calling code: a kind not declared or retired, a channel the kind does not declare, a value a template uses that the send does not pass, a value misnamed, named twice or under a reserved name, or a person reference, idempotency key or topic outside its bounds.</exception>
    /// <exception cref="System.InvalidOperationException">The library's startup checks have not completed, or the application does not record sends.</exception>
    ValueTask<Result<SendReceipt>> SendAsync(Send send, CancellationToken cancellationToken);

    /// <summary>
    /// Records <paramref name="send"/> on the connection of <paramref name="transaction"/>, inside
    /// it, committing nothing; the send's advisory locks are held until the host's transaction
    /// ends, one per <c>hashKeys</c> version for a person and one for an idempotency key, and its
    /// commit wakes the dispatchers.
    /// </summary>
    /// <param name="send">The send.</param>
    /// <param name="transaction">An open PostgreSQL transaction of the host's, on a database holding every migration of the library's release.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The receipt, or a failure as <see cref="SendAsync(Send, CancellationToken)"/> answers.</returns>
    /// <exception cref="System.ArgumentException">A fault as <see cref="SendAsync(Send, CancellationToken)"/> throws, or a transaction that is not an open PostgreSQL transaction.</exception>
    /// <exception cref="System.InvalidOperationException">As <see cref="SendAsync(Send, CancellationToken)"/> throws, or the transaction's database lacks a migration of the release.</exception>
    ValueTask<Result<SendReceipt>> SendAsync(Send send, DbTransaction transaction, CancellationToken cancellationToken);

    /// <summary>Ends each of the send's deliveries that is <c>waiting</c> as <c>cancelled</c>; one <c>inflight</c> or finished is left as it is.</summary>
    /// <param name="send">The send.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The deliveries cancelled; or <c>messaging.send.unknown</c> for a send the library does not hold.</returns>
    /// <exception cref="System.InvalidOperationException">The library's startup checks have not completed.</exception>
    ValueTask<Result<IReadOnlyList<DeliveryId>>> CancelAsync(SendId send, CancellationToken cancellationToken);

    /// <summary>Reads the send's deliveries, with their statuses, instants and attempts.</summary>
    /// <param name="send">The send.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The send's record; or <c>messaging.send.unknown</c> for a send the library does not hold.</returns>
    /// <exception cref="System.InvalidOperationException">The library's startup checks have not completed.</exception>
    ValueTask<Result<SendRecord>> ReadAsync(SendId send, CancellationToken cancellationToken);
}