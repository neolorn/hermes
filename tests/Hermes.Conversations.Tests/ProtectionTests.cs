using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Xunit;

namespace Hermes.Conversations.Tests;

/// <summary>A staff chat message's protection and its author's erasure (<c>09</c> MSG-CONV-009).</summary>
[Trait("Category", "integration")]
public sealed class ProtectionTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task MSG_CONV_009_AC1_AMessagesProtectedTextCopiedToAnotherRowDoesNotOpen()
    {
        await using ChatHost chat = await ChatHost.StartAsync(postgres);
        ConversationId direct = (await chat.CallAsync(chats => chats.StartDirectAsync("amal", ["omar"], default))).Value!.Conversation;
        MessageId source = (await chat.CallAsync(chats => chats.WriteAsync("amal", direct, "the source's words", MessageType.Text, null, null, null, default))).Value!.Message;
        MessageId target = (await chat.CallAsync(chats => chats.WriteAsync("amal", direct, "the target's words", MessageType.Text, null, null, null, default))).Value!.Message;
        Assert.Equal(0L, await chat.ScalarAsync(database => new NpgsqlCommand("SELECT count(*) FROM messaging.messages WHERE position(convert_to('words', 'UTF8') in text) > 0", database)));

        await chat.ScalarAsync(database =>
        {
            NpgsqlCommand command = new("UPDATE messaging.messages SET text = (SELECT text FROM messaging.messages WHERE id = @source) WHERE id = @target", database);
            command.Parameters.AddWithValue("source", source.Value);
            command.Parameters.AddWithValue("target", target.Value);
            return command;
        });
        MessagePage page = (await chat.CallAsync(chats => chats.MessagesAsync("omar", direct, null, null, null, default))).Value!;

        Assert.Equal("the source's words", page.Messages.Single(each => each.Message == source).Text);
        MessageRecord copied = page.Messages.Single(each => each.Message == target);
        Assert.Null(copied.Text);
        Assert.False(copied.Erased);
        Assert.Equal(1L, await chat.ScalarAsync(database => new NpgsqlCommand("SELECT count(*) FROM messaging.alerts WHERE condition = 'content-rejected' AND scope = 'messages' AND holding", database)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MSG_CONV_009_AC2_ErasingAnAuthorClearsTheirMessagesAndRowsAndLeavesOtherMessagesUntouched(bool hostProtector)
    {
        await using ChatHost chat = await ChatHost.StartAsync(postgres, services =>
        {
            if (hostProtector)
            {
                services.AddSingleton<IContentProtector>(new ContentProtectorFake("vault"));
            }
        });
        ConversationScope desk = MembershipFake.Organization("desk");
        chat.Membership.Add(desk, "amal", "omar");
        ConversationId direct = (await chat.CallAsync(chats => chats.StartDirectAsync("amal", ["omar", "sara"], default))).Value!.Conversation;
        ConversationId group = (await chat.CallAsync(chats => chats.OpenGroupAsync("omar", desk, default))).Value!.Conversation;
        MessageId erased = (await chat.CallAsync(chats => chats.WriteAsync("amal", direct, "Amal's words", MessageType.Text, null, ["omar"], null, default))).Value!.Message;
        MessageId inGroup = (await chat.CallAsync(chats => chats.WriteAsync("amal", group, "Amal in the group", MessageType.Text, null, null, null, default))).Value!.Message;
        MessageId kept = (await chat.CallAsync(chats => chats.WriteAsync("omar", direct, "Omar's words", MessageType.Text, erased, ["amal", "sara"], null, default))).Value!.Message;

        await chat.ScalarAsync(database => new NpgsqlCommand("INSERT INTO messaging.connections (id, person, process, last_seen_at) VALUES (gen_random_uuid(), 'amal', gen_random_uuid(), now())", database));
        Result erasure = await chat.SubjectsAsync(subjects => subjects.EraseAsync("amal", default));

        Assert.True(erasure.IsSuccess);
        MessagePage page = (await chat.CallAsync(chats => chats.MessagesAsync("omar", direct, null, null, null, default))).Value!;
        MessageRecord cleared = page.Messages.Single(each => each.Message == erased);
        Assert.Equal((AuthorKind.Person, (string?)null, (string?)null, true), (cleared.AuthorKind, cleared.Author, cleared.Text, cleared.Erased));
        Assert.Equal(["omar"], cleared.Mentions);
        MessageRecord other = page.Messages.Single(each => each.Message == kept);
        Assert.Equal(("omar", "Omar's words", false, (MessageId?)erased), (other.Author, other.Text, other.Erased, other.ReplyTo));
        Assert.Equal(["sara"], other.Mentions);
        Assert.True(Assert.Single((await chat.CallAsync(chats => chats.MessagesAsync("omar", group, null, null, null, default))).Value!.Messages).Erased);
        Assert.Equal(["omar", "sara"], (await chat.CallAsync(chats => chats.ReadAsync("omar", direct, default))).Value!.Participants.Select(each => each.Person));
        Assert.Equal(0L, await chat.ScalarAsync(database => new NpgsqlCommand("SELECT (SELECT count(*) FROM messaging.participants WHERE person = 'amal') + (SELECT count(*) FROM messaging.mentions WHERE person = 'amal') + (SELECT count(*) FROM messaging.connections WHERE person = 'amal') + (SELECT count(*) FROM messaging.messages WHERE author = 'amal' OR (author IS NULL AND text IS NOT NULL))", database)));
        Assert.Equal(inGroup, Assert.Single((await chat.CallAsync(chats => chats.MessagesAsync("omar", group, null, null, null, default))).Value!.Messages).Message);
        Assert.Equal("messaging.conversation.notfound", (await chat.CallAsync(chats => chats.ReadAsync("amal", direct, default))).Failure?.Code);
    }
}