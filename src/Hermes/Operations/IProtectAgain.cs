using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hermes.Operations;

/// <summary>
/// A step of re-protection a package registers for a keyed table whose protected values lie
/// partly outside its rows, as an attachment's chunks lie in the store (<c>06</c>
/// MSG-OPS-008; <c>09</c> MSG-CONV-009); the retention job runs it after the tables it protects
/// again alone.
/// </summary>
internal interface IProtectAgain
{
    /// <summary>
    /// Protects again, under <paramref name="write"/>, what is protected under an older
    /// version, renewing the job's claim with <paramref name="renew"/> between batches; false
    /// where a renewal finds the claim no longer the run's.
    /// </summary>
    Task<bool> ProtectAgainAsync(int write, Func<Task<bool>> renew, CancellationToken cancellationToken);
}