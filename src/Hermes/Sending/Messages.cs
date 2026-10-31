using System;
using System.Buffers.Binary;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Channels;
using Hermes.Content;
using Hermes.Core;
using Hermes.Operations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Hermes.Sending;

/// <summary>
/// The host's one way to send (<c>01</c> MSG-SEND-003 to MSG-SEND-005, MSG-SEND-008,
/// MSG-SEND-009, MSG-SEND-017, MSG-SEND-018): a send is checked, rendered and recorded with its
/// deliveries, in the host's transaction where it gives one, and nothing is handed to a
/// provider; a scoped service, resolving the content protector from the scope it was called in.
/// </summary>
internal sealed partial class Messages(
    MessagingReady ready,
    Declarations declarations,
    IOptionsMonitor<MessagingOptions> options,
    IOptionsMonitor<ChannelsOptions> channels,
    SendRenderer renderer,
    ContentProtection protection,
    KeyedHashes hashes,
    UnsubscribeTokens tokens,
    MessagingDatabase database,
    HostSchemas schemas,
    Alerts alerts,
    Notices notices,
    TimeProvider clock,
    ILogger<Messages> logger) : IMessages
{
    private const int MostReferenceCharacters = 128;

    private const int MostKeyCharacters = 128;

    private const int MostTopicCharacters = 128;

    private const int TopicBytes = 24;

    private const string Notify = "SELECT pg_notify('messaging_dispatch', '')";

    private const string Cancel = """
        UPDATE messaging.deliveries
        SET status = 'cancelled', reason = NULL, last_moved_at = @now, claimed_by = NULL, claimed_at = NULL, claim_expires_at = NULL,
            next_sequence = next_sequence + @advance
        WHERE id IN (SELECT id FROM messaging.deliveries WHERE send_id = @send AND status = 'waiting' ORDER BY id FOR UPDATE)
          AND status = 'waiting'
        RETURNING id AS "Id", channel AS "Channel", next_sequence - @advance AS "Sequence"
        """;

    public ValueTask<Result<SendReceipt>> SendAsync(Send send, CancellationToken cancellationToken)
    {
        return RecordAsync(send, host: null, cancellationToken);
    }

    public ValueTask<Result<SendReceipt>> SendAsync(Send send, DbTransaction transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        return transaction is NpgsqlTransaction { Connection: not null } open
            ? RecordAsync(send, open, cancellationToken)
            : throw new ArgumentException("The transaction is not an open NpgsqlTransaction.", nameof(transaction));
    }

    private async ValueTask<Result<SendReceipt>> RecordAsync(Send send, NpgsqlTransaction? host, CancellationToken cancellationToken)
    {
        ready.ThrowIfNotReady();
        MessagingOptions current = options.CurrentValue;
        if (!current.Messages.Enabled)
        {
            throw new InvalidOperationException("This application does not record sends: Hermes:Messages:Enabled is off.");
        }

        ArgumentNullException.ThrowIfNull(send);

        KindDeclaration kind = Kind(send);
        string? person = Person(send.Recipient);
        CheckBounds(send);
        SendValues.Check(send.Values);

        (IReadOnlyList<Channel>? chosen, Failure? noDestination) = Choose(kind, send, person);
        if (noDestination is not null)
        {
            return Result.Fail<SendReceipt>(noDestination);
        }

        (string? email, string? phone, Failure? unread) = Read(send, chosen!);
        if (unread is not null)
        {
            return Result.Fail<SendReceipt>(unread);
        }

        string language = SendLanguage.Resolve(send.Language, current.Languages, current.DefaultLanguage!);
        string zone = SendLanguage.Zone(send.TimeZone, current.TimeZone);
        renderer.CheckPassed(kind, chosen!, language, current.Languages, send.Values);
        IReadOnlyDictionary<string, SendValue>? targets = chosen!.Contains(Channel.InApp) && kind.InApp!.Target is not null ? TargetValues.Of(kind, send.Values) : null;
        string? unsubscribe = chosen!.Contains(Channel.Email) && IsOptional(kind) ? Unsubscribe(kind, language, email!, send.NotBefore, current) : null;
        SendRender render = await renderer.RenderAsync(kind, chosen!, language, current.Languages, zone, send.Values, unsubscribe).ConfigureAwait(false);
        if (render.Refusal is { } refusal)
        {
            return Result.Fail<SendReceipt>(Failed(refusal, ("channel", Spellings.Of(render.Channel!.Value))));
        }

        Prepared prepared = new(send, kind, person, email, phone, language, zone, render.Channels, unsubscribe, targets);
        Result<SendReceipt> result;
        if (host is null)
        {
            result = await LibraryTransactions.RunAsync(database, current, (context, token) => RecordAsync(context, prepared, current, token), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            MessagingContext context = MessagingContext.On(host.Connection!);
            await using (context.ConfigureAwait(false))
            {
                await context.Database.UseTransactionAsync(host, cancellationToken).ConfigureAwait(false);
                await schemas.CheckAsync(context, host.Connection!.ConnectionString, cancellationToken).ConfigureAwait(false);
                result = await RecordAsync(context, prepared, current, cancellationToken).ConfigureAwait(false);
            }
        }

        if (!result.IsSuccess && result.Failure!.Code == Codes.KeyUnavailable)
        {
            Dictionary<string, string> details = [];
            if (result.Failure.Details.TryGetValue("version", out string? version))
            {
                details["version"] = version;
            }

            await alerts.HoldsUntilAsync("key-unavailable", result.Failure.Details.GetValueOrDefault("key"), details, cancellationToken).ConfigureAwait(false);
            result = Result.Fail<SendReceipt>(new Failure { Code = Codes.KeyUnavailable, Details = result.Failure.Details.Where(detail => detail.Key == "key").ToDictionary() });
        }
        else if (result.IsSuccess)
        {
            Recorded(logger, result.Value!.Send.Value, kind.Name, result.Value.Deliveries.Count);
        }

        return result;
    }

    public async ValueTask<Result<IReadOnlyList<DeliveryId>>> CancelAsync(SendId send, CancellationToken cancellationToken)
    {
        ready.ThrowIfNotReady();
        MessagingOptions current = options.CurrentValue;
        return await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                if (!await context.Sends.AnyAsync(row => row.Id == send.Value, token).ConfigureAwait(false))
                {
                    return Result.Fail<IReadOnlyList<DeliveryId>>(Failed(Codes.SendUnknown));
                }

                DateTimeOffset now = clock.GetUtcNow();
                bool observe = current.Observer.Enabled;
                List<Cancelled> cancelled = await context.Database.SqlQueryRaw<Cancelled>(
                    Cancel,
                    new NpgsqlParameter("now", now),
                    new NpgsqlParameter("send", send.Value),
                    new NpgsqlParameter("advance", observe ? 1 : 0)).ToListAsync(token).ConfigureAwait(false);
                foreach (Cancelled delivery in cancelled)
                {
                    if (observe)
                    {
                        Notices.Add(context, delivery.Id, delivery.Sequence, Spellings.Of(DeliveryStatus.Cancelled), null, now);
                    }
                }

                if (cancelled.Count > 0)
                {
                    await Places.ReleaseAsync(context, [.. cancelled.Select(delivery => delivery.Id)], token).ConfigureAwait(false);
                }

                await context.SaveChangesAsync(token).ConfigureAwait(false);
                foreach (Cancelled delivery in cancelled)
                {
                    notices.Count(delivery.Channel, Spellings.Of(DeliveryStatus.Cancelled));
                }

                return Result.Success<IReadOnlyList<DeliveryId>>([.. cancelled.OrderBy(delivery => delivery.Id).Select(delivery => new DeliveryId(delivery.Id))]);
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Result<SendRecord>> ReadAsync(SendId send, CancellationToken cancellationToken)
    {
        ready.ThrowIfNotReady();
        MessagingContext context = database.Context();
        await using (context.ConfigureAwait(false))
        {
            if (await context.Sends.AsNoTracking().FirstOrDefaultAsync(row => row.Id == send.Value, cancellationToken).ConfigureAwait(false) is not { } row)
            {
                return Result.Fail<SendRecord>(Failed(Codes.SendUnknown));
            }

            List<DeliveryRow> deliveries = await context.Deliveries.AsNoTracking().Where(delivery => delivery.SendId == send.Value).OrderBy(delivery => delivery.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
            Guid[] ids = [.. deliveries.Select(delivery => delivery.Id)];
            List<AttemptRow> attempts = await context.Attempts.AsNoTracking().Where(attempt => ids.Contains(attempt.DeliveryId)).ToListAsync(cancellationToken).ConfigureAwait(false);
            List<CallRow> calls = await context.Calls.AsNoTracking().Where(call => ids.Contains(call.DeliveryId)).ToListAsync(cancellationToken).ConfigureAwait(false);
            List<DeliveryRecord> records = [];
            foreach (DeliveryRow delivery in deliveries)
            {
                if (DeliveryRecords.Of(delivery, attempts, calls) is { } record)
                {
                    records.Add(record);
                }
            }

            return Result.Success(new SendRecord { Send = send, Kind = row.Kind, Deliveries = records });
        }
    }

    private async Task<Result<SendReceipt>> RecordAsync(MessagingContext context, Prepared prepared, MessagingOptions current, CancellationToken cancellationToken)
    {
        Send send = prepared.Send;
        DateTimeOffset now = clock.GetUtcNow();
        IReadOnlyList<int> held = hashes.Versions;
        IdempotencyRow? lapsed = null;
        byte[]? form = null;
        if (send.IdempotencyKey is { } key)
        {
            await AdvisoryLocks.TakeAsync(context, [AdvisoryLocks.Idempotency(prepared.Kind.Name, key)], shared: false, cancellationToken).ConfigureAwait(false);
        }

        if (prepared.Person is { } person)
        {
            await AdvisoryLocks.TakeAsync(context, AdvisoryLocks.Person(hashes, person), shared: true, cancellationToken).ConfigureAwait(false);
        }

        if (send.IdempotencyKey is { } idempotencyKey)
        {
            List<byte[]> scopes = [.. held.Select(version => Scope(prepared, version))];
            bool anonymous = prepared.Person is null;
            List<IdempotencyRow> rows = await context.Idempotency
                .Where(row => row.Kind == prepared.Kind.Name && row.Anonymous == anonymous && row.Key == idempotencyKey && scopes.Contains(row.Scope))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            if (await KeysInUse.MissingAsync(context, KeyNeeds.HashKeys, [.. held], cancellationToken).ConfigureAwait(false) is { } missing)
            {
                return Unavailable(KeyNeeds.HashKeys, missing);
            }

            form = SendForm.Of(send);
            foreach (IdempotencyRow row in rows)
            {
                if (row.LapsesAt <= now)
                {
                    lapsed ??= row;
                    continue;
                }

                if (!held.Contains(row.HashVersion))
                {
                    return Unavailable(KeyNeeds.HashKeys, row.HashVersion);
                }

                byte[] digest = hashes.Under(row.HashVersion, KeyedHashes.Idempotency, form);
                return CryptographicOperations.FixedTimeEquals(digest, row.Digest)
                    ? Result.Success(Receipts.Read(row.Receipt))
                    : Result.Fail<SendReceipt>(Failed(Codes.IdempotencyConflict));
            }
        }

        Guid sendId = Guid.CreateVersion7(now);
        List<DeliveryRow> deliveries = [];
        foreach (RenderedChannel rendered in prepared.Channels)
        {
            Guid id = Guid.CreateVersion7(now);
            DateTimeOffset due = send.NotBefore ?? now;
            DeliveryRow delivery = new()
            {
                Id = id,
                SendId = sendId,
                Kind = prepared.Kind.Name,
                Category = prepared.Kind.Category,
                Channel = Spellings.Of(rendered.Channel),
                Status = Spellings.Of(DeliveryStatus.Waiting),
                Person = prepared.Person,
                Language = prepared.Language,
                TimeZone = prepared.Zone,
                RecordedAt = now,
                DueAt = due,
                BecameDueAt = due,
                LastMovedAt = now,
                NextSequence = 1,
            };
            if (rendered.Channel == Channel.Push && send.PushTopic is { } topic)
            {
                byte[] hash = hashes.Write(KeyedHashes.PushTopic, Encoding.UTF8.GetBytes($"{prepared.Kind.Name}\0{topic}"));
                delivery.PushTopic = Base64Url.EncodeToString(hash.AsSpan(0, TopicBytes));
            }

            string? destination = rendered.Channel switch
            {
                Channel.Email => prepared.Email,
                Channel.Sms => prepared.Phone,
                _ => null,
            };
            List<(string Field, string Text)> values = [.. rendered.Parts.Select(part => (part.Key, part.Value))];
            if (destination is not null)
            {
                values.Add(("destination", destination));
            }

            if (rendered.Channel == Channel.Email && prepared.Unsubscribe is { } address)
            {
                values.Add(("unsubscribe", address));
            }

            if (rendered.Channel == Channel.InApp && prepared.Targets is { } targets)
            {
                values.Add((InAppParts.Target, TargetValues.Write(targets)));
            }

            Dictionary<string, byte[]> inApp = new(StringComparer.Ordinal);
            foreach ((string field, string text) in values)
            {
                ContentProtecting made = await protection.ProtectAsync(prepared.Person, Encoding.UTF8.GetBytes(text), ContentContext.Of(ContentContext.Deliveries, id, field), cancellationToken).ConfigureAwait(false);
                if (made.Stored is not { } stored)
                {
                    return Unavailable(made.Key!, made.Version);
                }

                if (rendered.Channel == Channel.InApp)
                {
                    inApp[field] = stored;
                }
                else
                {
                    Store(delivery, field, stored);
                }

                if (LibraryVersion(stored) is { } version)
                {
                    delivery.ContentKeyVersion = version;
                }
            }

            if (rendered.Channel == Channel.InApp)
            {
                delivery.Parts = InAppParts.Write(inApp);
            }

            deliveries.Add(delivery);
        }

        SendReceipt receipt = new()
        {
            Send = new SendId(sendId),
            Deliveries = [.. deliveries.Select(delivery => new DeliveryReceipt { Channel = Spellings.Channel(delivery.Channel)!.Value, Delivery = new DeliveryId(delivery.Id) })],
        };
        if (lapsed is not null)
        {
            context.Idempotency.Remove(lapsed);
        }

        context.Sends.Add(new SendRow { Id = sendId, Kind = prepared.Kind.Name, RecordedAt = now, IdempotencyKey = send.IdempotencyKey });
        context.Deliveries.AddRange(deliveries);
        if (send.IdempotencyKey is { } written)
        {
            int version = hashes.WriteVersion;
            context.Idempotency.Add(new IdempotencyRow
            {
                Kind = prepared.Kind.Name,
                Anonymous = prepared.Person is null,
                Scope = Scope(prepared, version),
                Key = written,
                SendId = sendId,
                Receipt = Receipts.Write(receipt),
                Digest = hashes.Under(version, KeyedHashes.Idempotency, form!),
                HashVersion = version,
                RecordedAt = now,
                LapsesAt = now + current.Idempotency.Window,
            });
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (current.Dispatch.Notify)
        {
            await context.Database.ExecuteSqlRawAsync(Notify, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(receipt);
    }

    private bool IsOptional(KindDeclaration kind)
    {
        return declarations.Categories.Any(category => string.Equals(category.Name, kind.Category, StringComparison.Ordinal) && !category.Essential);
    }

    /// <summary>
    /// The one-click unsubscribe address of an optional category's email (<c>02</c> MSG-EMAIL-006):
    /// <c>Hermes:PublicBaseAddress</c>, <c>/unsubscribe/</c> and a token dated from the later of now and
    /// the send's <c>NotBefore</c>, made under the write versions.
    /// </summary>
    private string Unsubscribe(KindDeclaration kind, string language, string email, DateTimeOffset? notBefore, MessagingOptions current)
    {
        DateTimeOffset now = clock.GetUtcNow();
        DateTimeOffset from = notBefore is { } later && later > now ? later : now;
        int version = hashes.WriteVersion;
        byte[] hash = hashes.Under(version, KeyedHashes.Email, Encoding.UTF8.GetBytes(Destinations.EmailHashed(email)));
        string token = tokens.Make(version, kind.Category, language, from, hash);
        return current.PublicBaseAddress!.AbsoluteUri.TrimEnd('/') + "/unsubscribe/" + token;
    }

    private KindDeclaration Kind(Send send)
    {
        ArgumentNullException.ThrowIfNull(send.Kind, nameof(send));
        return declarations.Kinds.FirstOrDefault(kind => string.Equals(kind.Name, send.Kind, StringComparison.Ordinal))
            ?? throw new ArgumentException($"The kind '{send.Kind}' is not declared, or is retired.", nameof(send));
    }

    private static string? Person(Recipient recipient)
    {
        return recipient switch
        {
            Recipient.Person { Reference: { Length: >= 1 and <= MostReferenceCharacters } reference } => reference,
            Recipient.Person => throw new ArgumentException("A person reference is 1 to 128 characters.", nameof(recipient)),
            Recipient.Anonymous => null,
            _ => throw new ArgumentNullException(nameof(recipient)),
        };
    }

    private static void CheckBounds(Send send)
    {
        if (send.IdempotencyKey is { } key && (key.Length is < 1 or > MostKeyCharacters || key.Any(character => character is < ' ' or > '~')))
        {
            throw new ArgumentException("An idempotency key is 1 to 128 printable ASCII characters.", nameof(send));
        }

        if (send.PushTopic is { Length: < 1 or > MostTopicCharacters })
        {
            throw new ArgumentException("A push topic is 1 to 128 characters.", nameof(send));
        }

        ArgumentNullException.ThrowIfNull(send.Values, nameof(send));
    }

    /// <summary>The channels the send uses, or the refusal of one with no destination.</summary>
    private static (IReadOnlyList<Channel>? Chosen, Failure? Refusal) Choose(KindDeclaration kind, Send send, string? person)
    {
        List<Channel> declared = [];
        if (kind.Email is not null)
        {
            declared.Add(Channel.Email);
        }

        if (kind.Sms is not null)
        {
            declared.Add(Channel.Sms);
        }

        if (kind.Push is not null)
        {
            declared.Add(Channel.Push);
        }

        if (kind.InApp is not null)
        {
            declared.Add(Channel.InApp);
        }

        bool HasDestination(Channel channel) => channel switch
        {
            Channel.Email => send.Email is not null,
            Channel.Sms => send.Phone is not null,
            _ => person is not null,
        };

        if (send.Channels is { } named)
        {
            foreach (Channel channel in named)
            {
                if (!declared.Contains(channel))
                {
                    throw new ArgumentException($"The kind '{kind.Name}' does not declare the channel {channel}.", nameof(send));
                }
            }

            List<Channel> chosen = [.. declared.Where(named.Contains)];
            foreach (Channel channel in chosen)
            {
                if (!HasDestination(channel))
                {
                    return (null, Failed(Codes.NoDestination, ("channel", Spellings.Of(channel))));
                }
            }

            return chosen.Count == 0 ? (null, Failed(Codes.NoDestination)) : (chosen, null);
        }

        List<Channel> reachable = [.. declared.Where(HasDestination)];
        return reachable.Count == 0 ? (null, Failed(Codes.NoDestination)) : (reachable, null);
    }

    /// <summary>The chosen channels' destinations in the form they are sent, or the refusal of one that does not read or is not allowed.</summary>
    private (string? Email, string? Phone, Failure? Refusal) Read(Send send, IReadOnlyList<Channel> chosen)
    {
        string? email = null;
        string? phone = null;
        if (chosen.Contains(Channel.Email))
        {
            email = Destinations.Email(send.Email!);
            if (email is null)
            {
                return (null, null, Failed(Codes.DestinationInvalid, ("channel", Spellings.Of(Channel.Email))));
            }
        }

        if (chosen.Contains(Channel.Sms))
        {
            if (!Destinations.IsPhone(send.Phone!))
            {
                return (null, null, Failed(Codes.DestinationInvalid, ("channel", Spellings.Of(Channel.Sms))));
            }

            if (!SenderRule.Allows(channels.CurrentValue.Sms.AllowedPrefixes, send.Phone!))
            {
                return (null, null, Failed(Codes.DestinationNotAllowed, ("channel", Spellings.Of(Channel.Sms))));
            }

            phone = send.Phone;
        }

        return (email, phone, null);
    }

    /// <summary>The idempotency scope under <paramref name="version"/>: the person reference, or the destinations' keyed hashes, an absent one as 32 zero bytes.</summary>
    private byte[] Scope(Prepared prepared, int version)
    {
        if (prepared.Person is { } person)
        {
            return Encoding.UTF8.GetBytes(person);
        }

        byte[] scope = new byte[2 * SHA256.HashSizeInBytes];
        if (prepared.Email is { } email)
        {
            hashes.Under(version, KeyedHashes.Email, Encoding.UTF8.GetBytes(Destinations.EmailHashed(email))).CopyTo(scope, 0);
        }

        if (prepared.Phone is { } phone)
        {
            hashes.Under(version, KeyedHashes.Sms, Encoding.UTF8.GetBytes(phone)).CopyTo(scope, SHA256.HashSizeInBytes);
        }

        return scope;
    }

    private static void Store(DeliveryRow delivery, string field, byte[] stored)
    {
        switch (field)
        {
            case "destination":
                delivery.Destination = stored;
                break;
            case "unsubscribe":
                delivery.Unsubscribe = stored;
                break;
            case TemplateParts.Subject:
                delivery.Subject = stored;
                break;
            case TemplateParts.Text:
                delivery.Text = stored;
                break;
            case TemplateParts.Html:
                delivery.Html = stored;
                break;
            case TemplateParts.Title:
                delivery.Title = stored;
                break;
            case TemplateParts.Body:
                delivery.Body = stored;
                break;
            case TemplateParts.Url:
                delivery.Url = stored;
                break;
            case TemplateParts.Tag:
                delivery.Tag = stored;
                break;
            case TemplateParts.Icon:
                delivery.Icon = stored;
                break;
            case TemplateParts.Badge:
                delivery.Badge = stored;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(field), field, "A field no delivery holds.");
        }
    }

    /// <summary>The <c>contentKeys</c> version a value of the library's protector was made under, none for another protector's.</summary>
    internal static int? LibraryVersion(byte[] stored)
    {
        ContentEnvelope.Envelope envelope = ContentEnvelope.Read(stored);
        return string.Equals(envelope.Id, LibraryProtector.LibraryId, StringComparison.Ordinal) ? BinaryPrimitives.ReadInt32BigEndian(envelope.Output.Span) : null;
    }

    private static Result<SendReceipt> Unavailable(string key, int? version)
    {
        Dictionary<string, string> details = new(StringComparer.Ordinal) { ["key"] = key };
        if (version is { } held)
        {
            details["version"] = held.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return Result.Fail<SendReceipt>(new Failure { Code = Codes.KeyUnavailable, Details = details });
    }

    private static Failure Failed(string code, params (string Name, string Value)[] details)
    {
        return new Failure { Code = code, Details = details.ToDictionary(detail => detail.Name, detail => detail.Value, StringComparer.Ordinal) };
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Send {Send} of kind {Kind} recorded with {Deliveries} deliveries.")]
    private static partial void Recorded(ILogger logger, Guid send, string kind, int deliveries);

    private sealed record Prepared(Send Send, KindDeclaration Kind, string? Person, string? Email, string? Phone, string Language, string Zone, IReadOnlyList<RenderedChannel> Channels, string? Unsubscribe, IReadOnlyDictionary<string, SendValue>? Targets);

    private sealed record Cancelled(Guid Id, string Channel, int Sequence);
}