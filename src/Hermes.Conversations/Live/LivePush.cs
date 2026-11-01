using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Conversations.Chats;
using Hermes.Conversations.Endpoints;
using Hermes.Core;
using Hermes.Operations;

using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hermes.Conversations.Live;

/// <summary>
/// Pushes what a notification tells to this process's hub connections whose caller may read
/// the conversation now (<c>09</c> MSG-CONV-008): on the staff hub, a direct conversation's
/// participants who have not left; a group conversation's scope's members, from one
/// <see cref="IConversationMembership.MembersAsync"/> call per event, intersected with the
/// persons connected here; a support conversation's assignee alone (MSG-CONV-002). On the
/// public hub, a support conversation's visitor or customer, never a <c>note</c>. Each push
/// reads the store, so it follows what was written.
/// </summary>
internal sealed class LivePush(
    IServiceScopeFactory scopes,
    MessagingDatabase database,
    LiveConnections connections,
    PublicConnections visitors,
    IHubContext<StaffHub> hub,
    IHubContext<PublicHub> publicHub,
    TimeProvider clock)
{
    private static readonly string Direct = ConversationSpellings.Of(ConversationType.Direct);

    private static readonly string Support = ConversationSpellings.Of(ConversationType.Support);

    private static readonly string Note = ConversationSpellings.Of(MessageType.Note);

    /// <summary>Pushes what <paramref name="payload"/> tells.</summary>
    internal async Task PushAsync(string payload, CancellationToken cancellationToken)
    {
        if (LiveNotices.Parse(payload) is not { } told || (connections.Persons().Count == 0 && !visitors.Any))
        {
            return;
        }

        AsyncServiceScope scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            MessagingContext context = database.Context();
            await using (context.ConfigureAwait(false))
            {
                ConversationRow? conversation = await context.Conversations.AsNoTracking().FirstOrDefaultAsync(each => each.Id == told.Conversation, cancellationToken).ConfigureAwait(false);
                if (conversation is null)
                {
                    return;
                }

                if (string.Equals(conversation.Type, Support, StringComparison.Ordinal))
                {
                    await PushSupportAsync(context, scope.ServiceProvider, conversation, told.Kind, told.Id, cancellationToken).ConfigureAwait(false);
                    return;
                }

                bool direct = string.Equals(conversation.Type, Direct, StringComparison.Ordinal);
                IReadOnlySet<string> readers = await ReadersAsync(context, scope.ServiceProvider, conversation, direct, cancellationToken).ConfigureAwait(false);
                string id = conversation.Id.ToString("D");
                switch (told.Kind)
                {
                    case LiveNotices.Message:
                    case LiveNotices.Changed:
                        if (await MessageAsync(scope.ServiceProvider, told.Id, conversation.Id, cancellationToken).ConfigureAwait(false) is { } message)
                        {
                            await hub.Clients.Clients(connections.Of(readers)).SendAsync(MethodOf(told.Kind), id, message.Body, cancellationToken).ConfigureAwait(false);
                        }

                        break;
                    case LiveNotices.Moved:
                        if (await ParticipantAsync(context, told.Id, cancellationToken).ConfigureAwait(false) is { ReadPosition: { } position } reader)
                        {
                            await hub.Clients.Clients(connections.Of(readers, except: reader.Person)).SendAsync(StaffHub.ReadMethod, id, reader.Id.ToString("D"), position.ToString("D"), cancellationToken).ConfigureAwait(false);
                        }

                        break;
                    case LiveNotices.Typing:
                        if (await ParticipantAsync(context, told.Id, cancellationToken).ConfigureAwait(false) is { } typist)
                        {
                            await hub.Clients.Clients(connections.Of(readers, except: typist.Person)).SendAsync(StaffHub.TypingMethod, id, typist.Id.ToString("D"), cancellationToken).ConfigureAwait(false);
                        }

                        break;
                    case LiveNotices.Presence:
                        if (direct && await ParticipantAsync(context, told.Id, cancellationToken).ConfigureAwait(false) is { Person: { } presentPerson } present)
                        {
                            bool online = await LivePresence.OnlineAsync(context, presentPerson, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
                            await hub.Clients.Clients(connections.Of(readers, except: presentPerson)).SendAsync(StaffHub.PresenceMethod, present.Id.ToString("D"), online, cancellationToken).ConfigureAwait(false);
                        }

                        break;
                    case LiveNotices.Conversation:
                        if (direct)
                        {
                            await ConversationAsync(context, conversation, readers, cancellationToken).ConfigureAwait(false);
                        }

                        break;
                    default:
                        break;
                }
            }
        }
    }

    /// <summary>Pushes an event of a support conversation: on the staff hub to its assignee, on the public hub to its visitor or customer.</summary>
    private async Task PushSupportAsync(MessagingContext context, IServiceProvider services, ConversationRow conversation, string kind, Guid told, CancellationToken cancellationToken)
    {
        string id = conversation.Id.ToString("D");
        HashSet<string> staff = conversation.Assignee is { } assignee && connections.Persons().Contains(assignee) ? new(StringComparer.Ordinal) { assignee } : new(StringComparer.Ordinal);
        ParticipantRow? owner = await context.Participants.AsNoTracking()
            .FirstOrDefaultAsync(each => each.ConversationId == conversation.Id && (each.Visitor != null || each.Person == conversation.Customer), cancellationToken).ConfigureAwait(false);
        IReadOnlyList<string> callers = owner is null ? [] : OwnerConnections(conversation, owner);
        switch (kind)
        {
            case LiveNotices.Message:
            case LiveNotices.Changed:
                if (await MessageAsync(services, told, conversation.Id, cancellationToken).ConfigureAwait(false) is { } message)
                {
                    await hub.Clients.Clients(connections.Of(staff)).SendAsync(MethodOf(kind), id, message.Body, cancellationToken).ConfigureAwait(false);
                    if (!string.Equals(message.Type, Note, StringComparison.Ordinal))
                    {
                        await publicHub.Clients.Clients(callers).SendAsync(MethodOf(kind), id, message.Body, cancellationToken).ConfigureAwait(false);
                    }
                }

                break;
            case LiveNotices.Moved:
                if (await ParticipantAsync(context, told, cancellationToken).ConfigureAwait(false) is { ReadPosition: { } position } reader)
                {
                    await hub.Clients.Clients(connections.Of(staff, except: reader.Person)).SendAsync(StaffHub.ReadMethod, id, reader.Id.ToString("D"), position.ToString("D"), cancellationToken).ConfigureAwait(false);
                }

                break;
            case LiveNotices.Typing:
                if (await ParticipantAsync(context, told, cancellationToken).ConfigureAwait(false) is { } typist)
                {
                    bool fromOwner = typist.Id == owner?.Id;
                    await hub.Clients.Clients(connections.Of(staff, except: typist.Person)).SendAsync(StaffHub.TypingMethod, id, typist.Id.ToString("D"), cancellationToken).ConfigureAwait(false);
                    if (!fromOwner)
                    {
                        await publicHub.Clients.Clients(callers).SendAsync(StaffHub.TypingMethod, id, typist.Id.ToString("D"), cancellationToken).ConfigureAwait(false);
                    }
                }

                break;
            case LiveNotices.Place:
                if (await StaffChats.PlaceAsync(context, conversation, cancellationToken).ConfigureAwait(false) is { } place)
                {
                    await publicHub.Clients.Clients(callers).SendAsync(PublicHub.PlaceMethod, place, cancellationToken).ConfigureAwait(false);
                }

                break;
            case LiveNotices.Assigned:
                await publicHub.Clients.Clients(callers).SendAsync(PublicHub.AssignedMethod, id, cancellationToken).ConfigureAwait(false);
                break;
            case LiveNotices.Closed:
                await publicHub.Clients.Clients(callers).SendAsync(PublicHub.ClosedMethod, id, cancellationToken).ConfigureAwait(false);
                break;
            case LiveNotices.Conversation:
                await ConversationAsync(context, conversation, staff, cancellationToken).ConfigureAwait(false);
                break;
            default:
                break;
        }
    }

    /// <summary>Pushes <c>conversation</c> to each of <paramref name="readers"/> connected here, the conversation as a read answers it for them.</summary>
    private async Task ConversationAsync(MessagingContext context, ConversationRow conversation, IReadOnlySet<string> readers, CancellationToken cancellationToken)
    {
        foreach (string reader in readers)
        {
            ConversationRecord record = (await StaffChats.RecordsAsync(context, reader, clock.GetUtcNow(), [conversation], cancellationToken).ConfigureAwait(false))[0];
            JsonElement body = JsonSerializer.SerializeToElement(StaffEndpoints.Of(record), ConversationsJson.Default.ConversationBody);
            await hub.Clients.Clients(connections.Of(new HashSet<string>(StringComparer.Ordinal) { reader })).SendAsync(StaffHub.ConversationMethod, body, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>The public connections here of a support conversation's visitor or customer.</summary>
    private IReadOnlyList<string> OwnerConnections(ConversationRow conversation, ParticipantRow owner)
    {
        if (conversation.Customer is { } customer)
        {
            return visitors.Of(PublicConnections.CustomerKey(customer));
        }

        return owner.Visitor is { } hash ? visitors.Of(PublicConnections.VisitorKey(hash)) : [];
    }

    /// <summary>The message as a read answers it, with its type, or none where it is gone or its text cannot open yet.</summary>
    private static async Task<(JsonElement Body, string Type)?> MessageAsync(IServiceProvider services, Guid message, Guid conversation, CancellationToken cancellationToken)
    {
        if (await services.GetRequiredService<StaffChats>().LiveMessageAsync(message, conversation, cancellationToken).ConfigureAwait(false) is not { } record)
        {
            return null;
        }

        return (JsonSerializer.SerializeToElement(StaffEndpoints.Of(record), ConversationsJson.Default.MessageBody), ConversationSpellings.Of(record.Type));
    }

    private static string MethodOf(string kind)
    {
        return kind == LiveNotices.Message ? StaffHub.MessageMethod : StaffHub.MessageChangedMethod;
    }

    /// <summary>The persons connected here who may read <paramref name="conversation"/> now.</summary>
    private async Task<IReadOnlySet<string>> ReadersAsync(MessagingContext context, IServiceProvider services, ConversationRow conversation, bool direct, CancellationToken cancellationToken)
    {
        IReadOnlySet<string> here = connections.Persons();
        if (direct)
        {
            List<string> participants = await context.Participants.AsNoTracking()
                .Where(each => each.ConversationId == conversation.Id && each.LeftAt == null && each.Person != null)
                .Select(each => each.Person!)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            return participants.Where(here.Contains).ToHashSet(StringComparer.Ordinal);
        }

        if (conversation.ScopeKind is not { } kind || conversation.ScopeReference is not { } reference)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        HashSet<string> members = new(StringComparer.Ordinal);
        ConversationScope scope = new() { Kind = ConversationSpellings.ScopeKindOf(kind), Reference = reference };
        await foreach (string member in services.GetRequiredService<IConversationMembership>().MembersAsync(scope, cancellationToken).ConfigureAwait(false))
        {
            if (here.Contains(member))
            {
                members.Add(member);
            }
        }

        return members;
    }

    private static Task<ParticipantRow?> ParticipantAsync(MessagingContext context, Guid participant, CancellationToken cancellationToken)
    {
        return context.Participants.AsNoTracking().FirstOrDefaultAsync(each => each.Id == participant, cancellationToken);
    }
}