using System;
using System.Collections.Generic;
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
/// A person's in-application list (<c>02</c> MSG-INAPP-003): read a page at a time, newest
/// first, counted and marked read, every call in one transaction under the person's locks
/// taken shared, each wait bounded by <c>Hermes:Dispatch:Timeout</c>. A read opens its
/// entries' values after its transaction ends. A scoped service, resolving the content
/// protector from the scope it was called in.
/// </summary>
internal sealed class InAppList(
    MessagingReady ready,
    IOptionsMonitor<MessagingOptions> options,
    ContentProtection protection,
    KeyedHashes hashes,
    MessagingDatabase database,
    Alerts alerts,
    Notices notices,
    TimeProvider clock) : IInApp
{
    private const int MostReferenceCharacters = 128;

    private const int DefaultSize = 20;

    private const int MostSize = 100;

    private const string Page = """
        SELECT * FROM messaging.entries
        WHERE person = @person AND (NOT @after OR (written_at, id) < (@written_at, @id))
        ORDER BY written_at DESC, id DESC
        LIMIT @limit
        """;

    private const string Unread = "SELECT id AS \"Id\", delivery_id AS \"DeliveryId\" FROM messaging.entries WHERE person = @person AND read_at IS NULL ORDER BY id";

    private const string LockSent = "SELECT * FROM messaging.deliveries WHERE id = ANY(@ids) AND status = 'sent' ORDER BY id FOR UPDATE";

    private const string MarkRead = """
        UPDATE messaging.entries SET read_at = @now
        WHERE id = ANY(@ids) AND person = @person AND read_at IS NULL
        RETURNING id AS "Id", delivery_id AS "DeliveryId"
        """;

    public async ValueTask<Result<InAppPage>> ListAsync(string person, string? language, string? cursor, int? size, CancellationToken cancellationToken)
    {
        ready.ThrowIfNotReady();
        CheckPerson(person);
        int limit = size ?? DefaultSize;
        if (limit is < 1 or > MostSize)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "A page holds 1 to 100 entries.");
        }

        (DateTimeOffset WrittenAt, Guid Entry)? after = null;
        if (cursor is not null)
        {
            if (InAppCursor.Read(cursor) is not { } read)
            {
                return Result.Fail<InAppPage>(new Failure { Code = Codes.InAppCursorInvalid, Details = new Dictionary<string, string>(StringComparer.Ordinal) });
            }

            after = read;
        }

        MessagingOptions current = options.CurrentValue;
        List<EntryRow> rows = await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockAsync(context, current, person, token).ConfigureAwait(false);
                return await context.Entries.FromSqlRaw(
                    Page,
                    new NpgsqlParameter("person", person),
                    new NpgsqlParameter("after", after is not null),
                    new NpgsqlParameter("written_at", after?.WrittenAt ?? DateTimeOffset.UnixEpoch),
                    new NpgsqlParameter("id", after?.Entry ?? Guid.Empty),
                    new NpgsqlParameter("limit", limit + 1)).AsNoTracking().ToListAsync(token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
        List<EntryRow> page = [.. rows.Take(limit)];
        List<InAppEntry> entries = [];
        bool rejected = false;
        foreach (EntryRow row in page)
        {
            Opened opened = await OpenAsync(row, language, current, cancellationToken).ConfigureAwait(false);
            if (opened.Held is { } held)
            {
                return Result.Fail<InAppPage>(held);
            }

            rejected |= opened.Rejected;
            if (opened.Entry is { } entry)
            {
                entries.Add(entry);
            }
        }

        if (rejected)
        {
            await alerts.HoldsUntilAsync(
                "content-rejected",
                ContentContext.Entries,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["table"] = ContentContext.Entries },
                cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(new InAppPage
        {
            Entries = entries,
            Next = rows.Count > limit ? InAppCursor.Write(page[^1].WrittenAt, page[^1].Id) : null,
        });
    }

    public async ValueTask<int> UnreadCountAsync(string person, CancellationToken cancellationToken)
    {
        ready.ThrowIfNotReady();
        CheckPerson(person);
        MessagingOptions current = options.CurrentValue;
        return await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockAsync(context, current, person, token).ConfigureAwait(false);
                return await context.Entries.AsNoTracking().CountAsync(row => row.Person == person && row.ReadAt == null, token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Result> MarkReadAsync(string person, EntryId entry, CancellationToken cancellationToken)
    {
        ready.ThrowIfNotReady();
        CheckPerson(person);
        MessagingOptions current = options.CurrentValue;
        Marked? marked = await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockAsync(context, current, person, token).ConfigureAwait(false);
                EntryRow? row = await context.Entries.AsNoTracking().FirstOrDefaultAsync(each => each.Id == entry.Value && each.Person == person, token).ConfigureAwait(false);
                if (row is null)
                {
                    return null;
                }

                return row.ReadAt is null
                    ? await MarkAsync(context, current, person, [new Unmarked(row.Id, row.DeliveryId)], token).ConfigureAwait(false)
                    : new Marked(0, 0);
            },
            cancellationToken).ConfigureAwait(false);
        if (marked is null)
        {
            return Result.Fail(new Failure { Code = Codes.InAppUnknown, Details = new Dictionary<string, string>(StringComparer.Ordinal) });
        }

        Counted(marked);
        return Result.Success();
    }

    public async ValueTask<int> MarkAllReadAsync(string person, CancellationToken cancellationToken)
    {
        ready.ThrowIfNotReady();
        CheckPerson(person);
        MessagingOptions current = options.CurrentValue;
        Marked marked = await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockAsync(context, current, person, token).ConfigureAwait(false);
                List<Unmarked> unread = await context.Database.SqlQueryRaw<Unmarked>(Unread, new NpgsqlParameter("person", person)).ToListAsync(token).ConfigureAwait(false);
                return unread.Count == 0 ? new Marked(0, 0) : await MarkAsync(context, current, person, unread, token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
        Counted(marked);
        return marked.Entries;
    }

    /// <summary>
    /// Marks <paramref name="unread"/> read and moves each of their deliveries still <c>sent</c>
    /// to <c>read</c>, locking the deliveries' rows before the entries', in the order a
    /// delivery's deletion takes them, so the two never wait on each other.
    /// </summary>
    private async Task<Marked> MarkAsync(MessagingContext context, MessagingOptions current, string person, List<Unmarked> unread, CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.GetUtcNow();
        Guid[] deliveries = [.. unread.Where(each => each.DeliveryId is not null).Select(each => each.DeliveryId!.Value)];
        List<DeliveryRow> sent = deliveries.Length == 0
            ? []
            : await context.Deliveries.FromSqlRaw(LockSent, new NpgsqlParameter("ids", deliveries)).ToListAsync(cancellationToken).ConfigureAwait(false);
        List<Unmarked> changed = await context.Database.SqlQueryRaw<Unmarked>(
            MarkRead,
            new NpgsqlParameter("now", now),
            new NpgsqlParameter("ids", unread.Select(each => each.Id).ToArray()),
            new NpgsqlParameter("person", person)).ToListAsync(cancellationToken).ConfigureAwait(false);
        HashSet<Guid> moved = [.. changed.Where(each => each.DeliveryId is not null).Select(each => each.DeliveryId!.Value)];
        int count = 0;
        foreach (DeliveryRow row in sent.Where(row => moved.Contains(row.Id)))
        {
            DeliveryEnding.End(context, row, Spellings.Of(DeliveryStatus.Read), null, now, current.Observer.Enabled);
            count++;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new Marked(changed.Count, count);
    }

    private void Counted(Marked marked)
    {
        for (int each = 0; each < marked.Deliveries; each++)
        {
            notices.Count(Spellings.Of(Channel.InApp), Spellings.Of(DeliveryStatus.Read));
        }
    }

    private async Task LockAsync(MessagingContext context, MessagingOptions current, string person, CancellationToken cancellationToken)
    {
        await DeliveryClaims.LockTimeoutAsync(context, current.Dispatch.Timeout, cancellationToken).ConfigureAwait(false);
        await AdvisoryLocks.TakeAsync(context, AdvisoryLocks.Person(hashes, person), shared: true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The language an entry holding <paramref name="held"/> reads in: the one <paramref name="asked"/>
    /// matches, as a send's language is matched (<c>01</c> MSG-SEND-003); failing that the default
    /// language; failing that, a language declared after the entry was written, its send's.
    /// </summary>
    internal static string LanguageOf(IReadOnlyList<string> held, string? asked, string defaultLanguage, string sendLanguage)
    {
        if (asked is not null)
        {
            if (held.FirstOrDefault(language => string.Equals(language, asked, StringComparison.OrdinalIgnoreCase)) is { } exact)
            {
                return exact;
            }

            int dash = asked.IndexOf('-', StringComparison.Ordinal);
            string primary = dash < 0 ? asked : asked[..dash];
            if (held.FirstOrDefault(language => string.Equals(language, primary, StringComparison.OrdinalIgnoreCase)) is { } matched)
            {
                return matched;
            }
        }

        return held.FirstOrDefault(language => string.Equals(language, defaultLanguage, StringComparison.OrdinalIgnoreCase))
            ?? held.FirstOrDefault(language => string.Equals(language, sendLanguage, StringComparison.Ordinal))
            ?? held[0];
    }

    /// <summary>Opens the entry's parts in the language it reads in, and its target values, outside any transaction; the first value that is erased, rejected or cannot open yet decides.</summary>
    private async Task<Opened> OpenAsync(EntryRow row, string? asked, MessagingOptions current, CancellationToken cancellationToken)
    {
        if (InAppParts.Read(row.Parts) is not { } stored)
        {
            return new Opened(null, Rejected: true, Held: null);
        }

        List<string> held = [.. stored.Keys
            .Select(InAppParts.Of)
            .Where(field => field is { Part: TemplateParts.Title })
            .Select(field => field!.Value.Language)
            .Order(StringComparer.Ordinal)];
        if (held.Count == 0)
        {
            return new Opened(null, Rejected: true, Held: null);
        }

        string language = LanguageOf(held, asked, current.DefaultLanguage!, row.Language);
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        foreach (string field in new[] { InAppParts.Field(TemplateParts.Title, language), InAppParts.Field(TemplateParts.Body, language), InAppParts.Target })
        {
            if (!stored.TryGetValue(field, out byte[]? value))
            {
                continue;
            }

            ContentOpening opening = await OpenValueAsync(row, field, value, current, cancellationToken).ConfigureAwait(false);
            switch (opening.Status)
            {
                case ContentOpeningStatus.Opened:
                    values[field] = Encoding.UTF8.GetString(opening.Plaintext.Span);
                    break;
                case ContentOpeningStatus.Erased:
                    return new Opened(null, Rejected: false, Held: null);
                case ContentOpeningStatus.Rejected:
                    return new Opened(null, Rejected: true, Held: null);
                default:
                    return new Opened(null, Rejected: false, Held: new Failure
                    {
                        Code = Codes.KeyUnavailable,
                        Details = opening.Key is { } key
                            ? new Dictionary<string, string>(StringComparer.Ordinal) { ["key"] = key }
                            : new Dictionary<string, string>(StringComparer.Ordinal),
                    });
            }
        }

        IReadOnlyDictionary<string, SendValue> targetValues = new Dictionary<string, SendValue>(StringComparer.Ordinal);
        if (values.TryGetValue(InAppParts.Target, out string? text))
        {
            if (TargetValues.Read(text) is not { } read)
            {
                return new Opened(null, Rejected: true, Held: null);
            }

            targetValues = read;
        }

        return new Opened(
            new InAppEntry
            {
                Entry = new EntryId(row.Id),
                Kind = row.Kind,
                Category = row.Category,
                WrittenAt = row.WrittenAt,
                ReadAt = row.ReadAt,
                Language = language,
                Title = values[InAppParts.Field(TemplateParts.Title, language)],
                Body = values.GetValueOrDefault(InAppParts.Field(TemplateParts.Body, language)),
                Target = row.Target,
                TargetValues = targetValues,
            },
            Rejected: false,
            Held: null);
    }

    /// <summary>Opens one value bounded by <c>Hermes:Dispatch:Timeout</c>; a protector that throws or does not answer within it leaves the value unable to open yet.</summary>
    private async Task<ContentOpening> OpenValueAsync(EntryRow row, string field, byte[] value, MessagingOptions current, CancellationToken cancellationToken)
    {
        try
        {
            using CancellationTokenSource bound = new(current.Dispatch.Timeout, clock);
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(bound.Token, cancellationToken);
            return await protection.OpenAsync(row.Person, value, ContentContext.Of(ContentContext.Entries, row.Id, field), linked.Token)
                .AsTask()
                .WaitAsync(current.Dispatch.Timeout, clock, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception failure) when (failure is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return ContentOpening.Held(ContentEnvelope.Read(value).Id);
        }
    }

    private static void CheckPerson(string person)
    {
        if (person is not { Length: >= 1 and <= MostReferenceCharacters })
        {
            throw new ArgumentException("A person reference is 1 to 128 characters.", nameof(person));
        }
    }

    private sealed record Unmarked(Guid Id, Guid? DeliveryId);

    private sealed record Marked(int Entries, int Deliveries);

    private sealed record Opened(InAppEntry? Entry, bool Rejected, Failure? Held);
}