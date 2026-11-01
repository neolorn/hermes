using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Hermes.Core;

using Npgsql;

using Xunit;

namespace Hermes.Conversations.Tests;

/// <summary>The staff side's direct and group conversations through <see cref="IConversations"/> (<c>09</c> MSG-CONV-001, MSG-CONV-006).</summary>
[Trait("Category", "integration")]
public sealed class StaffChatTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly ConversationScope Desk = MembershipFake.Organization("desk");

    [Fact]
    public async Task MSG_CONV_006_AC1_ANonMembersEveryReadAndWriteAnswersNotFound()
    {
        await using ChatHost chat = await ChatHost.StartAsync(postgres);
        chat.Membership.Add(Desk, "amal");
        ConversationId group = (await chat.CallAsync(chats => chats.OpenGroupAsync("amal", Desk, default))).Value!.Conversation;
        MessageId message = (await chat.CallAsync(chats => chats.WriteAsync("amal", group, "Morning", MessageType.Text, null, null, null, default))).Value!.Message;

        List<Failure?> failures =
        [
            (await chat.CallAsync(chats => chats.OpenGroupAsync("omar", Desk, default))).Failure,
            (await chat.CallAsync(chats => chats.ReadAsync("omar", group, default))).Failure,
            (await chat.CallAsync(chats => chats.MessagesAsync("omar", group, null, null, null, default))).Failure,
            (await chat.CallAsync(chats => chats.WriteAsync("omar", group, "Hello", MessageType.Text, null, null, null, default))).Failure,
            (await chat.CallAsync(chats => chats.EditAsync("omar", group, message, "Hello", default))).Failure,
            (await chat.CallAsync(chats => chats.DeleteAsync("omar", group, message, default))).Failure,
            (await chat.CallAsync(chats => chats.MarkReadAsync("omar", group, message, default))).Failure,
            (await chat.CallAsync(chats => chats.MuteAsync("omar", group, true, default))).Failure,
            (await chat.CallAsync(chats => chats.AddParticipantsAsync("omar", group, ["sara"], default))).Failure,
            (await chat.CallAsync(chats => chats.LeaveAsync("omar", group, default))).Failure,
            (await chat.CallAsync(chats => chats.WriteAsync("omar", group, string.Empty, MessageType.Note, null, null, null, default))).Failure,
        ];

        Assert.All(failures, failure => Assert.Equal("messaging.conversation.notfound", failure?.Code));
        Assert.Empty((await chat.CallAsync(chats => chats.ListAsync("omar", null, null, default))).Value!.Conversations);
    }

    [Fact]
    public async Task MSG_CONV_006_AC1_AMemberTheSeamNoLongerAnswersForIsRefusedOnTheNextCall()
    {
        await using ChatHost chat = await ChatHost.StartAsync(postgres);
        chat.Membership.Add(Desk, "amal", "omar");
        ConversationId group = (await chat.CallAsync(chats => chats.OpenGroupAsync("omar", Desk, default))).Value!.Conversation;
        Assert.True((await chat.CallAsync(chats => chats.WriteAsync("omar", group, "Here", MessageType.Text, null, null, null, default))).IsSuccess);
        int asked = chat.Membership.Asked;

        chat.Membership.Remove(Desk, "omar");

        Assert.Equal("messaging.conversation.notfound", (await chat.CallAsync(chats => chats.MessagesAsync("omar", group, null, null, null, default))).Failure?.Code);
        Assert.Equal("messaging.conversation.notfound", (await chat.CallAsync(chats => chats.WriteAsync("omar", group, "Still here", MessageType.Text, null, null, null, default))).Failure?.Code);
        Assert.Equal(asked + 2, chat.Membership.Asked);
        Assert.Single((await chat.CallAsync(chats => chats.MessagesAsync("amal", group, null, null, null, default))).Value!.Messages);
    }

    [Fact]
    public async Task MSG_CONV_006_AC1_TheGroupConversationIsCreatedOnceForItsScope()
    {
        await using ChatHost chat = await ChatHost.StartAsync(postgres);
        chat.Membership.Add(Desk, "amal", "omar");

        Result<ConversationRecord> first = await chat.CallAsync(chats => chats.OpenGroupAsync("amal", Desk, default));
        Result<ConversationRecord> second = await chat.CallAsync(chats => chats.OpenGroupAsync("omar", Desk, default));

        Assert.Equal(first.Value!.Conversation, second.Value!.Conversation);
        Assert.Equal(ConversationType.Group, second.Value.Type);
        Assert.Equal(Desk, second.Value.Scope);
        Assert.Empty(second.Value.Participants);
        Assert.Equal(1L, await chat.ScalarAsync(database => new NpgsqlCommand("SELECT count(*) FROM messaging.conversations", database)));
        Assert.Single((await chat.CallAsync(chats => chats.ListAsync("omar", null, null, default))).Value!.Conversations);
    }

    [Fact]
    public async Task MSG_CONV_006_AC2_ANonParticipantAndOneWhoLeftAnswerNotFoundAtOnce()
    {
        await using ChatHost chat = await ChatHost.StartAsync(postgres);
        ConversationRecord started = (await chat.CallAsync(chats => chats.StartDirectAsync("amal", ["omar"], default))).Value!;
        MessageId message = (await chat.CallAsync(chats => chats.WriteAsync("omar", started.Conversation, "Hi", MessageType.Text, null, null, null, default))).Value!.Message;

        Assert.Equal("messaging.conversation.notfound", (await chat.CallAsync(chats => chats.ReadAsync("sara", started.Conversation, default))).Failure?.Code);
        Assert.Equal("messaging.conversation.notfound", (await chat.CallAsync(chats => chats.MessagesAsync("sara", started.Conversation, null, null, null, default))).Failure?.Code);

        Assert.True((await chat.CallAsync(chats => chats.LeaveAsync("omar", started.Conversation, default))).IsSuccess);

        Assert.Equal("messaging.conversation.notfound", (await chat.CallAsync(chats => chats.ReadAsync("omar", started.Conversation, default))).Failure?.Code);
        Assert.Equal("messaging.conversation.notfound", (await chat.CallAsync(chats => chats.WriteAsync("omar", started.Conversation, "Back", MessageType.Text, null, null, null, default))).Failure?.Code);
        Assert.Equal("messaging.conversation.notfound", (await chat.CallAsync(chats => chats.LeaveAsync("omar", started.Conversation, default))).Failure?.Code);
        Assert.Empty((await chat.CallAsync(chats => chats.ListAsync("omar", null, null, default))).Value!.Conversations);
        MessagePage kept = (await chat.CallAsync(chats => chats.MessagesAsync("amal", started.Conversation, null, null, null, default))).Value!;
        Assert.Equal(message, Assert.Single(kept.Messages).Message);
        Assert.Equal(["amal"], (await chat.CallAsync(chats => chats.ReadAsync("amal", started.Conversation, default))).Value!.Participants.Select(each => each.Person));

        Assert.True((await chat.CallAsync(chats => chats.AddParticipantsAsync("amal", started.Conversation, ["omar"], default))).IsSuccess);
        Assert.True((await chat.CallAsync(chats => chats.ReadAsync("omar", started.Conversation, default))).IsSuccess);
    }

    [Fact]
    public async Task MSG_CONV_006_AC2_ParticipantsAreAddedUpTo16AndThe17thAnswersFull()
    {
        await using ChatHost chat = await ChatHost.StartAsync(postgres);
        string[] others = [.. Enumerable.Range(1, 15).Select(each => $"p{each:D2}")];
        ConversationRecord started = (await chat.CallAsync(chats => chats.StartDirectAsync("amal", others, default))).Value!;
        Assert.Equal(16, started.Participants.Count);

        Result full = await chat.CallAsync(chats => chats.AddParticipantsAsync("p01", started.Conversation, ["p16"], default));

        Assert.Equal("messaging.conversation.full", full.Failure?.Code);
        Assert.Equal("messaging.conversation.notfound", (await chat.CallAsync(chats => chats.ReadAsync("p16", started.Conversation, default))).Failure?.Code);
        Assert.True((await chat.CallAsync(chats => chats.LeaveAsync("p15", started.Conversation, default))).IsSuccess);
        Assert.True((await chat.CallAsync(chats => chats.AddParticipantsAsync("p01", started.Conversation, ["p16", "amal"], default))).IsSuccess);
        Assert.Equal(16, (await chat.CallAsync(chats => chats.ReadAsync("p16", started.Conversation, default))).Value!.Participants.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    public async Task MSG_CONV_006_AC2_StartingWithNoneOrMoreThan15OthersIsRefused(int count)
    {
        await using ChatHost chat = await ChatHost.StartAsync(postgres);
        string[] others = [.. Enumerable.Range(1, count).Select(each => $"p{each:D2}")];

        Result<ConversationRecord> refused = await chat.CallAsync(chats => chats.StartDirectAsync("amal", others, default));

        Assert.Equal("messaging.conversation.invalid", refused.Failure?.Code);
        Assert.Equal("participants", refused.Failure!.Details["field"]);
    }

    [Theory]
    [InlineData("omar", "omar")]
    [InlineData("omar", "amal")]
    public async Task StartDirect_AReferenceNamedTwiceOrThePersonThemselves_IsRefused(string first, string second)
    {
        await using ChatHost chat = await ChatHost.StartAsync(postgres);

        Result<ConversationRecord> refused = await chat.CallAsync(chats => chats.StartDirectAsync("amal", [first, second], default));

        Assert.Equal("participants", refused.Failure?.Details["field"]);
    }

    [Fact]
    public async Task AddParticipantsAndLeave_AGroupConversation_AreRefusedNamingItsType()
    {
        await using ChatHost chat = await ChatHost.StartAsync(postgres);
        chat.Membership.Add(Desk, "amal");
        ConversationId group = (await chat.CallAsync(chats => chats.OpenGroupAsync("amal", Desk, default))).Value!.Conversation;

        Result added = await chat.CallAsync(chats => chats.AddParticipantsAsync("amal", group, ["omar"], default));
        Result left = await chat.CallAsync(chats => chats.LeaveAsync("amal", group, default));

        Assert.Equal(("messaging.conversation.invalid", "type"), (added.Failure?.Code, added.Failure?.Details["field"]));
        Assert.Equal(("messaging.conversation.invalid", "type"), (left.Failure?.Code, left.Failure?.Details["field"]));
    }

    [Fact]
    public async Task MSG_CONV_006_AC3_EditsAndDeletesWorkOnOnesOwnMessagesOnlyAndADeletedMessageReadsWithNoText()
    {
        await using ChatHost chat = await ChatHost.StartAsync(postgres);
        ConversationId direct = (await chat.CallAsync(chats => chats.StartDirectAsync("amal", ["omar"], default))).Value!.Conversation;
        MessageId mine = (await chat.CallAsync(chats => chats.WriteAsync("amal", direct, "First draft", MessageType.Text, null, ["omar"], null, default))).Value!.Message;
        MessageId theirs = (await chat.CallAsync(chats => chats.WriteAsync("omar", direct, "Their words", MessageType.Text, null, null, null, default))).Value!.Message;

        Assert.Equal("messaging.message.notauthor", (await chat.CallAsync(chats => chats.EditAsync("amal", direct, theirs, "Changed", default))).Failure?.Code);
        Assert.Equal("messaging.message.notauthor", (await chat.CallAsync(chats => chats.DeleteAsync("amal", direct, theirs, default))).Failure?.Code);
        Assert.True((await chat.CallAsync(chats => chats.EditAsync("amal", direct, mine, "Second draft", default))).IsSuccess);
        MessageRecord edited = await MessageAsync(chat, "omar", direct, mine);
        Assert.Equal("Second draft", edited.Text);
        Assert.NotNull(edited.EditedAt);
        Assert.Equal(["omar"], edited.Mentions);

        Assert.True((await chat.CallAsync(chats => chats.DeleteAsync("amal", direct, mine, default))).IsSuccess);
        Assert.True((await chat.CallAsync(chats => chats.DeleteAsync("amal", direct, mine, default))).IsSuccess);

        MessageRecord deleted = await MessageAsync(chat, "omar", direct, mine);
        Assert.Null(deleted.Text);
        Assert.NotNull(deleted.DeletedAt);
        Assert.False(deleted.Erased);
        Assert.Empty(deleted.Mentions);
        Assert.Equal("amal", deleted.Author);
        Assert.Equal("messaging.message.deleted", (await chat.CallAsync(chats => chats.EditAsync("amal", direct, mine, "Third draft", default))).Failure?.Code);
        Assert.Equal("Their words", (await MessageAsync(chat, "amal", direct, theirs)).Text);
        Assert.Equal("messaging.message.notfound", (await chat.CallAsync(chats => chats.EditAsync("amal", direct, new MessageId(Guid.CreateVersion7()), "Lost", default))).Failure?.Code);
    }

    [Fact]
    public async Task MSG_CONV_001_AC1_EachBoundIsRefusedAtItsEdge()
    {
        await using ChatHost chat = await ChatHost.StartAsync(postgres);
        string[] others = [.. Enumerable.Range(1, 15).Select(each => $"p{each:D2}")];
        ConversationId direct = (await chat.CallAsync(chats => chats.StartDirectAsync("amal", others, default))).Value!.Conversation;
        ConversationId other = (await chat.CallAsync(chats => chats.StartDirectAsync("amal", ["omar"], default))).Value!.Conversation;
        MessageId elsewhere = (await chat.CallAsync(chats => chats.WriteAsync("amal", other, "Elsewhere", MessageType.Text, null, null, null, default))).Value!.Message;
        chat.Membership.Add(Desk, ["amal", .. others]);
        ConversationId group = (await chat.CallAsync(chats => chats.OpenGroupAsync("amal", Desk, default))).Value!.Conversation;

        Assert.Equal("text", await RefusedAsync(chat, direct, string.Empty));
        Assert.Equal("text", await RefusedAsync(chat, direct, new string('a', 4_001)));
        Assert.Equal("mentions", await RefusedAsync(chat, direct, "Hi", mentions: [.. others, "amal", .. Enumerable.Range(1, 17).Select(each => $"q{each:D2}")]));
        Assert.Equal("mentions", await RefusedAsync(chat, direct, "Hi", mentions: ["sara"]));
        Assert.Equal("mentions", await RefusedAsync(chat, group, "Hi", mentions: ["sara"]));
        Assert.Equal("replyTo", await RefusedAsync(chat, direct, "Hi", replyTo: elsewhere));
        Assert.Equal("type", await RefusedAsync(chat, direct, "Hi", type: MessageType.Note));
        Assert.Equal("type", await RefusedAsync(chat, group, "Hi", type: MessageType.Note));

        Assert.True((await chat.CallAsync(chats => chats.WriteAsync("amal", direct, "a", MessageType.Text, null, null, null, default))).IsSuccess);
        Assert.True((await chat.CallAsync(chats => chats.WriteAsync("amal", direct, new string('a', 4_000), MessageType.Text, null, null, null, default))).IsSuccess);
        string[] thirtyTwo = [.. others, "amal", .. Enumerable.Range(1, 16).Select(each => $"q{each:D2}")];
        chat.Membership.Add(Desk, thirtyTwo);
        Result<MessageRecord> mentioned = await chat.CallAsync(chats => chats.WriteAsync("amal", group, "All of you", MessageType.Text, null, [.. thirtyTwo, "amal"], null, default));
        Assert.Equal(32, mentioned.Value!.Mentions.Count);
        Assert.Equal(thirtyTwo.Order(StringComparer.Ordinal), mentioned.Value.Mentions);
        Assert.Equal("text", (await chat.CallAsync(chats => chats.EditAsync("amal", group, mentioned.Value.Message, new string('a', 4_001), default))).Failure?.Details["field"]);
    }

    [Fact]
    public async Task MSG_CONV_001_AC1_AReplyNamesAMessageOfTheSameConversationDeletedOrNot()
    {
        await using ChatHost chat = await ChatHost.StartAsync(postgres);
        ConversationId direct = (await chat.CallAsync(chats => chats.StartDirectAsync("amal", ["omar"], default))).Value!.Conversation;
        MessageId first = (await chat.CallAsync(chats => chats.WriteAsync("omar", direct, "Question", MessageType.Text, null, null, null, default))).Value!.Message;
        await chat.CallAsync(chats => chats.DeleteAsync("omar", direct, first, default));

        Result<MessageRecord> reply = await chat.CallAsync(chats => chats.WriteAsync("amal", direct, "Answer", MessageType.Text, first, null, null, default));

        Assert.Equal(first, reply.Value!.ReplyTo);
        Assert.Equal(first, (await MessageAsync(chat, "omar", direct, reply.Value.Message)).ReplyTo);
    }

    [Fact]
    public async Task MSG_CONV_001_AC2_TheReadPositionMovesForwardOnlyAndTheUnreadCountsFollowIt()
    {
        await using ChatHost chat = await ChatHost.StartAsync(postgres);
        ConversationId direct = (await chat.CallAsync(chats => chats.StartDirectAsync("amal", ["omar"], default))).Value!.Conversation;
        List<MessageId> sent = [];
        foreach (string text in new[] { "One", "Two", "Three", "Four" })
        {
            sent.Add((await chat.CallAsync(chats => chats.WriteAsync("omar", direct, text, MessageType.Text, null, null, null, default))).Value!.Message);
        }

        await chat.CallAsync(chats => chats.WriteAsync("amal", direct, "Mine", MessageType.Text, null, null, null, default));
        Assert.Equal(4, await UnreadAsync(chat, "amal", direct));
        Assert.Equal(1, await UnreadAsync(chat, "omar", direct));

        Assert.True((await chat.CallAsync(chats => chats.MarkReadAsync("amal", direct, sent[2], default))).IsSuccess);
        Assert.Equal(1, await UnreadAsync(chat, "amal", direct));

        Assert.True((await chat.CallAsync(chats => chats.MarkReadAsync("amal", direct, sent[0], default))).IsSuccess);
        Assert.Equal(1, await UnreadAsync(chat, "amal", direct));

        await chat.CallAsync(chats => chats.DeleteAsync("omar", direct, sent[3], default));
        Assert.Equal(0, await UnreadAsync(chat, "amal", direct));
        Assert.Equal(0, (await chat.CallAsync(chats => chats.ListAsync("amal", null, null, default))).Value!.Conversations.Single().Unread);
        Assert.Equal("messaging.message.notfound", (await chat.CallAsync(chats => chats.MarkReadAsync("amal", direct, new MessageId(Guid.CreateVersion7()), default))).Failure?.Code);
    }

    [Fact]
    public async Task List_SeveralConversations_AnswersNewestActivityFirstAPageAtATime()
    {
        await using ChatHost chat = await ChatHost.StartAsync(postgres);
        List<ConversationId> started = [];
        foreach (string other in new[] { "b", "c", "d" })
        {
            started.Add((await chat.CallAsync(chats => chats.StartDirectAsync("amal", [other], default))).Value!.Conversation);
        }

        await chat.CallAsync(chats => chats.WriteAsync("amal", started[0], "Newest", MessageType.Text, null, null, null, default));
        await chat.CallAsync(chats => chats.MuteAsync("amal", started[1], true, default));

        ConversationPage first = (await chat.CallAsync(chats => chats.ListAsync("amal", null, 2, default))).Value!;
        ConversationPage second = (await chat.CallAsync(chats => chats.ListAsync("amal", first.Next, 2, default))).Value!;

        Assert.Equal([started[0], started[2]], first.Conversations.Select(each => each.Conversation));
        Assert.Equal([started[1]], second.Conversations.Select(each => each.Conversation));
        Assert.True(second.Conversations[0].Muted);
        Assert.Null(second.Next);
        Assert.Equal("cursor", (await chat.CallAsync(chats => chats.ListAsync("amal", "nope", null, default))).Failure?.Details["member"]);
    }

    private static async Task<string?> RefusedAsync(ChatHost chat, ConversationId conversation, string text, MessageType type = MessageType.Text, MessageId? replyTo = null, IReadOnlyList<string>? mentions = null)
    {
        Result<MessageRecord> written = await chat.CallAsync(chats => chats.WriteAsync("amal", conversation, text, type, replyTo, mentions, null, default));
        Assert.Equal("messaging.message.invalid", written.Failure?.Code);
        return written.Failure!.Details["field"];
    }

    private static async Task<MessageRecord> MessageAsync(ChatHost chat, string person, ConversationId conversation, MessageId message)
    {
        MessagePage page = (await chat.CallAsync(chats => chats.MessagesAsync(person, conversation, null, null, 100, default))).Value!;
        return page.Messages.Single(each => each.Message == message);
    }

    private static async Task<int> UnreadAsync(ChatHost chat, string person, ConversationId conversation)
    {
        return (await chat.CallAsync(chats => chats.ReadAsync(person, conversation, default))).Value!.Unread;
    }
}