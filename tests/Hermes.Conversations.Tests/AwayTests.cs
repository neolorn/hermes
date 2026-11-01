using System.Collections.Generic;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;

using Npgsql;

using Xunit;

namespace Hermes.Conversations.Tests;

/// <summary>Away notices: a person away from a conversation told through sending (<c>09</c> MSG-CONV-008).</summary>
[Trait("Category", "integration")]
public sealed class AwayTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Kind = "chat.away";

    private static readonly ConversationScope Desk = MembershipFake.Organization("desk");

    [Fact]
    public async Task MSG_CONV_008_AC5_AnAwayParticipantIsToldOnceUntilTheyReadAndAConnectedOneIsNotTold()
    {
        await using ChatHost chat = await StartAsync();
        ConversationId direct = (await chat.CallAsync(chats => chats.StartDirectAsync("amal", ["omar", "sara"], default))).Value!.Conversation;
        await using HubConnection sara = await chat.ConnectAsync("sara");

        MessageId first = (await chat.CallAsync(chats => chats.WriteAsync("amal", direct, "One", MessageType.Text, null, null, null, default))).Value!.Message;
        await chat.CallAsync(chats => chats.WriteAsync("amal", direct, "Two", MessageType.Text, null, null, null, default));
        Assert.Equal(1L, await chat.ScalarAsync(database => new NpgsqlCommand("SELECT count(*) FROM messaging.participants WHERE away_told IS NOT NULL", database)));
        Assert.Equal(1L, await ToldAsync(chat));

        Assert.True((await chat.CallAsync(chats => chats.MarkReadAsync("omar", direct, first, default))).IsSuccess);
        MessageId third = (await chat.CallAsync(chats => chats.WriteAsync("amal", direct, "Three", MessageType.Text, null, null, null, default))).Value!.Message;

        Assert.Equal(2L, await ToldAsync(chat));
        Assert.Equal(2L, await chat.ScalarAsync(database => new NpgsqlCommand("SELECT count(*) FROM messaging.idempotency", database)));
        Assert.Equal(third.Value, await chat.ScalarAsync(database => new NpgsqlCommand("SELECT away_told FROM messaging.participants WHERE person = 'omar'", database)));
        Assert.Equal(System.DBNull.Value, await chat.ScalarAsync(database => new NpgsqlCommand("SELECT away_told FROM messaging.participants WHERE person = 'sara'", database)));
    }

    [Fact]
    public async Task MSG_CONV_008_AC5_AMentionIsToldThroughAMuteAndAMutedParticipantNotMentionedIsNot()
    {
        await using ChatHost chat = await StartAsync();
        chat.Membership.Add(Desk, "amal", "omar", "lina");
        ConversationId group = (await chat.CallAsync(chats => chats.OpenGroupAsync("omar", Desk, default))).Value!.Conversation;
        await chat.CallAsync(chats => chats.MuteAsync("omar", group, true, default));
        ConversationId direct = (await chat.CallAsync(chats => chats.StartDirectAsync("amal", ["lina"], default))).Value!.Conversation;
        await chat.CallAsync(chats => chats.MuteAsync("lina", direct, true, default));

        await chat.CallAsync(chats => chats.WriteAsync("amal", group, "Omar, look", MessageType.Text, null, ["omar"], null, default));
        await chat.CallAsync(chats => chats.WriteAsync("amal", group, "Everyone", MessageType.Text, null, null, null, default));
        await chat.CallAsync(chats => chats.WriteAsync("amal", direct, "Muted", MessageType.Text, null, null, null, default));

        Assert.Equal(1L, await chat.ScalarAsync(database => new NpgsqlCommand("SELECT count(*) FROM messaging.participants WHERE away_told IS NOT NULL", database)));
        Assert.Equal(1L, await ToldAsync(chat));
        Assert.Equal(1L, await chat.ScalarAsync(database => new NpgsqlCommand("SELECT count(*) FROM messaging.participants WHERE person = 'omar' AND away_told IS NOT NULL", database)));
    }

    [Fact]
    public async Task MSG_CONV_008_AC6_AwayKindNamingAnUndeclaredKindStopsTheStartNamingTheKey()
    {
        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => ChatHost.StartAsync(postgres, configure: configuration => configuration["Hermes:Conversations:AwayKind"] = "chat.missing"));

        Assert.Equal("messaging.startup.configurationinvalid", failure.Code);
        Assert.Equal("Conversations:AwayKind", failure.Details["key"]);
    }

    [Fact]
    public async Task Write_WithNoAwayKind_TellsNoOne()
    {
        await using ChatHost chat = await ChatHost.StartAsync(postgres);
        ConversationId direct = (await chat.CallAsync(chats => chats.StartDirectAsync("amal", ["omar"], default))).Value!.Conversation;

        await chat.CallAsync(chats => chats.WriteAsync("amal", direct, "Hello", MessageType.Text, null, null, null, default));

        Assert.Equal(0L, await chat.ScalarAsync(database => new NpgsqlCommand("SELECT count(*) FROM messaging.sends", database)));
    }

    private Task<ChatHost> StartAsync()
    {
        return ChatHost.StartAsync(
            postgres,
            configure: configuration => configuration["Hermes:Conversations:AwayKind"] = Kind,
            declare: declare =>
            {
                declare.Category("chats");
                declare.Kind(Kind, "chats").InApp();
            });
    }

    private static async Task<object?> ToldAsync(ChatHost chat)
    {
        return await chat.ScalarAsync(database =>
        {
            NpgsqlCommand command = new("SELECT count(*) FROM messaging.sends WHERE kind = @kind", database);
            command.Parameters.AddWithValue("kind", Kind);
            return command;
        });
    }
}