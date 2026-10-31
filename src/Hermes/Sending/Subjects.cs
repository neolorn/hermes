using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Content;
using Hermes.Core;
using Hermes.Operations;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Hermes.Sending;

/// <summary>
/// A person's erasure and export (<c>01</c> MSG-SEND-020): each in one transaction under the
/// person's locks, exclusive to erase and shared to export, each wait bounded by
/// <c>Hermes:Dispatch:Timeout</c>; an export opens its values after its transaction commits.
/// A scoped service, resolving the content protector from the scope it was called in.
/// </summary>
internal sealed class Subjects(
    MessagingReady ready,
    Declarations declarations,
    IOptionsMonitor<MessagingOptions> options,
    ContentProtection protection,
    KeyedHashes hashes,
    MessagingDatabase database,
    Alerts alerts,
    Notices notices,
    TimeProvider clock) : ISubjects
{
    private const int MostReferenceCharacters = 128;

    private const string Destination = "destination";

    private const string Cancel = """
        UPDATE messaging.deliveries
        SET status = 'cancelled', reason = NULL, last_moved_at = @now, claimed_by = NULL, claimed_at = NULL, claim_expires_at = NULL,
            next_sequence = next_sequence + @advance
        WHERE id IN (SELECT id FROM messaging.deliveries WHERE person = @person AND status IN ('waiting', 'inflight') ORDER BY id FOR UPDATE)
          AND status IN ('waiting', 'inflight')
        RETURNING id AS "Id", channel AS "Channel", next_sequence - @advance AS "Sequence"
        """;

    private const string DeletePreferences = "DELETE FROM messaging.preferences WHERE person = @person";

    private const string DeleteSubscriptions = "DELETE FROM messaging.subscriptions WHERE person = @person";

    private const string DeleteIdempotency = "DELETE FROM messaging.idempotency WHERE NOT anonymous AND scope = @scope";

    private const string DeletePlaces = "DELETE FROM messaging.places WHERE kind = 'limit' AND hash = ANY(@hashes)";

    private const string Unlink = "UPDATE messaging.places SET delivery_id = NULL WHERE delivery_id IN (SELECT id FROM messaging.deliveries WHERE person = @person)";

    private const string Clear = """
        UPDATE messaging.deliveries
        SET person = NULL, destination = NULL, unsubscribe = NULL, subject = NULL, text = NULL, html = NULL,
            title = NULL, body = NULL, url = NULL, tag = NULL, icon = NULL, badge = NULL, content_key_version = NULL
        WHERE person = @person
        """;

    public async ValueTask<Result> EraseAsync(string person, CancellationToken cancellationToken)
    {
        ready.ThrowIfNotReady();
        CheckPerson(person);
        MessagingOptions current = options.CurrentValue;
        bool observe = current.Observer.Enabled;
        Erasure erasure = await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await DeliveryClaims.LockTimeoutAsync(context, current.Dispatch.Timeout, token).ConfigureAwait(false);
                await AdvisoryLocks.TakeAsync(context, AdvisoryLocks.Person(hashes, person), shared: false, token).ConfigureAwait(false);
                if (await SchemaCheck.UnknownAsync(context, token).ConfigureAwait(false) is { } migration)
                {
                    return new Erasure(Newer(migration), [], null);
                }

                if (await KeysInUse.MissingAsync(context, KeyNeeds.HashKeys, [.. hashes.Versions], token).ConfigureAwait(false) is { } version)
                {
                    return new Erasure(Result.Fail(Unavailable(KeyNeeds.HashKeys)), [], version);
                }

                DateTimeOffset now = clock.GetUtcNow();
                List<Cancelled> cancelled = await context.Database.SqlQueryRaw<Cancelled>(
                    Cancel,
                    new NpgsqlParameter("now", now),
                    new NpgsqlParameter("person", person),
                    new NpgsqlParameter("advance", observe ? 1 : 0)).ToListAsync(token).ConfigureAwait(false);
                if (observe)
                {
                    foreach (Cancelled delivery in cancelled)
                    {
                        Notices.Add(context, delivery.Id, delivery.Sequence, Spellings.Of(DeliveryStatus.Cancelled), null, now);
                    }
                }

                if (cancelled.Count > 0)
                {
                    await Places.ReleaseAsync(context, [.. cancelled.Select(delivery => delivery.Id)], token).ConfigureAwait(false);
                }

                await context.SaveChangesAsync(token).ConfigureAwait(false);
                byte[][] keyed = [.. hashes.Every(KeyedHashes.Person, Encoding.UTF8.GetBytes(person)).Select(hash => hash.Hash)];
                await RunAsync(context, DeletePreferences, new NpgsqlParameter("person", person), token).ConfigureAwait(false);
                await RunAsync(context, DeleteSubscriptions, new NpgsqlParameter("person", person), token).ConfigureAwait(false);
                await RunAsync(context, DeleteIdempotency, new NpgsqlParameter("scope", Encoding.UTF8.GetBytes(person)), token).ConfigureAwait(false);
                await RunAsync(context, DeletePlaces, new NpgsqlParameter("hashes", keyed), token).ConfigureAwait(false);
                await RunAsync(context, Unlink, new NpgsqlParameter("person", person), token).ConfigureAwait(false);
                await RunAsync(context, Clear, new NpgsqlParameter("person", person), token).ConfigureAwait(false);
                return new Erasure(Result.Success(), cancelled, null);
            },
            cancellationToken).ConfigureAwait(false);
        if (erasure.Missing is { } missing)
        {
            await alerts.HoldsUntilAsync(
                "key-unavailable",
                KeyNeeds.HashKeys,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["version"] = missing.ToString(CultureInfo.InvariantCulture) },
                cancellationToken).ConfigureAwait(false);
        }

        foreach (Cancelled delivery in erasure.Cancelled)
        {
            notices.Count(delivery.Channel, Spellings.Of(DeliveryStatus.Cancelled));
        }

        return erasure.Result;
    }

    public async ValueTask<Result<SubjectExport>> ExportAsync(string person, CancellationToken cancellationToken)
    {
        ready.ThrowIfNotReady();
        CheckPerson(person);
        MessagingOptions current = options.CurrentValue;
        Read read = await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await DeliveryClaims.LockTimeoutAsync(context, current.Dispatch.Timeout, token).ConfigureAwait(false);
                await AdvisoryLocks.TakeAsync(context, AdvisoryLocks.Person(hashes, person), shared: true, token).ConfigureAwait(false);
                if (await SchemaCheck.UnknownAsync(context, token).ConfigureAwait(false) is { } migration)
                {
                    return new Read(migration, [], [], [], []);
                }

                DateTimeOffset now = clock.GetUtcNow();
                byte[] scope = Encoding.UTF8.GetBytes(person);
                return new Read(
                    null,
                    await context.Preferences.AsNoTracking().Where(row => row.Person == person).ToListAsync(token).ConfigureAwait(false),
                    await context.Subscriptions.AsNoTracking().Where(row => row.Person == person).OrderBy(row => row.MadeAt).ToListAsync(token).ConfigureAwait(false),
                    await context.Idempotency.AsNoTracking().Where(row => !row.Anonymous && row.Scope == scope && row.LapsesAt > now).OrderBy(row => row.RecordedAt).ToListAsync(token).ConfigureAwait(false),
                    await context.Deliveries.AsNoTracking().Where(row => row.Person == person).OrderBy(row => row.RecordedAt).ThenBy(row => row.Id).ToListAsync(token).ConfigureAwait(false));
            },
            cancellationToken).ConfigureAwait(false);
        if (read.Migration is { } unknown)
        {
            return Result.Fail<SubjectExport>(Failure(Codes.ReleaseNewer, "migration", unknown));
        }

        List<DeliveryExport> deliveries = [];
        bool rejected = false;
        foreach (DeliveryRow row in read.Deliveries)
        {
            if (Spellings.Channel(row.Channel) is not { } channel || Spellings.Status(row.Status) is not { } status)
            {
                continue;
            }

            Opened opened = await OpenAsync(row, channel, current, cancellationToken).ConfigureAwait(false);
            if (opened.Held is { } held)
            {
                return Result.Fail<SubjectExport>(held);
            }

            rejected |= opened.Rejected;
            deliveries.Add(new DeliveryExport
            {
                Kind = row.Kind,
                Channel = channel,
                Status = status,
                RecordedAt = row.RecordedAt,
                LastMovedAt = row.LastMovedAt,
                Destination = opened.Values?.GetValueOrDefault(Destination),
                Content = opened.Values is { } values ? values.Where(value => value.Key != Destination).ToDictionary(StringComparer.Ordinal) : new Dictionary<string, string>(StringComparer.Ordinal),
                ContentDeleted = opened.Deleted,
                Erased = opened.Erased,
                Rejected = opened.Rejected,
            });
        }

        if (rejected)
        {
            await alerts.HoldsUntilAsync(
                "content-rejected",
                ContentContext.Deliveries,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["table"] = ContentContext.Deliveries },
                cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(new SubjectExport
        {
            Preferences = Preferences.Of(declarations, read.Preferences),
            Subscriptions = [.. read.Subscriptions
                .Where(row => Spellings.Service(row.Service) is not null)
                .Select(row => new SubscriptionExport { Service = Spellings.Service(row.Service)!.Value, MadeAt = row.MadeAt, LastAcceptedAt = row.LastAcceptedAt })],
            Deliveries = deliveries,
            Entries = [],
            IdempotencyKeys = [.. read.Idempotency.Select(row => new IdempotencyKeyExport { Kind = row.Kind, Key = row.Key, RecordedAt = row.RecordedAt })],
        });
    }

    /// <summary>The stored values of a delivery, by field: its destination, then its content's parts.</summary>
    private static IEnumerable<(string Field, byte[]? Stored)> Fields(DeliveryRow row, Channel channel)
    {
        yield return (Destination, row.Destination);
        switch (channel)
        {
            case Channel.Email:
                yield return ("subject", row.Subject);
                yield return ("text", row.Text);
                yield return ("html", row.Html);
                break;
            case Channel.Push:
                yield return (TemplateParts.Title, row.Title);
                yield return (TemplateParts.Body, row.Body);
                yield return (TemplateParts.Url, row.Url);
                yield return (TemplateParts.Tag, row.Tag);
                yield return (TemplateParts.Icon, row.Icon);
                yield return (TemplateParts.Badge, row.Badge);
                break;
            default:
                yield return ("text", row.Text);
                break;
        }
    }

    /// <summary>Opens each of the delivery's values outside any transaction; the first that is erased, rejected or cannot open yet decides.</summary>
    private async Task<Opened> OpenAsync(DeliveryRow row, Channel channel, MessagingOptions current, CancellationToken cancellationToken)
    {
        List<(string Field, byte[] Stored)> stored = [.. Fields(row, channel).Where(field => field.Stored is not null).Select(field => (field.Field, field.Stored!))];
        if (stored.Count == 0)
        {
            return new Opened(null, Deleted: true, Erased: false, Rejected: false, Held: null);
        }

        Dictionary<string, string> values = new(StringComparer.Ordinal);
        foreach ((string field, byte[] value) in stored)
        {
            ContentOpening opening;
            try
            {
                using CancellationTokenSource bound = new(current.Dispatch.Timeout, clock);
                using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(bound.Token, cancellationToken);
                opening = await protection.OpenAsync(row.Person, value, ContentContext.Of(ContentContext.Deliveries, row.Id, field), linked.Token)
                    .AsTask()
                    .WaitAsync(current.Dispatch.Timeout, clock, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception failure) when (failure is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                opening = ContentOpening.Held(ContentEnvelope.Read(value).Id);
            }

            switch (opening.Status)
            {
                case ContentOpeningStatus.Opened:
                    values[field] = Encoding.UTF8.GetString(opening.Plaintext.Span);
                    break;
                case ContentOpeningStatus.Erased:
                    return new Opened(null, Deleted: false, Erased: true, Rejected: false, Held: null);
                case ContentOpeningStatus.Rejected:
                    return new Opened(null, Deleted: false, Erased: false, Rejected: true, Held: null);
                default:
                    return new Opened(null, Deleted: false, Erased: false, Rejected: false, Held: opening.Key is { } key ? Failure(Codes.KeyUnavailable, "key", key) : new Failure { Code = Codes.KeyUnavailable, Details = new Dictionary<string, string>(StringComparer.Ordinal) });
            }
        }

        return new Opened(values, Deleted: false, Erased: false, Rejected: false, Held: null);
    }

    private static Task<int> RunAsync(MessagingContext context, string statement, NpgsqlParameter parameter, CancellationToken cancellationToken)
    {
        return context.Database.ExecuteSqlRawAsync(statement, [parameter], cancellationToken);
    }

    private static Result Newer(string migration)
    {
        return Result.Fail(Failure(Codes.ReleaseNewer, "migration", migration));
    }

    private static Failure Unavailable(string key)
    {
        return Failure(Codes.KeyUnavailable, "key", key);
    }

    private static Failure Failure(string code, string name, string value)
    {
        return new Failure { Code = code, Details = new Dictionary<string, string>(StringComparer.Ordinal) { [name] = value } };
    }

    private static void CheckPerson(string person)
    {
        if (person is not { Length: >= 1 and <= MostReferenceCharacters })
        {
            throw new ArgumentException("A person reference is 1 to 128 characters.", nameof(person));
        }
    }

    private sealed record Cancelled(Guid Id, string Channel, int Sequence);

    private sealed record Erasure(Result Result, List<Cancelled> Cancelled, int? Missing);

    private sealed record Read(string? Migration, List<PreferenceRow> Preferences, List<SubscriptionRow> Subscriptions, List<IdempotencyRow> Idempotency, List<DeliveryRow> Deliveries);

    private sealed record Opened(Dictionary<string, string>? Values, bool Deleted, bool Erased, bool Rejected, Failure? Held);
}