using System.Threading;
using System.Threading.Tasks;

namespace Hermes.Core;

/// <summary>Erases what the library holds about a person, and answers what it holds for their export (<c>01</c> MSG-SEND-020).</summary>
public interface ISubjects
{
    /// <summary>
    /// Erases the person: cancels their waiting and inflight deliveries, deletes their
    /// preferences, subscriptions and idempotency rows, and deletes the destination, content,
    /// unsubscribe address and person reference of every delivery for them, all in one
    /// transaction under the person's locks taken exclusively, waiting for each lock no longer
    /// than <c>Hermes:Dispatch:Timeout</c>; it waits for every open transaction that recorded
    /// a send for the person, so a host never calls it while such a transaction of its own is
    /// open. A person the library holds nothing of is erased by doing nothing.
    /// </summary>
    /// <param name="person">The person reference.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success; or a failure, changing nothing: <c>messaging.release.newer</c> or <c>messaging.key.unavailable</c>.</returns>
    /// <exception cref="System.ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="System.InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">A lock was not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result> EraseAsync(string person, CancellationToken cancellationToken);

    /// <summary>
    /// Answers what the library holds for the person: their preferences, subscriptions, live
    /// idempotency rows and every delivery whose record names them, the values opened after
    /// the read, outside any transaction.
    /// </summary>
    /// <param name="person">The person reference.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The export; or a failure: <c>messaging.release.newer</c> or <c>messaging.key.unavailable</c>.</returns>
    /// <exception cref="System.ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="System.InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">A lock was not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result<SubjectExport>> ExportAsync(string person, CancellationToken cancellationToken);
}