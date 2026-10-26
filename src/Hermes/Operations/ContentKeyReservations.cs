using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Hermes.Operations;

/// <summary>
/// The values this process may still make under each <c>contentKeys</c> version, reserved in
/// blocks of <c>Hermes:Secrets:ContentKeys:ReserveBlock</c> by one atomic update of the
/// version's count in the key table, on the library's own connection, before it protects
/// under them, so a crash loses no count (<c>06</c> MSG-OPS-008).
/// </summary>
internal sealed class ContentKeyReservations(MessagingDatabase database, IOptionsMonitor<MessagingOptions> options) : IDisposable
{
    /// <summary>The values random 96-bit nonces allow one AES-GCM key (NIST SP 800-38D section 8.3).</summary>
    internal const long NonceLimit = 4_294_967_296L;

    /// <summary>Adds a block to a version's count unless the count would pass <see cref="NonceLimit"/>.</summary>
    private const string Reserve = """
        UPDATE messaging.key_versions SET value_count = value_count + @block
        WHERE key = 'contentKeys' AND version = @version AND value_count + @block <= @limit
        RETURNING value_count AS "Value"
        """;

    private readonly SemaphoreSlim gate = new(1, 1);

    private readonly Dictionary<int, long> remaining = [];

    public void Dispose()
    {
        gate.Dispose();
    }

    /// <summary>
    /// Takes one value under <paramref name="version"/>, reserving a block first where none is
    /// left; false where a block would take the version's count past <see cref="NonceLimit"/>.
    /// </summary>
    internal async Task<bool> TakeAsync(int version, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (remaining.GetValueOrDefault(version) == 0)
            {
                MessagingOptions current = options.CurrentValue;
                int block = current.Secrets.ContentKeys.ReserveBlock;
                List<long> counts = await LibraryTransactions.RunAsync(
                    database,
                    current,
                    (context, token) => context.Database.SqlQueryRaw<long>(
                        Reserve,
                        new NpgsqlParameter("block", (long)block),
                        new NpgsqlParameter("version", version),
                        new NpgsqlParameter("limit", NonceLimit)).ToListAsync(token),
                    cancellationToken).ConfigureAwait(false);
                if (counts.Count == 0)
                {
                    return false;
                }

                remaining[version] = block;
            }

            remaining[version]--;
            return true;
        }
        finally
        {
            gate.Release();
        }
    }
}