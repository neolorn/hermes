using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Conversations.Support;
using Hermes.Core;
using Hermes.Operations;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace Hermes.Conversations.Chats;

/// <summary>
/// The desk's tools (<c>09</c> MSG-CONV-005): tags, snoozing and saved replies. A support
/// conversation is tagged and snoozed by a member of its topic's scope; a topic's saved replies
/// are managed by its scope's members, and each person's own by them alone, anyone else
/// answered as for a reply unknown.
/// </summary>
internal sealed partial class StaffChats
{
    private const int MostNameCharacters = 64;

    private const int MostTopicReplies = 200;

    private const int MostOwnReplies = 100;

    private static readonly string SnoozedStatus = ConversationSpellings.Of(ConversationStatus.Snoozed);

    private static readonly TimeSpan LeastSnooze = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan MostSnooze = TimeSpan.FromDays(30);

    public async ValueTask<Result> SetTagsAsync(string person, ConversationId conversation, IReadOnlyCollection<string> tags, CancellationToken cancellationToken)
    {
        Begin(person);
        ArgumentNullException.ThrowIfNull(tags);
        if (await DeskReachAsync(person, conversation.Value, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return Result.Fail(refused);
        }

        string[] set = [.. tags.Distinct(StringComparer.Ordinal)];
        MessagingOptions current = options.CurrentValue;
        Failure? failure = await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockAsync(context, current, [person], token).ConfigureAwait(false);
                ConversationRow? row = await LockedConversationAsync(context, conversation.Value, token).ConfigureAwait(false);
                if (row is null)
                {
                    return NotFound();
                }

                if (string.Equals(row.Status, ClosedStatus, StringComparison.Ordinal))
                {
                    return Plain(ChatCodes.ConversationClosed);
                }

                // A tag dropped from the declaration stays where it is, and only a new one must be declared.
                if (set.Any(tag => !settings.Support.Tags.Contains(tag, StringComparer.Ordinal) && !row.Tags.Contains(tag, StringComparer.Ordinal)))
                {
                    return Invalid(ChatCodes.ConversationInvalid, "tags");
                }

                row.Tags = set;
                await context.SaveChangesAsync(token).ConfigureAwait(false);
                return null;
            },
            cancellationToken).ConfigureAwait(false);
        return failure is null ? Result.Success() : Result.Fail(failure);
    }

    public async ValueTask<Result> SnoozeAsync(string person, ConversationId conversation, DateTimeOffset until, CancellationToken cancellationToken)
    {
        Begin(person);
        if (await DeskReachAsync(person, conversation.Value, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return Result.Fail(refused);
        }

        MessagingOptions current = options.CurrentValue;
        Failure? failure = await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockAsync(context, current, [person], token).ConfigureAwait(false);
                ConversationRow? row = await LockedConversationAsync(context, conversation.Value, token).ConfigureAwait(false);
                if (row is null)
                {
                    return NotFound();
                }

                if (string.Equals(row.Status, ClosedStatus, StringComparison.Ordinal))
                {
                    return Plain(ChatCodes.ConversationClosed);
                }

                DateTimeOffset now = clock.GetUtcNow();
                if (until < now + LeastSnooze || until > now + MostSnooze)
                {
                    return Invalid(ChatCodes.ConversationInvalid, "until");
                }

                bool wasQueued = row.Assignee is null && string.Equals(row.Status, OpenStatus, StringComparison.Ordinal);
                row.Status = SnoozedStatus;
                row.SnoozedUntil = until;
                await context.SaveChangesAsync(token).ConfigureAwait(false);
                if (wasQueued)
                {
                    await Live.LiveNotices.TellQueueAsync(context, row.Topic!, token).ConfigureAwait(false);
                }

                return null;
            },
            cancellationToken).ConfigureAwait(false);
        return failure is null ? Result.Success() : Result.Fail(failure);
    }

    public async ValueTask<Result<IReadOnlyList<SavedReplyRecord>>> RepliesAsync(string person, string? topic, CancellationToken cancellationToken)
    {
        Begin(person);
        if (topic is not null)
        {
            if (!settings.Support.Topics.TryGetValue(topic, out SupportTopic? configured))
            {
                return Result.Fail<IReadOnlyList<SavedReplyRecord>>(Malformed("topic"));
            }

            if (!await membership.IsMemberAsync(person, configured.Scope, cancellationToken).ConfigureAwait(false))
            {
                return Result.Fail<IReadOnlyList<SavedReplyRecord>>(Plain(ChatCodes.ReplyNotFound));
            }
        }

        MessagingOptions current = options.CurrentValue;
        List<SupportReplyRow> rows = await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockAsync(context, current, [person], token).ConfigureAwait(false);
                IQueryable<SupportReplyRow> owned = topic is null
                    ? context.SupportReplies.Where(each => each.Person == person)
                    : context.SupportReplies.Where(each => each.Topic == topic);
                return await owned.AsNoTracking().OrderBy(each => each.Name).ThenBy(each => each.Id).ToListAsync(token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
        return Result.Success<IReadOnlyList<SavedReplyRecord>>([.. rows.Select(RecordOf)]);
    }

    public async ValueTask<Result<SavedReplyRecord>> AddReplyAsync(string person, string name, string text, string? topic, CancellationToken cancellationToken)
    {
        Begin(person);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(text);
        if (ReplyBounds(name, text) is { } invalid)
        {
            return Result.Fail<SavedReplyRecord>(invalid);
        }

        if (topic is not null && !await IsTopicMemberAsync(person, topic, cancellationToken).ConfigureAwait(false))
        {
            return Result.Fail<SavedReplyRecord>(Plain(ChatCodes.ReplyNotFound));
        }

        MessagingOptions current = options.CurrentValue;
        (SupportReplyRow? Row, Failure? Failure) added = await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockAsync(context, current, [person], token).ConfigureAwait(false);
                await LockRepliesAsync(context, topic, topic is null ? person : null, token).ConfigureAwait(false);
                IQueryable<SupportReplyRow> owned = topic is null
                    ? context.SupportReplies.Where(each => each.Person == person)
                    : context.SupportReplies.Where(each => each.Topic == topic);
                if (await owned.CountAsync(token).ConfigureAwait(false) >= (topic is null ? MostOwnReplies : MostTopicReplies))
                {
                    return (null, Plain(ChatCodes.ReplyFull));
                }

                if (await owned.AnyAsync(each => each.Name == name, token).ConfigureAwait(false))
                {
                    return (null, Plain(ChatCodes.ReplyNameExists));
                }

                SupportReplyRow row = new()
                {
                    Id = Guid.CreateVersion7(),
                    Topic = topic,
                    Person = topic is null ? person : null,
                    Name = name,
                    Text = text,
                    CreatedAt = clock.GetUtcNow(),
                };
                context.SupportReplies.Add(row);
                await context.SaveChangesAsync(token).ConfigureAwait(false);
                return ((SupportReplyRow?)row, (Failure?)null);
            },
            cancellationToken).ConfigureAwait(false);
        return added.Failure is { } failed ? Result.Fail<SavedReplyRecord>(failed) : Result.Success(RecordOf(added.Row!));
    }

    public async ValueTask<Result> ChangeReplyAsync(string person, SavedReplyId reply, string name, string text, CancellationToken cancellationToken)
    {
        Begin(person);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(text);
        if (await ReachReplyAsync(person, reply.Value, cancellationToken).ConfigureAwait(false) is not { } reached)
        {
            return Result.Fail(Plain(ChatCodes.ReplyNotFound));
        }

        if (ReplyBounds(name, text) is { } invalid)
        {
            return Result.Fail(invalid);
        }

        MessagingOptions current = options.CurrentValue;
        Failure? failure = await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockAsync(context, current, [person], token).ConfigureAwait(false);
                await LockRepliesAsync(context, reached.Topic, reached.Person, token).ConfigureAwait(false);
                SupportReplyRow? row = await context.SupportReplies.FirstOrDefaultAsync(each => each.Id == reached.Id, token).ConfigureAwait(false);
                if (row is null)
                {
                    return Plain(ChatCodes.ReplyNotFound);
                }

                IQueryable<SupportReplyRow> owned = row.Topic is { } shared
                    ? context.SupportReplies.Where(each => each.Topic == shared)
                    : context.SupportReplies.Where(each => each.Person == row.Person);
                if (await owned.AnyAsync(each => each.Name == name && each.Id != row.Id, token).ConfigureAwait(false))
                {
                    return Plain(ChatCodes.ReplyNameExists);
                }

                row.Name = name;
                row.Text = text;
                await context.SaveChangesAsync(token).ConfigureAwait(false);
                return null;
            },
            cancellationToken).ConfigureAwait(false);
        return failure is null ? Result.Success() : Result.Fail(failure);
    }

    public async ValueTask<Result> RemoveReplyAsync(string person, SavedReplyId reply, CancellationToken cancellationToken)
    {
        Begin(person);
        if (await ReachReplyAsync(person, reply.Value, cancellationToken).ConfigureAwait(false) is not { } reached)
        {
            return Result.Fail(Plain(ChatCodes.ReplyNotFound));
        }

        MessagingOptions current = options.CurrentValue;
        await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockAsync(context, current, [person], token).ConfigureAwait(false);
                return await context.SupportReplies.Where(each => each.Id == reached.Id).ExecuteDeleteAsync(token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <summary>
    /// The refusal of tagging or snoozing <paramref name="conversation"/> for
    /// <paramref name="person"/>: one they may not read is unknown; one they may read that is not
    /// support is outside the rule; none where it is a support conversation of a topic whose
    /// scope they are a member of.
    /// </summary>
    private async Task<Failure?> DeskReachAsync(string person, Guid conversation, CancellationToken cancellationToken)
    {
        if (await ReachAsync(person, conversation, cancellationToken).ConfigureAwait(false) is not { } reached)
        {
            return NotFound();
        }

        return string.Equals(reached.Type, SupportType, StringComparison.Ordinal) ? null : Invalid(ChatCodes.ConversationInvalid, "type");
    }

    /// <summary>The saved reply <paramref name="reply"/>, where it is <paramref name="person"/>'s own or shared by a topic whose scope they are a member of; none otherwise.</summary>
    private async Task<SupportReplyRow?> ReachReplyAsync(string person, Guid reply, CancellationToken cancellationToken)
    {
        SupportReplyRow? row;
        MessagingContext context = database.Context();
        await using (context.ConfigureAwait(false))
        {
            row = await context.SupportReplies.AsNoTracking().FirstOrDefaultAsync(each => each.Id == reply, cancellationToken).ConfigureAwait(false);
        }

        if (row is null)
        {
            return null;
        }

        return row.Topic is { } topic
            ? await IsTopicMemberAsync(person, topic, cancellationToken).ConfigureAwait(false) ? row : null
            : string.Equals(row.Person, person, StringComparison.Ordinal) ? row : null;
    }

    private async Task<bool> IsTopicMemberAsync(string person, string topic, CancellationToken cancellationToken)
    {
        return settings.Support.Topics.TryGetValue(topic, out SupportTopic? configured)
            && await membership.IsMemberAsync(person, configured.Scope, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Takes, until the transaction ends, the lock of the replies of <paramref name="topic"/>,
    /// or of <paramref name="person"/>'s own where none, so that two writers of one owner's
    /// replies count them and check their names in turn.
    /// </summary>
    private static async Task LockRepliesAsync(MessagingContext context, string? topic, string? person, CancellationToken cancellationToken)
    {
        string owner = topic is not null ? "support_replies:topic:" + topic : "support_replies:person:" + person;
        long key = BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(owner)));
        await context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(@key)", [new NpgsqlParameter("key", key)], cancellationToken).ConfigureAwait(false);
    }

    private static Failure? ReplyBounds(string name, string text)
    {
        if (name.Length is < 1 or > MostNameCharacters)
        {
            return Invalid(ChatCodes.ReplyInvalid, "name");
        }

        return IsText(text) ? null : Invalid(ChatCodes.ReplyInvalid, "text");
    }

    private static SavedReplyRecord RecordOf(SupportReplyRow row)
    {
        return new SavedReplyRecord { Id = new SavedReplyId(row.Id), Name = row.Name, Text = row.Text, Topic = row.Topic };
    }
}