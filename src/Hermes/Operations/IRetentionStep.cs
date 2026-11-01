using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hermes.Operations;

/// <summary>
/// A step of the retention job a package registers for rows whose periods are its own, as
/// conversations' are (<c>06</c> MSG-OPS-004; <c>09</c> MSG-CONV-010); the job runs it after
/// its own deletions and before it protects values again.
/// </summary>
internal interface IRetentionStep
{
    /// <summary>
    /// Deletes what is past its period at <paramref name="now"/>, renewing the job's claim with
    /// <paramref name="renew"/> between batches; false where a renewal finds the claim no longer
    /// the run's.
    /// </summary>
    Task<bool> RunAsync(DateTimeOffset now, Func<Task<bool>> renew, CancellationToken cancellationToken);
}