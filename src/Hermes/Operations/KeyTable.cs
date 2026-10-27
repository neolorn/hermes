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
/// The key table's start statement and timer writes (<c>06</c> MSG-OPS-008), each on the
/// library's own connection and never from a send, so no host transaction touches the table.
/// </summary>
internal sealed class KeyTable(MessagingDatabase database, HeldKeys keys, IOptionsMonitor<MessagingOptions> options, TimeProvider clock)
{
    /// <summary>Records each held version's check value the first time a process holds it.</summary>
    private const string RecordCheckValues = """
        INSERT INTO messaging.key_versions (key, version, check_value, retired, value_count)
        SELECT held.key, held.version, held.check_value, false, 0
        FROM unnest(@keys, @versions, @checks) AS held (key, version, check_value)
        ON CONFLICT (key, version) DO NOTHING
        """;

    /// <summary>
    /// Sets, in one statement, the first-made instant of each write version where it is empty
    /// and their last-made instant, clearing their retired mark.
    /// </summary>
    private const string MarkWriteVersionsMade = """
        UPDATE messaging.key_versions
        SET first_made_at = COALESCE(first_made_at, @now), last_made_at = GREATEST(last_made_at, @now), retired = false
        WHERE (key, version) IN (SELECT * FROM unnest(@keys, @versions))
        """;

    /// <summary>Writes the current instant as one version's last-made instant where the one recorded is older, clearing its retired mark.</summary>
    private const string MarkVersionMade = """
        UPDATE messaging.key_versions
        SET last_made_at = GREATEST(last_made_at, @now), retired = false
        WHERE key = @key AND version = @version
        """;

    /// <summary>Writes this process's token lifetime where it is longer than the one recorded.</summary>
    private const string RecordTokenLifetime = """
        INSERT INTO messaging.token_lifetime (id, longest) VALUES (1, @lifetime)
        ON CONFLICT (id) DO UPDATE SET longest = EXCLUDED.longest WHERE token_lifetime.longest < EXCLUDED.longest
        """;

    /// <summary>
    /// Records the held versions' check values and checks them against those recorded, a
    /// version whose bytes changed stopping the start as invalid and a version in use that this
    /// process does not hold as unavailable (an <c>smsReportKey</c> version save); then marks
    /// the write versions made and writes the token lifetime, all in one transaction.
    /// </summary>
    internal Task StartAsync(CancellationToken cancellationToken)
    {
        return LibraryTransactions.RunAsync(
            database,
            options.CurrentValue,
            async (context, token) =>
            {
                IReadOnlyList<HeldKey> held = keys.All;
                Dictionary<(string Key, int Version), byte[]> checks = [];
                foreach (HeldKey key in held)
                {
                    foreach ((int version, byte[] value) in key.Versions)
                    {
                        checks[(key.Name, version)] = KeyForms.CheckValue(key.Name, value);
                    }
                }

                await context.Database.ExecuteSqlRawAsync(
                    RecordCheckValues,
                    [
                        new NpgsqlParameter("keys", checks.Keys.Select(held => held.Key).ToArray()),
                        new NpgsqlParameter("versions", checks.Keys.Select(held => held.Version).ToArray()),
                        new NpgsqlParameter("checks", checks.Values.ToArray()),
                    ],
                    token).ConfigureAwait(false);

                string[] names = [.. held.Select(key => key.Name)];
                List<KeyVersion> rows = await context.KeyVersions.AsNoTracking().Where(row => names.Contains(row.Key)).ToListAsync(token).ConfigureAwait(false);
                foreach (HeldKey key in held)
                {
                    IEnumerable<KeyVersion> recorded = rows.Where(row => string.Equals(row.Key, key.Name, StringComparison.Ordinal));
                    if (recorded.Any(row => checks.TryGetValue((key.Name, row.Version), out byte[]? check) && !check.AsSpan().SequenceEqual(row.CheckValue)))
                    {
                        throw MessagingStartup.Failure(Codes.SecretInvalid, ("key", key.Name));
                    }

                    if (!string.Equals(key.Name, KeyNeeds.SmsReportKey, StringComparison.Ordinal)
                        && recorded.Any(row => row.FirstMadeAt is not null && !row.Retired && !key.Versions.ContainsKey(row.Version)))
                    {
                        throw MessagingStartup.Failure(Codes.SecretUnavailable, ("key", key.Name));
                    }
                }

                return await WriteMadeAsync(context, token).ConfigureAwait(false);
            },
            cancellationToken);
    }

    /// <summary>Writes the write versions' last-made instant and the token lifetime, as the timer does each interval and at stop.</summary>
    internal Task WriteMadeAsync(CancellationToken cancellationToken)
    {
        return LibraryTransactions.RunAsync(database, options.CurrentValue, WriteMadeAsync, cancellationToken);
    }

    /// <summary>
    /// Writes, in its own transaction, the current instant as the last-made instant of
    /// <paramref name="key"/>'s <paramref name="version"/> where the one recorded is older,
    /// clearing its retired mark (<c>05</c> MSG-API-005).
    /// </summary>
    internal Task MarkMadeAsync(string key, int version, CancellationToken cancellationToken)
    {
        return LibraryTransactions.RunAsync(
            database,
            options.CurrentValue,
            (context, token) => context.Database.ExecuteSqlRawAsync(
                MarkVersionMade,
                [new NpgsqlParameter("now", clock.GetUtcNow()), new NpgsqlParameter("key", key), new NpgsqlParameter("version", version)],
                token),
            cancellationToken);
    }

    /// <summary>Whether values have been made under <paramref name="key"/>'s <paramref name="version"/> and it is not retired, read now.</summary>
    internal Task<bool> InUseAsync(string key, int version, CancellationToken cancellationToken)
    {
        return LibraryTransactions.RunAsync(
            database,
            options.CurrentValue,
            (context, token) => context.KeyVersions.AsNoTracking().AnyAsync(row => row.Key == key && row.Version == version && row.FirstMadeAt != null && !row.Retired, token),
            cancellationToken);
    }

    private async Task<int> WriteMadeAsync(MessagingContext context, CancellationToken cancellationToken)
    {
        IReadOnlyList<HeldKey> held = keys.All;
        int written = await context.Database.ExecuteSqlRawAsync(
            MarkWriteVersionsMade,
            [
                new NpgsqlParameter("now", clock.GetUtcNow()),
                new NpgsqlParameter("keys", held.Select(key => key.Name).ToArray()),
                new NpgsqlParameter("versions", held.Select(key => key.WriteVersion).ToArray()),
            ],
            cancellationToken).ConfigureAwait(false);

        if (keys.Find(KeyNeeds.TokenKeys) is not null)
        {
            await context.Database.ExecuteSqlRawAsync(
                RecordTokenLifetime,
                [new NpgsqlParameter("lifetime", options.CurrentValue.Unsubscribe.TokenLifetime)],
                cancellationToken).ConfigureAwait(false);
        }

        return written;
    }
}