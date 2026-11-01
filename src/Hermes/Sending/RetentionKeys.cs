using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Operations;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

using NpgsqlTypes;

namespace Hermes.Sending;

/// <summary>
/// The retention job's work on the key versions (<c>06</c> MSG-OPS-008): it protects again,
/// in batches, every row's values under a <c>contentKeys</c> version lower than the write
/// version, and marks retired each version nothing needs whose values were last made at least
/// <c>Hermes:Secrets:RetireAfter</c> before; it does neither while the database holds a
/// migration its release does not know, read in each batch's transaction.
/// </summary>
internal sealed partial class RetentionKeys(
    MessagingDatabase database,
    LibraryProtector library,
    HeldKeys keys,
    IOptionsMonitor<MessagingOptions> options,
    TimeProvider clock,
    ILogger<RetentionKeys> logger,
    IEnumerable<KeyedTable> tables)
{
    private const string Deliveries = """
        SELECT * FROM messaging.deliveries
        WHERE content_key_version < @write AND id > @after
          AND channel = ANY(@channels) AND status = ANY(@statuses) AND (reason IS NULL OR reason = ANY(@reasons))
        ORDER BY id LIMIT @batch FOR UPDATE SKIP LOCKED
        """;

    private const string ProtectDelivery = """
        UPDATE messaging.deliveries
        SET destination = @destination, unsubscribe = @unsubscribe, subject = @subject, text = @text, html = @html,
            title = @title, body = @body, url = @url, tag = @tag, icon = @icon, badge = @badge, parts = @parts,
            idempotency_key = @idempotencykey, content_key_version = @version
        WHERE id = @id
          AND destination IS NOT DISTINCT FROM @was_destination AND unsubscribe IS NOT DISTINCT FROM @was_unsubscribe
          AND subject IS NOT DISTINCT FROM @was_subject AND text IS NOT DISTINCT FROM @was_text AND html IS NOT DISTINCT FROM @was_html
          AND title IS NOT DISTINCT FROM @was_title AND body IS NOT DISTINCT FROM @was_body AND url IS NOT DISTINCT FROM @was_url
          AND tag IS NOT DISTINCT FROM @was_tag AND icon IS NOT DISTINCT FROM @was_icon AND badge IS NOT DISTINCT FROM @was_badge
          AND parts IS NOT DISTINCT FROM @was_parts AND idempotency_key IS NOT DISTINCT FROM @was_idempotencykey
        """;

    private const string Subscriptions = """
        SELECT * FROM messaging.subscriptions
        WHERE content_key_version < @write AND id > @after
        ORDER BY id LIMIT @batch FOR UPDATE SKIP LOCKED
        """;

    private const string ProtectSubscription = """
        UPDATE messaging.subscriptions SET endpoint = @endpoint, keys = @keys, content_key_version = @version
        WHERE id = @id AND endpoint = @was_endpoint AND keys = @was_keys
        """;

    private const string Entries = """
        SELECT * FROM messaging.entries
        WHERE content_key_version < @write AND id > @after
        ORDER BY id LIMIT @batch FOR UPDATE SKIP LOCKED
        """;

    private const string ProtectEntry = """
        UPDATE messaging.entries SET parts = @parts, content_key_version = @version
        WHERE id = @id AND parts = @was_parts
        """;

    private const string NoContentRow = """
        NOT EXISTS (SELECT 1 FROM messaging.deliveries WHERE content_key_version = @version)
           AND NOT EXISTS (SELECT 1 FROM messaging.subscriptions WHERE content_key_version = @version)
           AND NOT EXISTS (SELECT 1 FROM messaging.entries WHERE content_key_version = @version)
        """;

    private const string NoHash = """
        NOT EXISTS (SELECT 1 FROM messaging.idempotency WHERE hash_version = @version)
           AND NOT EXISTS (SELECT 1 FROM messaging.suppressions WHERE hash_version = @version)
           AND NOT EXISTS (SELECT 1 FROM messaging.places WHERE hash_version = @version)
           AND NOT EXISTS (SELECT 1 FROM messaging.subscriptions WHERE hash_version = @version)
        """;

    private const string NoWaitingToken = """
        SELECT NOT EXISTS (
            SELECT 1 FROM messaging.deliveries
            WHERE NOT (status = ANY(@finals)) AND unsubscribe IS NOT NULL AND recorded_at < @lastMade + @retireAfter) AS "Value"
        """;

    private const string NoSubscription = """
        SELECT NOT EXISTS (SELECT 1 FROM messaging.subscriptions WHERE vapid_version = @version) AS "Value"
        """;

    private const string Retire = """
        UPDATE messaging.key_versions SET retired = true
        WHERE key = @key AND version = @version AND last_made_at = @lastMade AND NOT retired
        """;

    private static readonly string[] Finals = [.. Spellings.AllStatuses.Where(status => status is not ("waiting" or "inflight"))];

    private static readonly string[] Judged = [KeyNeeds.ContentKeys, KeyNeeds.HashKeys, KeyNeeds.TokenKeys, KeyNeeds.PushVapidKeys];

    /// <summary>The other tables holding values, hashes or person references (M-031).</summary>
    private readonly IReadOnlyList<KeyedTable> keyed = [.. tables];

    private enum Again
    {
        Protected,
        Left,
        Refused,
    }

    /// <summary>
    /// Protects again, in batches, the values of every delivery, subscription, in-app entry and
    /// row of a keyed table under a <c>contentKeys</c> version below this process's write
    /// version, renewing the claim after each; false where a renewal finds the claim no longer
    /// the run's.
    /// </summary>
    internal async Task<bool> ProtectAgainAsync(Func<Task<bool>> renew, CancellationToken cancellationToken)
    {
        if (keys.Find(KeyNeeds.ContentKeys) is not { } content)
        {
            return true;
        }

        if (!(await RunAsync(content.WriteVersion, Deliveries, renew, (context, row, token) => DeliveryAsync(context, (DeliveryRow)row, content.WriteVersion, token), context => context.Deliveries, cancellationToken).ConfigureAwait(false)
            && await RunAsync(content.WriteVersion, Subscriptions, renew, (context, row, token) => SubscriptionAsync(context, (SubscriptionRow)row, content.WriteVersion, token), context => context.Subscriptions, cancellationToken).ConfigureAwait(false)
            && await RunAsync(content.WriteVersion, Entries, renew, (context, row, token) => EntryAsync(context, (EntryRow)row, content.WriteVersion, token), context => context.Entries, cancellationToken).ConfigureAwait(false)))
        {
            return false;
        }

        foreach (KeyedTable table in keyed.Where(table => table.Values.Count > 0))
        {
            if (!await RunKeyedAsync(table, content.WriteVersion, renew, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Marks retired each version nothing needs, one transaction a version, renewing the claim after each; false where a renewal finds the claim no longer the run's.</summary>
    internal async Task<bool> RetireAsync(Func<Task<bool>> renew, CancellationToken cancellationToken)
    {
        MessagingOptions current = options.CurrentValue;
        DateTimeOffset now = clock.GetUtcNow();
        TimeSpan retireAfter = current.Secrets.RetireAfter;
        List<KeyVersion> candidates;
        MessagingContext reading = database.Context();
        await using (reading.ConfigureAwait(false))
        {
            DateTimeOffset latest = now - retireAfter;
            candidates = await reading.KeyVersions.AsNoTracking()
                .Where(row => Judged.Contains(row.Key) && row.FirstMadeAt != null && !row.Retired && row.LastMadeAt <= latest)
                .OrderBy(row => row.Key).ThenBy(row => row.Version)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (KeyVersion candidate in candidates)
        {
            bool retired = await LibraryTransactions.RunAsync(
                database,
                current,
                async (context, token) =>
                {
                    if (await SchemaCheck.UnknownAsync(context, token).ConfigureAwait(false) is not null
                        || !await UnneededAsync(context, candidate, now, retireAfter, current.Dispatch.Interval, token).ConfigureAwait(false))
                    {
                        return false;
                    }

                    return await context.Database.ExecuteSqlRawAsync(
                        Retire,
                        [new NpgsqlParameter("key", candidate.Key), new NpgsqlParameter("version", candidate.Version), new NpgsqlParameter("lastMade", candidate.LastMadeAt!.Value)],
                        token).ConfigureAwait(false) == 1;
                },
                cancellationToken).ConfigureAwait(false);
            if (retired)
            {
                Retired(logger, candidate.Key, candidate.Version);
            }

            if (!await renew().ConfigureAwait(false))
            {
                return false;
            }
        }

        return true;
    }

    private static NpgsqlParameter Bytes(string name, byte[]? value)
    {
        return new NpgsqlParameter(name, NpgsqlDbType.Bytea) { Value = (object?)value ?? DBNull.Value };
    }

    private static (string Field, byte[]? Stored)[] Fields(DeliveryRow row)
    {
        return
        [
            ("destination", row.Destination), ("unsubscribe", row.Unsubscribe), ("subject", row.Subject), ("text", row.Text), ("html", row.Html),
            ("title", row.Title), ("body", row.Body), ("url", row.Url), ("tag", row.Tag), ("icon", row.Icon), ("badge", row.Badge),
            (ContentContext.IdempotencyKey, row.IdempotencyKey),
        ];
    }

    /// <summary>Runs the batches of one table, each in its own transaction that first reads the migration history; answers false where the claim was lost.</summary>
    private async Task<bool> RunAsync<TRow>(
        int write,
        string select,
        Func<Task<bool>> renew,
        Func<MessagingContext, object, CancellationToken, Task<Again>> protect,
        Func<MessagingContext, DbSet<TRow>> table,
        CancellationToken cancellationToken)
        where TRow : class
    {
        MessagingOptions current = options.CurrentValue;
        int batch = current.Jobs.BatchSize;
        Guid after = Guid.Empty;
        int protectedRows = 0;
        while (true)
        {
            (int Read, Guid Last, bool Stop) outcome = await LibraryTransactions.RunAsync(
                database,
                current,
                async (context, token) =>
                {
                    if (await SchemaCheck.UnknownAsync(context, token).ConfigureAwait(false) is not null)
                    {
                        return (0, after, true);
                    }

                    List<TRow> rows = await table(context).FromSqlRaw(
                        select,
                        new NpgsqlParameter("write", write),
                        new NpgsqlParameter("after", after),
                        new NpgsqlParameter("batch", batch),
                        new NpgsqlParameter("channels", Spellings.AllChannels.ToArray()),
                        new NpgsqlParameter("statuses", Spellings.AllStatuses.ToArray()),
                        new NpgsqlParameter("reasons", Spellings.AllReasons.ToArray())).AsNoTracking().ToListAsync(token).ConfigureAwait(false);
                    Guid last = after;
                    foreach (TRow row in rows)
                    {
                        last = row switch { DeliveryRow delivery => delivery.Id, EntryRow entry => entry.Id, _ => ((SubscriptionRow)(object)row).Id };
                        switch (await protect(context, row, token).ConfigureAwait(false))
                        {
                            case Again.Protected:
                                protectedRows++;
                                break;
                            case Again.Refused:
                                return (rows.Count, last, true);
                            default:
                                break;
                        }
                    }

                    return (rows.Count, last, false);
                },
                cancellationToken).ConfigureAwait(false);
            after = outcome.Last;
            if (!await renew().ConfigureAwait(false))
            {
                return false;
            }

            if (outcome.Stop || outcome.Read < batch)
            {
                break;
            }
        }

        if (protectedRows > 0)
        {
            ProtectedAgain(logger, protectedRows);
        }

        return true;
    }

    private async Task<Again> DeliveryAsync(MessagingContext context, DeliveryRow row, int write, CancellationToken cancellationToken)
    {
        (string Field, byte[]? Stored)[] fields = Fields(row);
        List<NpgsqlParameter> parameters = [new NpgsqlParameter("id", row.Id), new NpgsqlParameter("version", write)];
        foreach ((string field, byte[]? stored) in fields)
        {
            byte[]? again = null;
            if (stored is not null)
            {
                (Again outcome, byte[]? made) = await AgainAsync(row.Person, stored, ContentContext.Of(ContentContext.Deliveries, row.Id, field), cancellationToken).ConfigureAwait(false);
                if (outcome != Again.Protected)
                {
                    return outcome;
                }

                again = made;
            }

            parameters.Add(Bytes(field, again));
            parameters.Add(Bytes("was_" + field, stored));
        }

        string? parts = null;
        if (row.Parts is not null)
        {
            (Again outcome, string? made) = await PartsAgainAsync(row.Person, row.Parts, ContentContext.Deliveries, row.Id, cancellationToken).ConfigureAwait(false);
            if (outcome != Again.Protected)
            {
                return outcome;
            }

            parts = made;
        }

        parameters.Add(Json("parts", parts));
        parameters.Add(Json("was_parts", row.Parts));
        return await context.Database.ExecuteSqlRawAsync(ProtectDelivery, parameters, cancellationToken).ConfigureAwait(false) == 1 ? Again.Protected : Again.Left;
    }

    private async Task<Again> EntryAsync(MessagingContext context, EntryRow row, int write, CancellationToken cancellationToken)
    {
        (Again outcome, string? parts) = await PartsAgainAsync(row.Person, row.Parts, ContentContext.Entries, row.Id, cancellationToken).ConfigureAwait(false);
        if (outcome != Again.Protected)
        {
            return outcome;
        }

        int changed = await context.Database.ExecuteSqlRawAsync(
            ProtectEntry,
            [new NpgsqlParameter("id", row.Id), new NpgsqlParameter("version", write), Json("parts", parts), Json("was_parts", row.Parts)],
            cancellationToken).ConfigureAwait(false);
        return changed == 1 ? Again.Protected : Again.Left;
    }

    /// <summary>Each value of an in-app parts column, as <see cref="InAppParts"/> writes it, protected again bound to its field of <paramref name="row"/> in <paramref name="table"/>; the first that is not protected again decides.</summary>
    private async Task<(Again Outcome, string? Parts)> PartsAgainAsync(string? person, string column, string table, Guid row, CancellationToken cancellationToken)
    {
        if (InAppParts.Read(column) is not { } stored)
        {
            return (Again.Left, null);
        }

        Dictionary<string, byte[]> again = new(StringComparer.Ordinal);
        foreach ((string field, byte[] value) in stored)
        {
            (Again outcome, byte[]? made) = await AgainAsync(person, value, ContentContext.Of(table, row, field), cancellationToken).ConfigureAwait(false);
            if (outcome != Again.Protected)
            {
                return (outcome, null);
            }

            again[field] = made!;
        }

        return (Again.Protected, InAppParts.Write(again));
    }

    private static NpgsqlParameter Json(string name, string? value)
    {
        return new NpgsqlParameter(name, NpgsqlDbType.Jsonb) { Value = (object?)value ?? DBNull.Value };
    }

    private async Task<Again> SubscriptionAsync(MessagingContext context, SubscriptionRow row, int write, CancellationToken cancellationToken)
    {
        (Again endpointOutcome, byte[]? endpoint) = await AgainAsync(row.Person, row.Endpoint, ContentContext.Of(ContentContext.Subscriptions, row.Id, "endpoint"), cancellationToken).ConfigureAwait(false);
        if (endpointOutcome != Again.Protected)
        {
            return endpointOutcome;
        }

        (Again keysOutcome, byte[]? subscriptionKeys) = await AgainAsync(row.Person, row.Keys, ContentContext.Of(ContentContext.Subscriptions, row.Id, "keys"), cancellationToken).ConfigureAwait(false);
        if (keysOutcome != Again.Protected)
        {
            return keysOutcome;
        }

        int changed = await context.Database.ExecuteSqlRawAsync(
            ProtectSubscription,
            [new NpgsqlParameter("id", row.Id), new NpgsqlParameter("version", write), Bytes("endpoint", endpoint), Bytes("keys", subscriptionKeys), Bytes("was_endpoint", row.Endpoint), Bytes("was_keys", row.Keys)],
            cancellationToken).ConfigureAwait(false);
        return changed == 1 ? Again.Protected : Again.Left;
    }

    /// <summary>Runs the batches of a keyed table, as <see cref="RunAsync{TRow}"/> runs those of the tables the engine knows; answers false where the claim was lost.</summary>
    private async Task<bool> RunKeyedAsync(KeyedTable table, int write, Func<Task<bool>> renew, CancellationToken cancellationToken)
    {
        MessagingOptions current = options.CurrentValue;
        int batch = current.Jobs.BatchSize;
        Guid after = Guid.Empty;
        int protectedRows = 0;
        while (true)
        {
            (int Read, Guid Last, bool Stop) outcome = await LibraryTransactions.RunAsync(
                database,
                current,
                async (context, token) =>
                {
                    if (await SchemaCheck.UnknownAsync(context, token).ConfigureAwait(false) is not null)
                    {
                        return (0, after, true);
                    }

                    List<KeyedRow> rows = await ReadKeyedAsync(context, table, write, after, batch, token).ConfigureAwait(false);
                    Guid last = after;
                    foreach (KeyedRow row in rows)
                    {
                        last = row.Id;
                        switch (await KeyedAsync(context, table, row, write, token).ConfigureAwait(false))
                        {
                            case Again.Protected:
                                protectedRows++;
                                break;
                            case Again.Refused:
                                return (rows.Count, last, true);
                            default:
                                break;
                        }
                    }

                    return (rows.Count, last, false);
                },
                cancellationToken).ConfigureAwait(false);
            after = outcome.Last;
            if (!await renew().ConfigureAwait(false))
            {
                return false;
            }

            if (outcome.Stop || outcome.Read < batch)
            {
                break;
            }
        }

        if (protectedRows > 0)
        {
            ProtectedAgain(logger, protectedRows);
        }

        return true;
    }

    /// <summary>A batch of a keyed table's rows to protect again, read in <paramref name="context"/>'s transaction.</summary>
    private static async Task<List<KeyedRow>> ReadKeyedAsync(MessagingContext context, KeyedTable table, int write, Guid after, int batch, CancellationToken cancellationToken)
    {
        List<KeyedLine> lines = await context.Database.SqlQueryRaw<KeyedLine>(
            table.SelectToProtectAgain(),
            new NpgsqlParameter("write", write),
            new NpgsqlParameter("after", after),
            new NpgsqlParameter("batch", batch)).ToListAsync(cancellationToken).ConfigureAwait(false);
        return [.. lines.GroupBy(line => line.Id).Select(row => new KeyedRow(row.Key, row.First().Person, [.. row.OrderBy(line => line.Position).Select(line => line.Value)]))];
    }

    /// <summary>A keyed table's row's values protected again together, in one update conditional on each still being the one read.</summary>
    private async Task<Again> KeyedAsync(MessagingContext context, KeyedTable table, KeyedRow row, int write, CancellationToken cancellationToken)
    {
        List<NpgsqlParameter> parameters = [new NpgsqlParameter("id", row.Id), new NpgsqlParameter("version", write)];
        for (int index = 0; index < table.Values.Count; index++)
        {
            byte[]? stored = row.Values[index];
            byte[]? again = null;
            if (stored is not null)
            {
                (Again outcome, byte[]? made) = await AgainAsync(row.Person, stored, ContentContext.Of(table.Context, row.Id, table.Values[index].Field), cancellationToken).ConfigureAwait(false);
                if (outcome != Again.Protected)
                {
                    return outcome;
                }

                again = made;
            }

            parameters.Add(Bytes("v" + index.ToString(CultureInfo.InvariantCulture), again));
            parameters.Add(Bytes("was" + index.ToString(CultureInfo.InvariantCulture), stored));
        }

        return await context.Database.ExecuteSqlRawAsync(table.ProtectAgain(), parameters, cancellationToken).ConfigureAwait(false) == 1 ? Again.Protected : Again.Left;
    }

    /// <summary>A library value opened and protected again under the write version; left where it is not the library's or does not open, refused where no value can be reserved.</summary>
    private async Task<(Again Outcome, byte[]? Stored)> AgainAsync(string? person, byte[] stored, string context, CancellationToken cancellationToken)
    {
        ContentEnvelope.Envelope envelope = ContentEnvelope.Read(stored);
        if (envelope.Form != ContentEnvelope.EnvelopeForm.Value || !string.Equals(envelope.Id, LibraryProtector.LibraryId, StringComparison.Ordinal))
        {
            return (Again.Left, null);
        }

        if (await library.UnprotectAsync(person ?? string.Empty, envelope.Output, context).ConfigureAwait(false) is not UnprotectResult.Opened opened)
        {
            return (Again.Left, null);
        }

        ContentProtecting made = await library.ProtectAsync(person ?? string.Empty, opened.Plaintext, context, cancellationToken).ConfigureAwait(false);
        return made.Stored is null ? (Again.Refused, null) : (Again.Protected, ContentEnvelope.Wrap(LibraryProtector.LibraryId, made.Stored));
    }

    /// <summary>
    /// The statement answering whether no row records <c>@version</c>: <paramref name="known"/>
    /// over the tables the engine knows, and the same over each keyed table that records
    /// <paramref name="column"/>'s version.
    /// </summary>
    internal static string NoRowUnder(string known, IEnumerable<KeyedTable> tables, Func<KeyedTable, string?> column)
    {
        IEnumerable<string> others = tables.Select(table => column(table) is { } named ? table.NoRowUnder(named) : null).OfType<string>();
        return "SELECT " + string.Join(" AND ", [known.Trim(), .. others]) + " AS \"Value\"";
    }

    /// <summary>Whether nothing needs <paramref name="candidate"/>, read in the marking transaction.</summary>
    private async Task<bool> UnneededAsync(MessagingContext context, KeyVersion candidate, DateTimeOffset now, TimeSpan retireAfter, TimeSpan interval, CancellationToken cancellationToken)
    {
        NpgsqlParameter version = new("version", candidate.Version);
        switch (candidate.Key)
        {
            case KeyNeeds.ContentKeys:
                return await IsAsync(context, NoRowUnder(NoContentRow, keyed, table => table.ContentKeyVersion), [version], cancellationToken).ConfigureAwait(false);
            case KeyNeeds.PushVapidKeys:
                return await IsAsync(context, NoSubscription, [version], cancellationToken).ConfigureAwait(false);
            case KeyNeeds.HashKeys:
                return await IsAsync(context, NoRowUnder(NoHash, keyed, table => table.HashVersion), [version], cancellationToken).ConfigureAwait(false)
                    && await NoTokenNeedsAsync(context, candidate, now, retireAfter, interval, cancellationToken).ConfigureAwait(false);
            default:
                return await NoTokenNeedsAsync(context, candidate, now, retireAfter, interval, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Whether no token can still name the version: no delivery that is not final and holds an
    /// unsubscribe address was recorded before <c>RetireAfter</c> had passed since values were
    /// last made under it, and the longest recorded token lifetime and <c>RetireAfter</c> have
    /// passed since then.
    /// </summary>
    private static async Task<bool> NoTokenNeedsAsync(MessagingContext context, KeyVersion candidate, DateTimeOffset now, TimeSpan retireAfter, TimeSpan interval, CancellationToken cancellationToken)
    {
        DateTimeOffset lastMade = candidate.LastMadeAt!.Value;
        TimeSpan longest = await context.TokenLifetimes.AsNoTracking().Select(row => (TimeSpan?)row.Longest).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false) ?? TimeSpan.Zero;
        // A dispatcher writes a token's versions at most once an interval, so the last email may be an interval later.
        return now >= lastMade + interval + longest + retireAfter
            && await IsAsync(
                context,
                NoWaitingToken,
                [new NpgsqlParameter("finals", Finals), new NpgsqlParameter("lastMade", lastMade), new NpgsqlParameter("retireAfter", retireAfter)],
                cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> IsAsync(MessagingContext context, string query, NpgsqlParameter[] parameters, CancellationToken cancellationToken)
    {
        List<bool> answer = await context.Database.SqlQueryRaw<bool>(query, parameters).ToListAsync(cancellationToken).ConfigureAwait(false);
        return answer.Single();
    }

    [LoggerMessage(EventId = 62, EventName = "RetentionVersionRetired", Level = LogLevel.Information, Message = "The retention job marked {Key} version {Version} retired.")]
    private static partial void Retired(ILogger logger, string key, int version);

    [LoggerMessage(EventId = 63, EventName = "RetentionValuesProtectedAgain", Level = LogLevel.Information, Message = "The retention job protected again the values of {Count} rows under the contentKeys write version.")]
    private static partial void ProtectedAgain(ILogger logger, int count);
}