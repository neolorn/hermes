using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.AspNetCore.SignalR.Client;

using Xunit;

namespace Hermes.Conversations.Tests;

/// <summary>Support's live connection (<c>09</c> MSG-CONV-001, MSG-CONV-002, MSG-CONV-008; <c>05</c> MSG-API-010): the public hub, and the staff hub's pushes of support.</summary>
[Trait("Category", "integration")]
public sealed class SupportLiveTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task MSG_CONV_002_AC2_TheVisitorsPlaceIsPushedWhenTheQueueMoves()
    {
        await using ChatHost chat = await SupportTests.StartSupportAsync(postgres);
        Browser first = await SupportTests.AcknowledgedAsync(chat, "192.0.2.1");
        Browser second = await SupportTests.AcknowledgedAsync(chat, "192.0.2.2");
        Guid older = await OfflineAsync(first);
        await OfflineAsync(second);
        await using HubConnection secondHub = await chat.ConnectPublicAsync(second);
        HubInbox told = HubInbox.Of(secondHub);

        Assert.True((await chat.CallAsync(chats => chats.CloseAsync("amal", new ConversationId(older), default))).IsSuccess);

        Assert.Equal(1, (await told.NextAsync("place"))[0].GetInt32());
    }

    [Fact]
    public async Task MSG_CONV_001_AC4_AVisitorNeverReceivesANote()
    {
        await using ChatHost chat = await SupportTests.StartSupportAsync(postgres);
        await using HubConnection amal = await SupportTests.AvailableAsync(chat, "amal");
        Browser visitor = await SupportTests.AcknowledgedAsync(chat, "192.0.2.1");
        Guid conversation = await visitor.StartAsync();
        await using HubConnection hub = await chat.ConnectPublicAsync(visitor);
        HubInbox told = HubInbox.Of(hub);
        HubInbox staff = HubInbox.Of(amal);

        await chat.CallAsync(chats => chats.WriteAsync("amal", new ConversationId(conversation), "Only for staff", MessageType.Note, null, null, null, default));
        await chat.CallAsync(chats => chats.WriteAsync("amal", new ConversationId(conversation), "Hello there", MessageType.Text, null, null, null, default));

        await staff.NextAsync("message", arguments => arguments[1].GetProperty("text").GetString() == "Hello there");
        await told.NextAsync("message", arguments => arguments[1].GetProperty("text").GetString() == "Hello there");
        Assert.Single(told.All, push => push.Method == "message");
        Assert.Equal(2, staff.All.Count(push => push.Method == "message"));
    }

    [Fact]
    public async Task Push_AnAssignment_TellsTheVisitorAssignedAndTheAssigneeTheConversation()
    {
        await using ChatHost chat = await SupportTests.StartSupportAsync(postgres);
        Browser visitor = await SupportTests.AcknowledgedAsync(chat, "192.0.2.1");
        using (HttpResponseMessage form = await visitor.SendAsync(HttpMethod.Post, "/support/offline", """{ "topic": "billing", "email": "v@example.com", "text": "Hi" }"""))
        {
            Assert.True(form.IsSuccessStatusCode);
        }

        await using HubConnection hub = await chat.ConnectPublicAsync(visitor);
        HubInbox told = HubInbox.Of(hub);
        await using HubConnection amal = await chat.ConnectAsync("amal");
        HubInbox staff = HubInbox.Of(amal);

        Assert.True((await chat.CallAsync(chats => chats.SetAvailableAsync("amal", true, default))).IsSuccess);

        string conversation = (await told.NextAsync("assigned"))[0].GetString()!;
        JsonElement added = (await staff.NextAsync("conversation"))[0];
        Assert.Equal((conversation, "support", "billing"), (added.GetProperty("id").GetString(), added.GetProperty("type").GetString(), added.GetProperty("topic").GetString()));

        Assert.True((await chat.CallAsync(chats => chats.CloseAsync("amal", new ConversationId(Guid.Parse(conversation)), default))).IsSuccess);
        Assert.Equal(conversation, (await told.NextAsync("closed"))[0].GetString());
    }

    [Fact]
    public async Task Push_TypingAndMessages_ReachTheAssigneeFromTheVisitorAndTheVisitorFromTheAssignee()
    {
        await using ChatHost chat = await SupportTests.StartSupportAsync(postgres);
        await using HubConnection amal = await SupportTests.AvailableAsync(chat, "amal");
        HubInbox staff = HubInbox.Of(amal);
        Browser visitor = await SupportTests.AcknowledgedAsync(chat, "192.0.2.1");
        Guid conversation = await visitor.StartAsync();
        await using HubConnection hub = await chat.ConnectPublicAsync(visitor);
        HubInbox told = HubInbox.Of(hub);

        await hub.InvokeAsync("Typing", conversation.ToString("D"), TestContext.Current.CancellationToken);
        await staff.NextAsync("typing", arguments => arguments[0].GetString() == conversation.ToString("D"));
        using (HttpResponseMessage written = await visitor.SendAsync(HttpMethod.Post, $"/support/conversations/{conversation:D}/messages", """{ "text": "Are you there?" }"""))
        {
            Assert.True(written.IsSuccessStatusCode);
        }

        await staff.NextAsync("message", arguments => arguments[1].GetProperty("authorKind").GetString() == "visitor");
        await amal.InvokeAsync("Typing", conversation.ToString("D"), TestContext.Current.CancellationToken);
        await told.NextAsync("typing");
    }

    [Fact]
    public async Task Connect_WithNoCookieAndNoPerson_IsRefused()
    {
        await using ChatHost chat = await SupportTests.StartSupportAsync(postgres);
        HubConnection hub = await chat.ConnectPublicAsync(new Browser(chat));
        await using (hub)
        {
            for (int wait = 0; wait < 250 && hub.State != HubConnectionState.Disconnected; wait++)
            {
                await Task.Delay(20, TestContext.Current.CancellationToken);
            }

            Assert.Equal(HubConnectionState.Disconnected, hub.State);
        }
    }

    [Fact]
    public async Task Push_ADirectConversationStarted_TellsItsParticipantsWithTheConversation()
    {
        await using ChatHost chat = await ChatHost.StartAsync(postgres);
        await using HubConnection omar = await chat.ConnectAsync("omar");
        HubInbox told = HubInbox.Of(omar);

        ConversationId direct = (await chat.CallAsync(chats => chats.StartDirectAsync("amal", ["omar"], default))).Value!.Conversation;

        JsonElement added = (await told.NextAsync("conversation"))[0];
        Assert.Equal((direct.Value.ToString("D"), "direct", 2), (added.GetProperty("id").GetString(), added.GetProperty("type").GetString(), added.GetProperty("participants").GetArrayLength()));
    }

    [Fact]
    public async Task MSG_LIB_004_AC1_ThePublicConversationsGroupMappedAloneWorks()
    {
        await using ChatHost chat = await ChatHost.StartAsync(postgres, configure: SupportTests.Topics, declare: declare =>
        {
            declare.Category("support").Essential();
            declare.Kind(SupportTests.ReplyKind, "support").Email();
            declare.Kind(SupportTests.TranscriptKind, "support").Email();
        }, staff: false);

        using HttpResponseMessage topics = await new Browser(chat).SendAsync(HttpMethod.Get, "/support/topics");
        using HttpResponseMessage staff = await chat.SendAsync(HttpMethod.Get, "/conversations", "amal");

        Assert.Equal(System.Net.HttpStatusCode.OK, topics.StatusCode);
        Assert.Equal(["billing", "late", "night", "other"], (await Browser.JsonAsync(topics)).GetProperty("topics").EnumerateArray().Select(topic => topic.GetProperty("name").GetString()!));
        Assert.Equal(System.Net.HttpStatusCode.NotFound, staff.StatusCode);
    }

    private static async Task<Guid> OfflineAsync(Browser visitor)
    {
        using HttpResponseMessage form = await visitor.SendAsync(HttpMethod.Post, "/support/offline", """{ "topic": "billing", "email": "visitor@example.com", "text": "Call me" }""");
        Assert.True(form.IsSuccessStatusCode);
        return Guid.Parse((await Browser.JsonAsync(form)).GetProperty("id").GetString()!);
    }
}