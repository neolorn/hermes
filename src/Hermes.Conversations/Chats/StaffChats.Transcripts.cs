using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Content;
using Hermes.Conversations.Support;
using Hermes.Core;
using Hermes.Operations;
using Hermes.Sending;

using Microsoft.EntityFrameworkCore;

namespace Hermes.Conversations.Chats;

/// <summary>
/// The end of a support conversation for its visitor or customer (<c>09</c> MSG-CONV-004):
/// once it is closed, its transcript, read or sent by email at most three times, and its
/// survey, answered once within seven days of the close.
/// </summary>
internal sealed partial class StaffChats
{
    /// <summary>The most bytes of a transcript read through the public group, 1 MiB in UTF-8.</summary>
    internal const int MostTranscriptBytes = 1 << 20;

    /// <summary>The most times a conversation's transcript is sent by email.</summary>
    internal const int MostTranscriptSends = 3;

    /// <summary>The most characters of a survey's comment.</summary>
    internal const int MostCommentCharacters = 1_000;

    private static readonly TimeSpan SurveyWindow = TimeSpan.FromDays(7);

    /// <summary>The transcript of the caller's closed conversation, at most 1 MiB.</summary>
    internal async Task<Result<string>> TranscriptAsync(SupportCaller caller, Guid conversation, CancellationToken cancellationToken)
    {
        ready.ThrowIfNotReady();
        if (await ClosedOfAsync(caller, conversation, cancellationToken).ConfigureAwait(false) is not { } closed)
        {
            return Result.Fail<string>(NotFound());
        }

        if (closed.Failure is { } refused)
        {
            return Result.Fail<string>(refused);
        }

        return await RenderTranscriptAsync(closed.Row!, MostTranscriptBytes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends the transcript of the caller's closed conversation to <paramref name="email"/> as
    /// <c>Support:TranscriptKind</c>, at most three times, each send's idempotency key naming
    /// its number; answers whether a failure is the send's own refusal.
    /// </summary>
    internal async Task<(Failure? Failure, bool BySend)> TranscriptEmailAsync(SupportCaller caller, Guid conversation, string email, CancellationToken cancellationToken)
    {
        ready.ThrowIfNotReady();
        if (Destinations.Email(email) is not { } address)
        {
            return (Malformed("email"), false);
        }

        if (await ClosedOfAsync(caller, conversation, cancellationToken).ConfigureAwait(false) is not { } closed)
        {
            return (NotFound(), false);
        }

        if (closed.Failure is { } refused)
        {
            return (refused, false);
        }

        if (closed.Row!.TranscriptSends >= MostTranscriptSends)
        {
            return (Plain(SupportCodes.TranscriptLimit), false);
        }

        // The value is bounded at half an email, so the email that carries it fits (02 MSG-EMAIL-003).
        Result<string> rendered = await RenderTranscriptAsync(closed.Row, sendRenderer.EmailMaxBytes / 2, cancellationToken).ConfigureAwait(false);
        if (rendered.Failure is { } unrendered)
        {
            return (unrendered, false);
        }

        MessagingOptions current = options.CurrentValue;
        return await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockCallerAsync(context, current, caller, token).ConfigureAwait(false);
                ConversationRow? row = await LockedConversationAsync(context, closed.Row.Id, token).ConfigureAwait(false);
                if (row is null)
                {
                    return (NotFound(), false);
                }

                if (row.TranscriptSends >= MostTranscriptSends)
                {
                    return (Plain(SupportCodes.TranscriptLimit), false);
                }

                Result<SendReceipt> sent = await messages.SendAsync(
                    new Send
                    {
                        Kind = settings.Support.TranscriptKind!,
                        Recipient = row.Customer is { } customer ? new Recipient.Person(customer) : new Recipient.Anonymous(),
                        Email = address,
                        Values = new Dictionary<string, SendValue>(StringComparer.Ordinal)
                        {
                            ["topic"] = new SendValue.PlainText(row.Topic!),
                            ["closedAt"] = new SendValue.Instant(row.ClosedAt!.Value),
                            ["transcript"] = new SendValue.PlainText(rendered.Value!),
                        },
                        IdempotencyKey = "conversation:" + row.Id.ToString("D") + ":transcript:" + (row.TranscriptSends + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    },
                    token).ConfigureAwait(false);
                if (sent.Failure is { } refusedSend)
                {
                    return (refusedSend, true);
                }

                row.TranscriptSends++;
                await context.SaveChangesAsync(token).ConfigureAwait(false);
                return ((Failure?)null, false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Answers the survey of the caller's closed conversation, once, within seven days of its
    /// close; its comment protected as the conversation's messages are, and the staff member
    /// assigned when it closed the one rated.
    /// </summary>
    internal async Task<Result> SurveyAsync(SupportCaller caller, Guid conversation, bool? resolved, int? rating, string? comment, CancellationToken cancellationToken)
    {
        ready.ThrowIfNotReady();
        if (resolved is null)
        {
            return Result.Fail(Invalid(SupportCodes.SurveyInvalid, "resolved"));
        }

        if (rating is not (>= 1 and <= 5))
        {
            return Result.Fail(Invalid(SupportCodes.SurveyInvalid, "rating"));
        }

        if (comment is { Length: > MostCommentCharacters })
        {
            return Result.Fail(Invalid(SupportCodes.SurveyInvalid, "comment"));
        }

        if (await ClosedOfAsync(caller, conversation, cancellationToken).ConfigureAwait(false) is not { } closed)
        {
            return Result.Fail(NotFound());
        }

        if (closed.Failure is { } refused)
        {
            return Result.Fail(refused);
        }

        MessagingOptions current = options.CurrentValue;
        Protected? made = null;
        if (comment is { Length: > 0 })
        {
            made = await ProtectValueAsync(caller.Customer, comment, ContentContext.Of(ContentContext.Conversations, closed.Row!.Id, ConversationTables.SurveyComment), current, cancellationToken).ConfigureAwait(false);
            if (made.Failure is { } unavailable)
            {
                return Result.Fail(unavailable);
            }
        }

        Failure? failure = await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await LockCallerAsync(context, current, caller, token).ConfigureAwait(false);
                ConversationRow? row = await LockedConversationAsync(context, closed.Row!.Id, token).ConfigureAwait(false);
                if (row is null)
                {
                    return NotFound();
                }

                if (row.SurveyAt is not null)
                {
                    return Plain(SupportCodes.SurveyAnswered);
                }

                DateTimeOffset now = clock.GetUtcNow();
                if (now - row.ClosedAt!.Value > SurveyWindow)
                {
                    return Plain(SupportCodes.SurveyClosed);
                }

                row.SurveyAt = now;
                row.SurveyResolved = resolved;
                row.SurveyRating = rating;
                row.SurveyComment = made?.Stored;
                row.SurveyCommentKeyVersion = made?.Version;
                row.SurveyPerson = row.Assignee;
                await context.SaveChangesAsync(token).ConfigureAwait(false);
                return null;
            },
            cancellationToken).ConfigureAwait(false);
        return failure is null ? Result.Success() : Result.Fail(failure);
    }

    /// <summary>The caller's conversation, with <c>messaging.support.notclosed</c> where it is not closed; none where it is not theirs.</summary>
    private async Task<(ConversationRow? Row, Failure? Failure)?> ClosedOfAsync(SupportCaller caller, Guid conversation, CancellationToken cancellationToken)
    {
        if (await CallerConversationAsync(caller, conversation, cancellationToken).ConfigureAwait(false) is not { } row)
        {
            return null;
        }

        return string.Equals(row.Status, ClosedStatus, StringComparison.Ordinal) ? (row, null) : (row, Plain(SupportCodes.NotClosed));
    }

    /// <summary>
    /// Renders <paramref name="row"/>'s transcript (<c>09</c> MSG-CONV-004): every message but
    /// notes, oldest first, in its language and its topic's time zone, staff never named; where
    /// a render passes <paramref name="bytes"/> or the steps, the oldest messages are left out,
    /// as few as fit, and <c>omitted</c> counts them.
    /// </summary>
    private async Task<Result<string>> RenderTranscriptAsync(ConversationRow row, long bytes, CancellationToken cancellationToken)
    {
        MessagingOptions current = options.CurrentValue;
        List<MessageRow> rows;
        MessagingContext context = database.Context();
        await using (context.ConfigureAwait(false))
        {
            rows = await context.Messages.AsNoTracking()
                .Where(each => each.ConversationId == row.Id && each.Type != Note)
                .OrderBy(each => each.SentAt).ThenBy(each => each.Id)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        List<Dictionary<string, SendValue>> lines = [];
        foreach (MessageRow message in rows)
        {
            Opened opened = await OpenAsync(message, current, cancellationToken).ConfigureAwait(false);
            if (opened.Held is { } held)
            {
                return Result.Fail<string>(held);
            }

            lines.Add(new Dictionary<string, SendValue>(StringComparer.Ordinal)
            {
                ["author"] = new SendValue.PlainText(AuthorOf(row, message)),
                ["sentAt"] = new SendValue.Instant(message.SentAt),
                ["text"] = new SendValue.PlainText(message.DeletedAt is null ? opened.Text ?? string.Empty : string.Empty),
            });
        }

        string language = row.Language ?? current.DefaultLanguage ?? current.Languages[0];
        SupportTopic? topic = settings.Support.Topics.GetValueOrDefault(row.Topic!);
        TimeZoneInfo zone = topic?.Zone ?? TimeZoneInfo.Utc;

        // The fewest oldest messages left out that let it render, found by halving.
        int least = 0;
        int most = lines.Count;
        string? best = null;
        while (least <= most)
        {
            int omitted = least + ((most - least) / 2);
            string? text = await transcriptTemplates.RenderAsync(language, Values(row, language, lines, omitted), zone, bytes).ConfigureAwait(false);
            if (text is not null)
            {
                best = text;
                most = omitted - 1;
            }
            else
            {
                least = omitted + 1;
            }
        }

        return best is null ? Result.Fail<string>(Plain(Codes.RenderFailed)) : Result.Success(best);
    }

    private static Dictionary<string, SendValue> Values(ConversationRow row, string language, List<Dictionary<string, SendValue>> lines, int omitted)
    {
        return new Dictionary<string, SendValue>(StringComparer.Ordinal)
        {
            ["topic"] = new SendValue.PlainText(row.Topic!),
            ["openedAt"] = new SendValue.Instant(row.CreatedAt),
            ["closedAt"] = new SendValue.Instant(row.ClosedAt!.Value),
            ["language"] = new SendValue.PlainText(language),
            ["direction"] = new SendValue.PlainText(SendLanguage.Direction(language)),
            ["messages"] = new SendValue.List([.. lines.Skip(omitted)]),
            ["omitted"] = new SendValue.WholeNumber(omitted),
        };
    }

    /// <summary><c>visitor</c> for the visitor's or the customer's own messages, <c>library</c> for the library's, <c>staff</c> for any other, never a reference.</summary>
    private static string AuthorOf(ConversationRow conversation, MessageRow message)
    {
        if (string.Equals(message.AuthorKind, Library, StringComparison.Ordinal))
        {
            return "library";
        }

        return string.Equals(message.AuthorKind, Person, StringComparison.Ordinal) && (message.Author is null || !string.Equals(message.Author, conversation.Customer, StringComparison.Ordinal))
            ? "staff"
            : "visitor";
    }
}