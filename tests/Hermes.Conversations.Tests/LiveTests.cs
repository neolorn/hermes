using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Hermes.Conversations.Live;
using Hermes.Core;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Xunit;

namespace Hermes.Conversations.Tests;

/// <summary>The live connection: the staff hub, the fan-out between processes, typing and presence (<c>09</c> MSG-CONV-005, MSG-CONV-006, MSG-CONV-008).</summary>
[Trait("Category", "integration")]
public sealed partial class LiveTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly ConversationScope Desk = MembershipFake.Organization("desk");

    [Fact]
    public async Task MSG_CONV_008_AC1_AMessageWrittenThroughOneProcessReachesAClientConnectedToAnotherAfterItIsStored()
    {
        await using ChatHost a = await ChatHost.StartAsync(postgres);
        await using ChatHost b = await ChatHost.StartAsync(postgres, shared: a);
        ConversationId direct = (await a.CallAsync(chats => chats.StartDirectAsync("amal", ["omar"], default))).Value!.Conversation;
        await using HubConnection omar = await b.ConnectAsync("omar");
        HubInbox inbox = HubInbox.Of(omar);

        MessageId written = (await a.CallAsync(chats => chats.WriteAsync("amal", direct, "Across processes", MessageType.Text, null, null, default))).Value!.Message;

        JsonElement[] pushed = await inbox.NextAsync("message");
        Assert.Equal(direct.Value.ToString("D"), pushed[0].GetString());
        Assert.Equal(written.Value.ToString("D"), pushed[1].GetProperty("id").GetString());
        Assert.Equal(("Across processes", "amal", "person"), (pushed[1].GetProperty("text").GetString(), pushed[1].GetProperty("author").GetString(), pushed[1].GetProperty("authorKind").GetString()));

        Assert.True((await a.CallAsync(chats => chats.EditAsync("amal", direct, written, "Edited", default))).IsSuccess);
        JsonElement[] changed = await inbox.NextAsync("messageChanged");
        Assert.Equal("Edited", changed[1].GetProperty("text").GetString());
    }

    [Fact]
    public async Task MSG_CONV_008_AC2_AClientThatReconnectsReadsWhatItMissedWithAfter()
    {
        await using ChatHost chat = await ChatHost.StartAsync(postgres);
        ConversationId direct = (await chat.CallAsync(chats => chats.StartDirectAsync("amal", ["omar"], default))).Value!.Conversation;
        List<MessageId> sent = [];
        foreach (string text in new[] { "Seen", "Missed one", "Missed two" })
        {
            sent.Add((await chat.CallAsync(chats => chats.WriteAsync("amal", direct, text, MessageType.Text, null, null, default))).Value!.Message);
        }

        MessagePage missed = (await chat.CallAsync(chats => chats.MessagesAsync("omar", direct, null, sent[0], null, default))).Value!;
        using System.Net.Http.HttpResponseMessage routed = await chat.SendAsync(System.Net.Http.HttpMethod.Get, $"/conversations/{direct.Value:D}/messages?after={sent[0].Value:D}&size=1", "omar");
        using System.Net.Http.HttpResponseMessage both = await chat.SendAsync(System.Net.Http.HttpMethod.Get, $"/conversations/{direct.Value:D}/messages?after={sent[0].Value:D}&before={sent[2].Value:D}", "omar");

        Assert.Equal(["Missed one", "Missed two"], missed.Messages.Select(each => each.Text));
        using JsonDocument page = JsonDocument.Parse(await routed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("Missed one", page.RootElement.GetProperty("messages").EnumerateArray().Single().GetProperty("text").GetString());
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, both.StatusCode);
        Assert.Equal("after", (await chat.CallAsync(chats => chats.MessagesAsync("omar", direct, null, new MessageId(Guid.CreateVersion7()), null, default))).Failure?.Details["member"]);
    }

    [Fact]
    public async Task MSG_CONV_008_AC3_TheNotificationsPayloadHoldsNoPersonReferenceOrText()
    {
        await using ChatHost chat = await ChatHost.StartAsync(postgres);
        ConcurrentQueue<string> payloads = new();
        await using NpgsqlConnection listening = new(chat.Connection);
        listening.Notification += (_, notification) => payloads.Enqueue(notification.Payload);
        await listening.OpenAsync(TestContext.Current.CancellationToken);
        await using (NpgsqlCommand listen = new("LISTEN messaging_conversations", listening))
        {
            await listen.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        ConversationId direct = (await chat.CallAsync(chats => chats.StartDirectAsync("person-amal-31", ["person-omar-47"], default))).Value!.Conversation;
        await using HubConnection omar = await chat.ConnectAsync("person-omar-47");
        MessageId written = (await chat.CallAsync(chats => chats.WriteAsync("person-amal-31", direct, "a secret text", MessageType.Text, null, ["person-omar-47"], default))).Value!.Message;
        await chat.CallAsync(chats => chats.EditAsync("person-amal-31", direct, written, "another secret text", default));
        await chat.CallAsync(chats => chats.MarkReadAsync("person-omar-47", direct, written, default));
        await omar.InvokeAsync("Typing", direct.Value.ToString("D"), TestContext.Current.CancellationToken);
        await chat.CallAsync(chats => chats.DeleteAsync("person-amal-31", direct, written, default));
        while (payloads.Count < 7)
        {
            await listening.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        }

        Assert.Equal(["conversation", "message", "changed", "read", "typing", "changed"], payloads.Select(each => each.Split(':')[0]).Where(kind => kind != "presence").Take(6));
        Assert.Contains(payloads, payload => payload.StartsWith("presence:", StringComparison.Ordinal));
        Assert.All(payloads, payload => Assert.Matches(Payload(), payload));
        Assert.All(payloads, payload => Assert.DoesNotContain("person-", payload, StringComparison.Ordinal));
        Assert.All(payloads, payload => Assert.DoesNotContain("secret", payload, StringComparison.Ordinal));
    }

    [Fact]
    public async Task MSG_CONV_008_AC4_ANonMemberReceivesNothingOfAGroupConversationAndAMemberTheSeamDropsNothingFromTheNextEvent()
    {
        await using ChatHost chat = await ChatHost.StartAsync(postgres);
        chat.Membership.Add(Desk, "amal", "omar", "lina");
        ConversationId group = (await chat.CallAsync(chats => chats.OpenGroupAsync("omar", Desk, default))).Value!.Conversation;
        await using HubConnection amal = await chat.ConnectAsync("amal");
        await using HubConnection lina = await chat.ConnectAsync("lina");
        await using HubConnection sara = await chat.ConnectAsync("sara");
        HubInbox amalInbox = HubInbox.Of(amal);
        HubInbox linaInbox = HubInbox.Of(lina);
        HubInbox saraInbox = HubInbox.Of(sara);

        await chat.CallAsync(chats => chats.WriteAsync("omar", group, "First", MessageType.Text, null, null, default));
        await amalInbox.NextAsync("message");
        await linaInbox.NextAsync("message");

        chat.Membership.Remove(Desk, "amal");
        await chat.CallAsync(chats => chats.WriteAsync("omar", group, "Second", MessageType.Text, null, null, default));
        await linaInbox.NextAsync("message", arguments => arguments[1].GetProperty("text").GetString() == "Second");

        Assert.Empty(saraInbox.All);
        Assert.Single(amalInbox.All, each => each.Method == "message");
    }

    [Fact]
    public async Task MSG_CONV_005_AC1_TypingReachesTheOthersAndIsNeverStored()
    {
        await using ChatHost chat = await ChatHost.StartAsync(postgres);
        chat.Membership.Add(Desk, "amal", "omar");
        ConversationId group = (await chat.CallAsync(chats => chats.OpenGroupAsync("omar", Desk, default))).Value!.Conversation;
        await using HubConnection amal = await chat.ConnectAsync("amal");
        await using HubConnection omar = await chat.ConnectAsync("omar");
        HubInbox amalInbox = HubInbox.Of(amal);
        HubInbox omarInbox = HubInbox.Of(omar);
        object? before = await chat.ScalarAsync(database => new NpgsqlCommand("SELECT count(*) FROM messaging.messages", database));

        await amal.InvokeAsync("Typing", group.Value.ToString("D"), TestContext.Current.CancellationToken);

        JsonElement[] typing = await omarInbox.NextAsync("typing");
        Assert.Equal(group.Value.ToString("D"), typing[0].GetString());
        object? participant = await chat.ScalarAsync(database =>
        {
            NpgsqlCommand command = new("SELECT id::text FROM messaging.participants WHERE person = 'amal'", database);
            return command;
        });
        Assert.Equal(participant, typing[1].GetString());
        Assert.Equal(before, await chat.ScalarAsync(database => new NpgsqlCommand("SELECT count(*) FROM messaging.messages", database)));
        await chat.CallAsync(chats => chats.WriteAsync("omar", group, "Synced", MessageType.Text, null, null, default));
        await amalInbox.NextAsync("message");
        Assert.DoesNotContain(amalInbox.All, each => each.Method == "typing");
    }

    [Fact]
    public async Task MSG_CONV_006_AC4_PresenceTurnsOfflineWithin90SecondsOfAProcessStopping()
    {
        await using ChatHost chat = await ChatHost.StartAsync(postgres);
        ConversationRecord started = (await chat.CallAsync(chats => chats.StartDirectAsync("amal", ["omar"], default))).Value!;
        string omarParticipant = started.Participants.Single(each => each.Person == "omar").Participant.Value.ToString("D");
        await using HubConnection amal = await chat.ConnectAsync("amal");
        HubInbox inbox = HubInbox.Of(amal);

        // A process that stopped without closing its connections leaves its rows to age.
        await chat.ScalarAsync(database =>
        {
            NpgsqlCommand command = new("INSERT INTO messaging.connections (id, person, process, last_seen_at) VALUES (@id, 'omar', @process, now() - interval '60 seconds')", database);
            command.Parameters.AddWithValue("id", Guid.CreateVersion7());
            command.Parameters.AddWithValue("process", Guid.CreateVersion7());
            return command;
        });
        Assert.True(Online(await chat.CallAsync(chats => chats.ReadAsync("amal", started.Conversation, default)), "omar"));

        await chat.ScalarAsync(database => new NpgsqlCommand("UPDATE messaging.connections SET last_seen_at = now() - interval '91 seconds' WHERE person = 'omar'", database));
        Assert.False(Online(await chat.CallAsync(chats => chats.ReadAsync("amal", started.Conversation, default)), "omar"));
        await chat.Application.Services.GetRequiredService<LivePresence>().RefreshAsync(TestContext.Current.CancellationToken);

        JsonElement[] offline = await inbox.NextAsync("presence");
        Assert.Equal((omarParticipant, false), (offline[0].GetString(), offline[1].GetBoolean()));
        Assert.Equal(0L, await chat.ScalarAsync(database => new NpgsqlCommand("SELECT count(*) FROM messaging.connections WHERE person = 'omar'", database)));
        Assert.True(Online(await chat.CallAsync(chats => chats.ReadAsync("omar", started.Conversation, default)), "amal"));
    }

    [Fact]
    public async Task Connect_ThePersonsFirstConnection_TellsTheOtherParticipantsTheyAreOnlineAndTheLastThatTheyAreNot()
    {
        await using ChatHost chat = await ChatHost.StartAsync(postgres);
        ConversationRecord started = (await chat.CallAsync(chats => chats.StartDirectAsync("amal", ["omar"], default))).Value!;
        string omarParticipant = started.Participants.Single(each => each.Person == "omar").Participant.Value.ToString("D");
        await using HubConnection amal = await chat.ConnectAsync("amal");
        HubInbox inbox = HubInbox.Of(amal);

        HubConnection omar = await chat.ConnectAsync("omar");
        JsonElement[] online = await inbox.NextAsync("presence", arguments => arguments[1].GetBoolean());
        await omar.DisposeAsync();
        JsonElement[] offline = await inbox.NextAsync("presence", arguments => !arguments[1].GetBoolean());

        Assert.Equal(omarParticipant, online[0].GetString());
        Assert.Equal(omarParticipant, offline[0].GetString());
    }

    [Fact]
    public async Task Connect_WithNoPersonSignedIn_IsRefused()
    {
        await using ChatHost chat = await ChatHost.StartAsync(postgres);

        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await using HubConnection anonymous = await chat.ConnectAsync(string.Empty, recorded: false);
            await anonymous.InvokeAsync("Typing", Guid.CreateVersion7().ToString("D"), TestContext.Current.CancellationToken);
        });
        Assert.Equal(0L, await chat.ScalarAsync(database => new NpgsqlCommand("SELECT count(*) FROM messaging.connections", database)));
    }

    private static bool Online(Result<ConversationRecord> read, string person)
    {
        return read.Value!.Participants.Single(each => each.Person == person).Online;
    }

    [GeneratedRegex(@"^(message|changed|read|typing|presence|place|assigned|closed|conversation):[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}:[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$")]
    private static partial Regex Payload();
}