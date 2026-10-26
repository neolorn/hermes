using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using Npgsql;

namespace Hermes.Operations;

/// <summary>Runs one of the library's own transactions on its own connection (<c>07</c> MSG-ENG-005).</summary>
internal static class LibraryTransactions
{
    /// <summary>
    /// Runs <paramref name="work"/> in a transaction whose idle timeout is twice
    /// <c>Hermes:Dispatch:Timeout</c>, so the server ends a session that stalls inside it, and
    /// commits it; a run PostgreSQL aborts for a deadlock (40P01) or a serialization failure
    /// (40001) is run again, at most <c>Hermes:Database:Retries</c> times, after which the
    /// database's error goes through.
    /// </summary>
    internal static async Task<T> RunAsync<T>(MessagingDatabase database, MessagingOptions options, Func<MessagingContext, CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        string idle = ((long)(2 * options.Dispatch.Timeout).TotalMilliseconds).ToString(CultureInfo.InvariantCulture);
        for (int run = 0; ; run++)
        {
            MessagingContext context = database.Context();
            await using (context.ConfigureAwait(false))
            {
                try
                {
                    IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                    await using (transaction.ConfigureAwait(false))
                    {
                        await context.Database.ExecuteSqlRawAsync("SELECT set_config('idle_in_transaction_session_timeout', @idle, true)", [new NpgsqlParameter("idle", idle)], cancellationToken).ConfigureAwait(false);
                        T result = await work(context, cancellationToken).ConfigureAwait(false);
                        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                        return result;
                    }
                }
                catch (Exception aborted) when (run < options.Database.Retries && IsRunAgain(aborted))
                {
                }
            }
        }
    }

    private static bool IsRunAgain(Exception failure)
    {
        for (Exception? cause = failure; cause is not null; cause = cause.InnerException)
        {
            if (cause is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected })
            {
                return true;
            }
        }

        return false;
    }
}