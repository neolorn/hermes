using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using Hermes.Conversations.Support;
using Hermes.Core;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Xunit;

namespace Hermes.Conversations.Tests;

/// <summary>
/// How long conversations are kept, ending a scope, and a person's export of their
/// conversations (<c>09</c> MSG-CONV-010; <c>01</c> MSG-SEND-020; M-038). A run of the
/// retention job's conversations step, and a pass of <c>attachment-scans</c>, run at the
/// instant a test names.
/// </summary>
[Trait("Category", "integration")]
public sealed class RetentionTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly ConversationScope Shop = new() { Kind = ScopeKind.Group, Reference = "team-shop" };

    private readonly StoreFake store = new();

    private readonly ScannerFake scanner = new();

    [Fact]
    public async Task MSG_CONV_010_AC3_AVisitorsConversationIsGoneRetentionVisitorAfterCloseWithEveryRowAndItsAttachmentsBytes()
    {
        await using ChatHost chat = await StartAsync(configuration => configuration["Hermes:Conversations:Retention:Visitor"] = "30.00:00:00");
        await using HubConnection amal = await SupportTests.AvailableAsync(chat, "amal");
        Browser visitor = await SupportTests.AcknowledgedAsync(chat, "192.0.2.1");
        Guid gone = await visitor.StartAsync();
        using HttpResponseMessage uploaded = await visitor.SendAsync(HttpMethod.Post, $"/support/conversations/{gone:D}/attachments", () => AttachmentTests.File(AttachmentTests.Png(2_000), "photo.png", "image/png"));
        string attachment = (await Browser.JsonAsync(uploaded)).GetProperty("id").GetString()!;
        using HttpResponseMessage written = await visitor.SendAsync(HttpMethod.Post, $"/support/conversations/{gone:D}/messages", $$"""{ "text": "A photo", "attachments": ["{{attachment}}"] }""");
        Assert.Equal(HttpStatusCode.Created, written.StatusCode);
        Assert.True((await chat.CallAsync(chats => chats.CloseAsync("amal", new ConversationId(gone), default))).IsSuccess);

        // Another visitor's conversation, still open, keeps theirs; a customer's closed one stays while the person does.
        Browser other = await SupportTests.AcknowledgedAsync(chat, "192.0.2.2");
        Guid open = await other.StartAsync();
        Browser carla = new(chat, person: "carla");
        Guid customers = await carla.StartAsync();
        Assert.True((await chat.CallAsync(chats => chats.CloseAsync("amal", new ConversationId(customers), default))).IsSuccess);
        DateTimeOffset closed = DateTimeOffset.UtcNow;

        await RetainAsync(chat, closed.AddDays(29));
        Assert.Equal(3L, await CountAsync(chat, database => new NpgsqlCommand("SELECT count(*) FROM messaging.conversations", database)));

        await RetainAsync(chat, closed.AddDays(31));
        Assert.Equal(0L, await CountAsync(chat, database => With(new NpgsqlCommand("SELECT count(*) FROM messaging.conversations WHERE id = @id", database), gone)));
        Assert.Equal(0L, await CountAsync(chat, database => With(new NpgsqlCommand("SELECT count(*) FROM messaging.messages WHERE conversation_id = @id", database), gone)));
        Assert.Equal(0L, await CountAsync(chat, database => With(new NpgsqlCommand("SELECT count(*) FROM messaging.participants WHERE conversation_id = @id", database), gone)));
        Assert.Equal(2L, await CountAsync(chat, database => new NpgsqlCommand("SELECT count(*) FROM messaging.conversations", database)));
        Assert.Equal(1L, await CountAsync(chat, database => new NpgsqlCommand("SELECT count(*) FROM messaging.support_acknowledgements", database)));
        Assert.Contains(open, await ConversationsAsync(chat));
        Assert.Contains(customers, await ConversationsAsync(chat));

        Assert.Single(store.Objects);
        await AttachmentTests.PassAsync(chat, DateTimeOffset.UtcNow.AddSeconds(1));
        Assert.Empty(store.Objects);
        Assert.Equal(0L, await CountAsync(chat, database => new NpgsqlCommand("SELECT count(*) FROM messaging.attachments", database)));
    }

    [Fact]
    public async Task MSG_CONV_010_AC4_WithRetentionStaffUnsetNothingOfAStaffChatGoes()
    {
        await using ChatHost chat = await StartAsync();
        Guid conversation = (await chat.CallAsync(chats => chats.StartDirectAsync("amal", ["omar"], default))).Value!.Conversation.Value;
        Assert.True((await chat.CallAsync(chats => chats.WriteAsync("amal", new ConversationId(conversation), "From long ago", MessageType.Text, null, null, null, default))).IsSuccess);
        await AgeAsync(chat, TimeSpan.FromDays(3650));

        await RetainAsync(chat, DateTimeOffset.UtcNow);

        Assert.Equal(1L, await CountAsync(chat, database => new NpgsqlCommand("SELECT count(*) FROM messaging.messages", database)));
    }

    [Fact]
    public async Task MSG_CONV_010_AC4_WithRetentionStaffSetOlderMessagesGoWithTheirAttachmentsAndTheRestReadOn()
    {
        await using ChatHost chat = await StartAsync(configuration => configuration["Hermes:Conversations:Retention:Staff"] = "30.00:00:00");
        Guid conversation = (await chat.CallAsync(chats => chats.StartDirectAsync("amal", ["omar"], default))).Value!.Conversation.Value;
        Guid attachment = await UploadAsync(chat, conversation);
        MessageId old = (await chat.CallAsync(chats => chats.WriteAsync("amal", new ConversationId(conversation), "From long ago", MessageType.Text, null, null, [new AttachmentId(attachment)], default))).Value!.Message;
        Assert.True((await chat.CallAsync(chats => chats.MarkReadAsync("omar", new ConversationId(conversation), old, default))).IsSuccess);
        await AgeAsync(chat, TimeSpan.FromDays(40));
        Assert.True((await chat.CallAsync(chats => chats.WriteAsync("omar", new ConversationId(conversation), "A reply today", MessageType.Text, old, null, null, default))).IsSuccess);

        await RetainAsync(chat, DateTimeOffset.UtcNow);

        MessageRecord kept = Assert.Single((await chat.CallAsync(chats => chats.MessagesAsync("amal", new ConversationId(conversation), null, null, null, default))).Value!.Messages);
        Assert.Equal(("A reply today", null), (kept.Text, kept.ReplyTo));
        Assert.Equal(0, (await chat.CallAsync(chats => chats.ReadAsync("omar", new ConversationId(conversation), default))).Value!.Unread);
        await AttachmentTests.PassAsync(chat, DateTimeOffset.UtcNow.AddSeconds(1));
        Assert.Empty(store.Objects);
    }

    [Fact]
    public async Task MSG_CONV_010_AC5_EndScopeAsyncRemovesTheScopesConversationEntirely()
    {
        await using ChatHost chat = await StartAsync();
        chat.Membership.Add(Shop, "amal", "omar");
        Guid ended = (await chat.CallAsync(chats => chats.OpenGroupAsync("amal", Shop, default))).Value!.Conversation.Value;
        Guid attachment = await UploadAsync(chat, ended);
        Assert.True((await chat.CallAsync(chats => chats.WriteAsync("amal", new ConversationId(ended), "Our last word", MessageType.Text, null, ["omar"], [new AttachmentId(attachment)], default))).IsSuccess);
        Guid kept = (await chat.CallAsync(chats => chats.OpenGroupAsync("amal", SupportTests.Team, default))).Value!.Conversation.Value;

        Assert.True((await chat.CallAsync(chats => chats.EndScopeAsync(Shop, default))).IsSuccess);
        Assert.True((await chat.CallAsync(chats => chats.EndScopeAsync(new ConversationScope { Kind = ScopeKind.Organization, Reference = "none" }, default))).IsSuccess);

        Assert.Equal([kept], await ConversationsAsync(chat));
        Assert.Equal(0L, await CountAsync(chat, database => new NpgsqlCommand("SELECT (SELECT count(*) FROM messaging.messages) + (SELECT count(*) FROM messaging.mentions)", database)));
        Assert.Equal(0L, await CountAsync(chat, database => With(new NpgsqlCommand("SELECT count(*) FROM messaging.participants WHERE conversation_id = @id", database), ended)));
        await AttachmentTests.PassAsync(chat, DateTimeOffset.UtcNow.AddSeconds(1));
        Assert.Empty(store.Objects);
        Assert.Equal(0L, await CountAsync(chat, database => new NpgsqlCommand("SELECT count(*) FROM messaging.attachments", database)));
    }

    [Fact]
    public async Task MSG_SEND_020_AC6_APersonsExportHoldsTheirConversationsAndNobodyElsesReferences()
    {
        await using ChatHost chat = await StartAsync();
        await using HubConnection amal = await SupportTests.AvailableAsync(chat, "amal");

        // A support conversation of carla's, as the customer: every message but the note, and her survey.
        Browser carla = new(chat, person: "carla");
        Guid support = await carla.StartAsync(text: "Where is my order?");
        using HttpResponseMessage uploaded = await carla.SendAsync(HttpMethod.Post, $"/support/conversations/{support:D}/attachments", () => AttachmentTests.File(AttachmentTests.Png(1_500), "receipt.png", "image/png"));
        string receipt = (await Browser.JsonAsync(uploaded)).GetProperty("id").GetString()!;
        using HttpResponseMessage named = await carla.SendAsync(HttpMethod.Post, $"/support/conversations/{support:D}/messages", $$"""{ "text": "My receipt", "attachments": ["{{receipt}}"] }""");
        Assert.Equal(HttpStatusCode.Created, named.StatusCode);
        Assert.True((await chat.CallAsync(chats => chats.WriteAsync("amal", new ConversationId(support), "Staff only", MessageType.Note, null, null, null, default))).IsSuccess);
        Assert.True((await chat.CallAsync(chats => chats.WriteAsync("amal", new ConversationId(support), "It ships today", MessageType.Text, null, null, null, default))).IsSuccess);
        Assert.True((await chat.CallAsync(chats => chats.CloseAsync("amal", new ConversationId(support), default))).IsSuccess);
        using HttpResponseMessage survey = await carla.SendAsync(HttpMethod.Post, $"/support/conversations/{support:D}/survey", """{ "resolved": true, "rating": 4, "comment": "quick and kind" }""");
        Assert.Equal(HttpStatusCode.NoContent, survey.StatusCode);

        // A staff chat carla wrote in: her own messages only.
        Guid chatted = (await chat.CallAsync(chats => chats.StartDirectAsync("carla", ["omar"], default))).Value!.Conversation.Value;
        Assert.True((await chat.CallAsync(chats => chats.WriteAsync("carla", new ConversationId(chatted), "Mine", MessageType.Text, null, null, null, default))).IsSuccess);
        Assert.True((await chat.CallAsync(chats => chats.WriteAsync("omar", new ConversationId(chatted), "Omar's own", MessageType.Text, null, ["carla"], null, default))).IsSuccess);

        // A chat she never wrote in is not hers to export.
        Guid silent = (await chat.CallAsync(chats => chats.StartDirectAsync("omar", ["carla"], default))).Value!.Conversation.Value;
        Assert.True((await chat.CallAsync(chats => chats.WriteAsync("omar", new ConversationId(silent), "Only omar", MessageType.Text, null, null, null, default))).IsSuccess);

        Result<SubjectExport> export = await chat.SubjectsAsync(subjects => subjects.ExportAsync("carla", default));

        IReadOnlyList<ConversationExport> conversations = export.Value!.Conversations!;
        Assert.Equal([support, chatted], conversations.Select(each => each.Conversation.Value));
        ConversationExport first = conversations[0];
        Assert.Equal((ConversationType.Support, "billing", true), (first.Type, first.Topic, first.ClosedAt is not null));
        string[] lines = [.. first.Messages.Select(message => message.Author + ":" + message.Text)];
        Assert.Contains("person:Where is my order?", lines);
        Assert.Contains("person:My receipt", lines);
        Assert.Contains("staff:It ships today", lines);
        Assert.Contains("library:", lines);
        Assert.DoesNotContain(lines, line => line.Contains("Staff only", StringComparison.Ordinal));
        AttachmentExport file = Assert.Single(first.Messages.SelectMany(message => message.Attachments));
        Assert.Equal(("receipt.png", "image/png", 1_500L), (file.Name, file.MediaType, file.Size));
        Assert.Equal((true, 4, "quick and kind"), (first.Survey!.Resolved, first.Survey.Rating, first.Survey.Comment));

        ConversationExport second = conversations[1];
        Assert.Equal((ConversationType.Direct, null, null), (second.Type, second.Topic, second.ClosedAt));
        Assert.Equal(["person:Mine"], second.Messages.Select(message => message.Author + ":" + message.Text));

        string json = JsonSerializer.Serialize(conversations);
        Assert.DoesNotContain("amal", json, StringComparison.Ordinal);
        Assert.DoesNotContain("omar", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MSG_SEND_020_AC6_AnExportWhoseMessageCannotOpenYetAnswersKeyUnavailable()
    {
        await using ChatHost chat = await StartAsync();
        Guid conversation = (await chat.CallAsync(chats => chats.StartDirectAsync("carla", ["omar"], default))).Value!.Conversation.Value;
        Assert.True((await chat.CallAsync(chats => chats.WriteAsync("carla", new ConversationId(conversation), "Mine", MessageType.Text, null, null, null, default))).IsSuccess);

        // A format byte kept for a later release holds the value.
        await chat.ScalarAsync(database => new NpgsqlCommand("UPDATE messaging.messages SET text = set_byte(text, 0, 2)", database));
        Result<SubjectExport> export = await chat.SubjectsAsync(subjects => subjects.ExportAsync("carla", default));

        Assert.Equal("messaging.key.unavailable", export.Failure?.Code);
    }

    private static async Task<Guid> UploadAsync(ChatHost chat, Guid conversation)
    {
        using System.IO.MemoryStream content = new(AttachmentTests.Png(1_000));
        Result<AttachmentRecord> uploaded = await chat.CallAsync(chats => chats.UploadAttachmentAsync("amal", new ConversationId(conversation), "photo.png", "image/png", content, default));
        Assert.True(uploaded.IsSuccess, uploaded.Failure?.Code);
        return uploaded.Value!.Attachment.Value;
    }

    /// <summary>Runs the retention job's conversations step at <paramref name="now"/>.</summary>
    private static Task<bool> RetainAsync(ChatHost chat, DateTimeOffset now)
    {
        ConversationRetention step = ActivatorUtilities.CreateInstance<ConversationRetention>(chat.Application.Services);
        return step.RunAsync(now, () => Task.FromResult(true), TestContext.Current.CancellationToken);
    }

    /// <summary>Moves every message <paramref name="age"/> into the past.</summary>
    private static Task<object?> AgeAsync(ChatHost chat, TimeSpan age)
    {
        return chat.ScalarAsync(database =>
        {
            NpgsqlCommand command = new("UPDATE messaging.messages SET sent_at = sent_at - @age", database);
            command.Parameters.AddWithValue("age", age);
            return command;
        });
    }

    private static async Task<Guid[]> ConversationsAsync(ChatHost chat)
    {
        NpgsqlConnection database = new(chat.Connection);
        await using (database)
        {
            await database.OpenAsync(TestContext.Current.CancellationToken);
            await using NpgsqlCommand command = new("SELECT id FROM messaging.conversations ORDER BY id", database);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
            List<Guid> ids = [];
            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            {
                ids.Add(reader.GetGuid(0));
            }

            return [.. ids];
        }
    }

    private static async Task<long> CountAsync(ChatHost chat, Func<NpgsqlConnection, NpgsqlCommand> make)
    {
        return Convert.ToInt64(await chat.ScalarAsync(make), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static NpgsqlCommand With(NpgsqlCommand command, Guid id)
    {
        command.Parameters.AddWithValue("id", id);
        return command;
    }

    /// <summary>A support host with attachments on in both groups, the store and scanner this test holds.</summary>
    private Task<ChatHost> StartAsync(Action<Dictionary<string, string?>>? configure = null)
    {
        return SupportTests.StartSupportAsync(
            postgres,
            configuration =>
            {
                configuration["Hermes:Conversations:Attachments:Enabled"] = "true";
                configuration["Hermes:Conversations:Attachments:Public"] = "true";
                configure?.Invoke(configuration);
            },
            services: services =>
            {
                services.AddSingleton<IAttachmentStore>(store);
                services.AddSingleton<IAttachmentScanner>(scanner);
            });
    }
}