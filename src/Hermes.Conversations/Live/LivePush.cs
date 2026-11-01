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
/// Pushes what a notification tells to this process's staff-hub connections whose person may
/// read the conversation now (<c>09</c> MSG-CONV-008): a direct conversation's participants
/// who have not left; a group conversation's scope's members, from one
/// <see cref="IConversationMembership.MembersAsync"/> call per event, intersected with the
/// persons connected here. Each push reads the store, so it follows what was written.
/// </summary>
internal sealed class LivePush(
    IServiceScopeFactory scopes,
    MessagingDatabase database,
    LiveConnections connections,
    IHubContext<StaffHub> hub,
    TimeProvider clock)
{
    /// <summary>Pushes what <paramref name="payload"/> tells.</summary>
    internal async Task PushAsync(string payload, CancellationToken cancellationToken)
    {
        if (LiveNotices.Parse(payload) is not { } told || connections.Persons().Count == 0)
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

                bool direct = string.Equals(conversation.Type, ConversationSpellings.Of(ConversationType.Direct), StringComparison.Ordinal);
                IReadOnlySet<string> readers = await ReadersAsync(context, scope.ServiceProvider, conversation, direct, cancellationToken).ConfigureAwait(false);
                string id = conversation.Id.ToString("D");
                switch (told.Kind)
                {
                    case LiveNotices.Message:
                    case LiveNotices.Changed:
                        if (await scope.ServiceProvider.GetRequiredService<StaffChats>().LiveMessageAsync(told.Id, conversation.Id, cancellationToken).ConfigureAwait(false) is { } message)
                        {
                            JsonElement body = JsonSerializer.SerializeToElement(StaffEndpoints.Of(message), ConversationsJson.Default.MessageBody);
                            await hub.Clients.Clients(connections.Of(readers)).SendAsync(told.Kind == LiveNotices.Message ? StaffHub.MessageMethod : StaffHub.MessageChangedMethod, id, body, cancellationToken).ConfigureAwait(false);
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
                    default:
                        if (direct && await ParticipantAsync(context, told.Id, cancellationToken).ConfigureAwait(false) is { Person: { } presentPerson } present)
                        {
                            bool online = await LivePresence.OnlineAsync(context, presentPerson, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
                            await hub.Clients.Clients(connections.Of(readers, except: presentPerson)).SendAsync(StaffHub.PresenceMethod, present.Id.ToString("D"), online, cancellationToken).ConfigureAwait(false);
                        }

                        break;
                }
            }
        }
    }

    /// <summary>The persons connected here who may read <paramref name="conversation"/> now.</summary>
    private async Task<IReadOnlySet<string>> ReadersAsync(MessagingContext context, IServiceProvider services, ConversationRow conversation, bool direct, CancellationToken cancellationToken)
    {
        IReadOnlySet<string> here = connections.Persons();
        if (direct)
        {
            List<string> participants = await context.Participants.AsNoTracking()
                .Where(each => each.ConversationId == conversation.Id && each.LeftAt == null)
                .Where(each => each.Person != null).Select(each => each.Person!)
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