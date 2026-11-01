using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Conversations.Attachments;
using Hermes.Core;
using Hermes.Operations;
using Hermes.Sending;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Hermes.Conversations.Chats;

/// <summary>
/// The conversations of a person's export (<c>09</c> MSG-CONV-010; <c>01</c> MSG-SEND-020;
/// M-038): their support conversations as the customer, with every message but notes, and the
/// staff chats they wrote in, with their own messages only; each message's author as
/// <c>person</c>, <c>staff</c> or <c>library</c>, so no one else's reference appears; the
/// attachments' names, media types and sizes; and their survey answer. A text or comment that
/// does not open is marked, raising <c>content-rejected</c>, and one that cannot open yet
/// answers the export <c>messaging.key.unavailable</c>. A scoped service.
/// </summary>
internal sealed class ConversationsExport(
    ContentProtection protection,
    Alerts alerts,
    IOptionsMonitor<MessagingOptions> options,
    TimeProvider clock) : ISubjectConversations
{
    private static readonly string Support = ConversationSpellings.Of(ConversationType.Support);

    private static readonly string Direct = ConversationSpellings.Of(ConversationType.Direct);

    private static readonly string Group = ConversationSpellings.Of(ConversationType.Group);

    private static readonly string Note = ConversationSpellings.Of(MessageType.Note);

    private static readonly string Person = ConversationSpellings.Of(AuthorKind.Person);

    private static readonly string Library = ConversationSpellings.Of(AuthorKind.Library);

    public async Task<Func<CancellationToken, Task<Result<IReadOnlyList<ConversationExport>>>>> ReadAsync(MessagingContext context, string person, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        List<ConversationRow> supports = await context.Conversations.AsNoTracking()
            .Where(each => each.Type == Support && each.Customer == person)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        List<ConversationRow> chats = await context.Conversations.AsNoTracking()
            .Where(each => (each.Type == Direct || each.Type == Group)
                && context.Messages.Any(message => message.ConversationId == each.Id && message.AuthorKind == Person && message.Author == person))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        Guid[] supportIds = [.. supports.Select(each => each.Id)];
        Guid[] chatIds = [.. chats.Select(each => each.Id)];
        List<MessageRow> messages = await context.Messages.AsNoTracking()
            .Where(each => (supportIds.Contains(each.ConversationId) && each.Type != Note)
                || (chatIds.Contains(each.ConversationId) && each.AuthorKind == Person && each.Author == person))
            .OrderBy(each => each.SentAt).ThenBy(each => each.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        Guid[] messageIds = [.. messages.Select(each => each.Id)];
        List<AttachmentRow> attachments = await context.Attachments.AsNoTracking()
            .Where(each => each.MessageId != null && messageIds.Contains(each.MessageId.Value) && each.Name != null)
            .OrderBy(each => each.CreatedAt).ThenBy(each => each.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        List<ConversationRow> all = [.. supports.Concat(chats).OrderBy(each => each.CreatedAt).ThenBy(each => each.Id)];
        return token => OpenAsync(person, all, messages, attachments, token);
    }

    /// <summary>Opens what <see cref="ReadAsync"/> read, after the export's transaction.</summary>
    private async Task<Result<IReadOnlyList<ConversationExport>>> OpenAsync(string person, List<ConversationRow> conversations, List<MessageRow> messages, List<AttachmentRow> attachments, CancellationToken cancellationToken)
    {
        TimeSpan bound = options.CurrentValue.Dispatch.Timeout;
        HashSet<string> rejected = new(StringComparer.Ordinal);
        Dictionary<Guid, List<AttachmentExport>> named = [];
        foreach (AttachmentRow row in attachments)
        {
            ContentOpening name = await AttachmentOpening.OpenAsync(protection, row.Owner, row.Name!, AttachmentFiles.NameContext(row.Id), bound, clock, cancellationToken).ConfigureAwait(false);
            if (name.Status == ContentOpeningStatus.Held)
            {
                return Held(name.Key);
            }

            if (name.Status == ContentOpeningStatus.Rejected)
            {
                rejected.Add(ConversationTables.Attachments);
            }

            if (name.Status == ContentOpeningStatus.Opened)
            {
                if (!named.TryGetValue(row.MessageId!.Value, out List<AttachmentExport>? list))
                {
                    named[row.MessageId.Value] = list = [];
                }

                list.Add(new AttachmentExport { Name = Encoding.UTF8.GetString(name.Plaintext.Span), MediaType = row.MediaType, Size = row.Size });
            }
        }

        List<ConversationExport> exported = [];
        foreach (ConversationRow conversation in conversations)
        {
            List<ConversationMessageExport> written = [];
            foreach (MessageRow message in messages.Where(each => each.ConversationId == conversation.Id))
            {
                bool erased = string.Equals(message.AuthorKind, Person, StringComparison.Ordinal) && message.Author is null && message.Text is null;
                bool unopened = false;
                string? text = null;
                if (message.Text is { } stored)
                {
                    ContentOpening opening = await AttachmentOpening.OpenAsync(protection, message.Owner, stored, ContentContext.Of(ContentContext.Messages, message.Id, ConversationTables.Text), bound, clock, cancellationToken).ConfigureAwait(false);
                    switch (opening.Status)
                    {
                        case ContentOpeningStatus.Held:
                            return Held(opening.Key);
                        case ContentOpeningStatus.Opened:
                            text = Encoding.UTF8.GetString(opening.Plaintext.Span);
                            break;
                        case ContentOpeningStatus.Erased:
                            erased = true;
                            break;
                        default:
                            unopened = true;
                            rejected.Add(ContentContext.Messages);
                            break;
                    }
                }

                written.Add(new ConversationMessageExport
                {
                    SentAt = message.SentAt,
                    Author = Author(message, person),
                    Text = text,
                    Erased = erased,
                    Rejected = unopened,
                    Attachments = named.TryGetValue(message.Id, out List<AttachmentExport>? files) ? files : [],
                });
            }

            (SurveyExport? survey, string? heldKey, bool surveyHeld) = await SurveyAsync(conversation, bound, rejected, cancellationToken).ConfigureAwait(false);
            if (surveyHeld)
            {
                return Held(heldKey);
            }

            bool support = string.Equals(conversation.Type, Support, StringComparison.Ordinal);
            exported.Add(new ConversationExport
            {
                Conversation = new ConversationId(conversation.Id),
                Type = ConversationSpellings.TypeOf(conversation.Type),
                Topic = support ? conversation.Topic : null,
                OpenedAt = conversation.CreatedAt,
                ClosedAt = support ? conversation.ClosedAt : null,
                Messages = written,
                Survey = survey,
            });
        }

        foreach (string table in rejected.Order(StringComparer.Ordinal))
        {
            await alerts.HoldsUntilAsync("content-rejected", table, new Dictionary<string, string>(StringComparer.Ordinal) { ["table"] = table }, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success<IReadOnlyList<ConversationExport>>(exported);
    }

    /// <summary>A support conversation's survey answer, its comment opened for the customer; none where none was given.</summary>
    private async Task<(SurveyExport? Survey, string? HeldKey, bool Held)> SurveyAsync(ConversationRow conversation, TimeSpan bound, HashSet<string> rejected, CancellationToken cancellationToken)
    {
        if (conversation.SurveyAt is null || conversation.SurveyResolved is not { } resolved || conversation.SurveyRating is not { } rating)
        {
            return (null, null, false);
        }

        string? comment = null;
        if (conversation.SurveyComment is { } stored)
        {
            ContentOpening opening = await AttachmentOpening.OpenAsync(protection, conversation.Customer, stored, ContentContext.Of(ContentContext.Conversations, conversation.Id, ConversationTables.SurveyComment), bound, clock, cancellationToken).ConfigureAwait(false);
            switch (opening.Status)
            {
                case ContentOpeningStatus.Held:
                    return (null, opening.Key, true);
                case ContentOpeningStatus.Opened:
                    comment = Encoding.UTF8.GetString(opening.Plaintext.Span);
                    break;
                case ContentOpeningStatus.Rejected:
                    rejected.Add(ContentContext.Conversations);
                    break;
                default:
                    break;
            }
        }

        return (new SurveyExport { Resolved = resolved, Rating = rating, Comment = comment }, null, false);
    }

    /// <summary>Who wrote <paramref name="message"/>, as the export names it: never another person's reference.</summary>
    private static string Author(MessageRow message, string person)
    {
        if (string.Equals(message.AuthorKind, Library, StringComparison.Ordinal))
        {
            return "library";
        }

        return string.Equals(message.AuthorKind, Person, StringComparison.Ordinal) && string.Equals(message.Author, person, StringComparison.Ordinal) ? "person" : "staff";
    }

    private static Result<IReadOnlyList<ConversationExport>> Held(string? key)
    {
        return Result.Fail<IReadOnlyList<ConversationExport>>(new Failure
        {
            Code = Codes.KeyUnavailable,
            Details = key is { } named
                ? new Dictionary<string, string>(StringComparer.Ordinal) { ["key"] = named }
                : new Dictionary<string, string>(StringComparer.Ordinal),
        });
    }
}