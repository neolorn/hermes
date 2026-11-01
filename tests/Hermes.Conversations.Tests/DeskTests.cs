using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.AspNetCore.SignalR.Client;

using Npgsql;

using Xunit;

namespace Hermes.Conversations.Tests;

/// <summary>
/// The support desk's tools (<c>09</c> MSG-CONV-005; <c>05</c> MSG-API-009): saved replies,
/// tags and snoozing, on the topics of <see cref="SupportTests"/>, with the tags
/// <c>vip</c> and <c>refund</c> declared.
/// </summary>
[Trait("Category", "integration")]
public sealed class DeskTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task MSG_CONV_005_AC2_SavedRepliesKeepTheirBoundsAndOwners()
    {
        await using ChatHost chat = await StartAsync();

        using HttpResponseMessage added = await chat.SendAsync(HttpMethod.Post, "/support/replies", "amal", """{ "name": "Greeting", "text": "Hello, how can I help?" }""");
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        JsonElement reply = await Browser.JsonAsync(added);
        string id = reply.GetProperty("id").GetString()!;
        Assert.Equal(("Greeting", JsonValueKind.Null), (reply.GetProperty("name").GetString(), reply.GetProperty("topic").ValueKind));

        await AssertRefusedAsync(chat, HttpMethod.Post, "/support/replies", "amal", """{ "name": "Greeting", "text": "Again" }""", HttpStatusCode.Conflict, "messaging.reply.nameexists");
        await AssertRefusedAsync(chat, HttpMethod.Post, "/support/replies", "amal", $$"""{ "name": "{{new string('n', 65)}}", "text": "Hi" }""", HttpStatusCode.BadRequest, "messaging.reply.invalid", "name");
        await AssertRefusedAsync(chat, HttpMethod.Post, "/support/replies", "amal", """{ "name": "Empty", "text": "" }""", HttpStatusCode.BadRequest, "messaging.reply.invalid", "text");
        await AssertRefusedAsync(chat, HttpMethod.Post, "/support/replies", "amal", $$"""{ "name": "Long", "text": "{{new string('t', 4001)}}" }""", HttpStatusCode.BadRequest, "messaging.reply.invalid", "text");

        // Another person's own reply is unknown to them, on every route.
        Assert.Empty(await NamesAsync(chat, "omar", "?mine"));
        await AssertRefusedAsync(chat, HttpMethod.Put, "/support/replies/" + id, "omar", """{ "name": "Mine", "text": "Taken" }""", HttpStatusCode.NotFound, "messaging.reply.notfound");
        await AssertRefusedAsync(chat, HttpMethod.Delete, "/support/replies/" + id, "omar", null, HttpStatusCode.NotFound, "messaging.reply.notfound");

        using HttpResponseMessage changed = await chat.SendAsync(HttpMethod.Put, "/support/replies/" + id, "amal", """{ "name": "Welcome", "text": "Welcome back" }""");
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        Assert.Equal(["Welcome"], await NamesAsync(chat, "amal", "?mine"));

        using HttpResponseMessage removed = await chat.SendAsync(HttpMethod.Delete, "/support/replies/" + id, "amal");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Empty(await NamesAsync(chat, "amal", "?mine"));
    }

    [Fact]
    public async Task MSG_CONV_005_AC2_ATopicsRepliesAreSharedByItsStaffAndUnknownToOthers()
    {
        await using ChatHost chat = await StartAsync();

        using HttpResponseMessage added = await chat.SendAsync(HttpMethod.Post, "/support/replies", "amal", """{ "name": "Refunds", "text": "Refunds take five days.", "topic": "billing" }""");
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        string id = (await Browser.JsonAsync(added)).GetProperty("id").GetString()!;

        Assert.Equal(["Refunds"], await NamesAsync(chat, "omar", "?topic=billing"));
        Assert.Empty(await NamesAsync(chat, "amal", "?mine"));
        using HttpResponseMessage changed = await chat.SendAsync(HttpMethod.Put, "/support/replies/" + id, "omar", """{ "name": "Refunds", "text": "Refunds take a week." }""");
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);

        await AssertRefusedAsync(chat, HttpMethod.Get, "/support/replies?topic=billing", "stranger", null, HttpStatusCode.NotFound, "messaging.reply.notfound");
        await AssertRefusedAsync(chat, HttpMethod.Post, "/support/replies", "stranger", """{ "name": "Mine", "text": "Hi", "topic": "billing" }""", HttpStatusCode.NotFound, "messaging.reply.notfound");
        await AssertRefusedAsync(chat, HttpMethod.Delete, "/support/replies/" + id, "stranger", null, HttpStatusCode.NotFound, "messaging.reply.notfound");
        await AssertRefusedAsync(chat, HttpMethod.Get, "/support/replies?topic=unknown", "amal", null, HttpStatusCode.BadRequest, "messaging.request.malformed", "topic");
        await AssertRefusedAsync(chat, HttpMethod.Get, "/support/replies", "amal", null, HttpStatusCode.BadRequest, "messaging.request.malformed", "topic");
        await AssertRefusedAsync(chat, HttpMethod.Get, "/support/replies?mine&topic=billing", "amal", null, HttpStatusCode.BadRequest, "messaging.request.malformed", "topic");
    }

    [Fact]
    public async Task MSG_CONV_005_AC2_APersonHoldsAHundredRepliesAndATopicTwoHundred()
    {
        await using ChatHost chat = await StartAsync();
        for (int reply = 0; reply < 100; reply++)
        {
            Assert.True((await chat.CallAsync(chats => chats.AddReplyAsync("amal", Name(reply), "Text", null, default))).IsSuccess);
        }

        Result<SavedReplyRecord> own = await chat.CallAsync(chats => chats.AddReplyAsync("amal", "One more", "Text", null, default));
        Assert.Equal("messaging.reply.full", own.Failure!.Code);

        for (int reply = 0; reply < 200; reply++)
        {
            Assert.True((await chat.CallAsync(chats => chats.AddReplyAsync("omar", Name(reply), "Text", "billing", default))).IsSuccess);
        }

        Result<SavedReplyRecord> shared = await chat.CallAsync(chats => chats.AddReplyAsync("amal", "One more", "Text", "billing", default));
        Assert.Equal("messaging.reply.full", shared.Failure!.Code);
        Assert.True((await chat.CallAsync(chats => chats.AddReplyAsync("omar", "One more", "Text", null, default))).IsSuccess);
    }

    [Fact]
    public async Task AddReply_ManyAtOnceUnderOneName_AddsOneAndAnswersTheRestNameExists()
    {
        await using ChatHost chat = await StartAsync();

        Result<SavedReplyRecord>[] added = await Task.WhenAll(Enumerable.Range(0, 8).Select(each => chat.CallAsync(chats => chats.AddReplyAsync(each % 2 == 0 ? "amal" : "omar", "Same", "Text", "billing", default))));

        Assert.Single(added, result => result.IsSuccess);
        Assert.All(added.Where(result => !result.IsSuccess), result => Assert.Equal("messaging.reply.nameexists", result.Failure!.Code));
    }

    [Fact]
    public async Task MSG_CONV_005_AC3_AnUndeclaredTagIsRefusedAndTheTagsAreOnStaffBodiesOnly()
    {
        await using ChatHost chat = await StartAsync();
        await using HubConnection amal = await SupportTests.AvailableAsync(chat, "amal");
        Browser visitor = await SupportTests.AcknowledgedAsync(chat, "192.0.2.1");
        Guid conversation = await visitor.StartAsync();

        using HttpResponseMessage tagged = await chat.SendAsync(HttpMethod.Put, $"/support/conversations/{conversation:D}/tags", "amal", """{ "tags": ["vip", "refund", "vip"] }""");
        Assert.Equal(HttpStatusCode.NoContent, tagged.StatusCode);
        Assert.Equal(["vip", "refund"], await TagsAsync(chat, "amal", conversation));

        await AssertRefusedAsync(chat, HttpMethod.Put, $"/support/conversations/{conversation:D}/tags", "amal", """{ "tags": ["vip", "angry"] }""", HttpStatusCode.BadRequest, "messaging.conversation.invalid", "tags");
        await AssertRefusedAsync(chat, HttpMethod.Put, $"/support/conversations/{conversation:D}/tags", "amal", """{ "tags": null }""", HttpStatusCode.BadRequest, "messaging.request.malformed", "tags");
        await AssertRefusedAsync(chat, HttpMethod.Put, $"/support/conversations/{conversation:D}/tags", "stranger", """{ "tags": ["vip"] }""", HttpStatusCode.NotFound, "messaging.conversation.notfound");

        using HttpResponseMessage current = await visitor.SendAsync(HttpMethod.Get, "/support/conversations/current");
        JsonElement body = await Browser.JsonAsync(current);
        Assert.False(body.TryGetProperty("tags", out _));
        Assert.False(body.TryGetProperty("snoozedUntil", out _));
    }

    [Fact]
    public async Task MSG_CONV_005_AC3_ATagDroppedFromTheDeclarationStaysUntilItIsPutAgain()
    {
        await using ChatHost first = await StartAsync();
        await using HubConnection amal = await SupportTests.AvailableAsync(first, "amal");
        Guid conversation = await (await SupportTests.AcknowledgedAsync(first, "192.0.2.1")).StartAsync();
        Assert.True((await first.CallAsync(chats => chats.SetTagsAsync("amal", new ConversationId(conversation), ["vip"], default))).IsSuccess);

        await using ChatHost second = await SupportTests.StartSupportAsync(postgres, configuration => configuration["Hermes:Conversations:Tags:0"] = "refund", shared: first);
        Assert.True((await second.CallAsync(chats => chats.SetTagsAsync("amal", new ConversationId(conversation), ["vip", "refund"], default))).IsSuccess);
        Assert.True((await second.CallAsync(chats => chats.SetTagsAsync("amal", new ConversationId(conversation), ["refund"], default))).IsSuccess);

        Result again = await second.CallAsync(chats => chats.SetTagsAsync("amal", new ConversationId(conversation), ["vip"], default));
        Assert.Equal(("messaging.conversation.invalid", "tags"), (again.Failure!.Code, again.Failure.Details["field"]));
    }

    [Fact]
    public async Task MSG_CONV_005_AC4_ASnoozeKeepsItsRangeAndTheVisitorsMessageWakesIt()
    {
        await using ChatHost chat = await StartAsync();
        await using HubConnection amal = await SupportTests.AvailableAsync(chat, "amal");
        Browser visitor = await SupportTests.AcknowledgedAsync(chat, "192.0.2.1");
        Guid conversation = await visitor.StartAsync();
        string route = $"/support/conversations/{conversation:D}/snooze";

        await AssertRefusedAsync(chat, HttpMethod.Post, route, "amal", Until(TimeSpan.FromMinutes(4)), HttpStatusCode.BadRequest, "messaging.conversation.invalid", "until");
        await AssertRefusedAsync(chat, HttpMethod.Post, route, "amal", Until(TimeSpan.FromDays(31)), HttpStatusCode.BadRequest, "messaging.conversation.invalid", "until");
        await AssertRefusedAsync(chat, HttpMethod.Post, route, "amal", """{ "until": "tomorrow" }""", HttpStatusCode.BadRequest, "messaging.request.malformed", "until");

        using HttpResponseMessage snoozed = await chat.SendAsync(HttpMethod.Post, route, "amal", Until(TimeSpan.FromHours(1)));
        Assert.Equal(HttpStatusCode.NoContent, snoozed.StatusCode);
        Assert.Equal("snoozed", await StatusAsync(chat, conversation));
        JsonElement staff = await StaffBodyAsync(chat, "amal", conversation);
        Assert.Equal("snoozed", staff.GetProperty("status").GetString());
        Assert.True(DateTimeOffset.Parse(staff.GetProperty("snoozedUntil").GetString()!, CultureInfo.InvariantCulture) > DateTimeOffset.UtcNow.AddMinutes(59));

        // A snoozed conversation stays the visitor's current one and on its assignee's list.
        using HttpResponseMessage current = await visitor.SendAsync(HttpMethod.Get, "/support/conversations/current");
        Assert.Equal(("snoozed", conversation.ToString("D")), ((await Browser.JsonAsync(current)).GetProperty("status").GetString(), (await Browser.JsonAsync(current)).GetProperty("id").GetString()));
        using HttpResponseMessage listed = await chat.SendAsync(HttpMethod.Get, "/conversations", "amal");
        Assert.Contains((await Browser.JsonAsync(listed)).GetProperty("conversations").EnumerateArray(), each => each.GetProperty("id").GetString() == conversation.ToString("D"));
        // A staff member may still write in it, and the visitor's message wakes it at once.
        using HttpResponseMessage written = await visitor.SendAsync(HttpMethod.Post, $"/support/conversations/{conversation:D}/messages", """{ "text": "Still there?" }""");
        Assert.Equal(HttpStatusCode.Created, written.StatusCode);
        Assert.Equal("open", await StatusAsync(chat, conversation));
        Assert.Equal(JsonValueKind.Null, (await StaffBodyAsync(chat, "amal", conversation)).GetProperty("snoozedUntil").ValueKind);
    }

    [Fact]
    public async Task MSG_CONV_005_AC4_ASnoozedConversationKeepsItsAssigneeAndCountsTowardTheirLimit()
    {
        await using ChatHost chat = await StartAsync(configuration => configuration["Hermes:Conversations:Support:Limit"] = "1");
        await using HubConnection amal = await SupportTests.AvailableAsync(chat, "amal");
        Guid conversation = await (await SupportTests.AcknowledgedAsync(chat, "192.0.2.1")).StartAsync();

        Assert.True((await chat.CallAsync(chats => chats.SnoozeAsync("amal", new ConversationId(conversation), DateTimeOffset.UtcNow.AddHours(1), default))).IsSuccess);

        Browser next = await SupportTests.AcknowledgedAsync(chat, "192.0.2.2");
        using HttpResponseMessage refused = await next.SendAsync(HttpMethod.Post, "/support/conversations", """{ "topic": "billing", "text": "Hi" }""");
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("amal", await chat.ScalarAsync(database => With(new NpgsqlCommand("SELECT assignee FROM messaging.conversations WHERE id = @id", database), conversation)));
    }

    [Fact]
    public async Task MSG_CONV_005_AC4_TaggingOrSnoozingAClosedOrANonSupportConversationIsRefused()
    {
        await using ChatHost chat = await StartAsync();
        await using HubConnection amal = await SupportTests.AvailableAsync(chat, "amal");
        Guid conversation = await (await SupportTests.AcknowledgedAsync(chat, "192.0.2.1")).StartAsync();
        Assert.True((await chat.CallAsync(chats => chats.CloseAsync("amal", new ConversationId(conversation), default))).IsSuccess);

        await AssertRefusedAsync(chat, HttpMethod.Put, $"/support/conversations/{conversation:D}/tags", "amal", """{ "tags": ["vip"] }""", HttpStatusCode.Conflict, "messaging.conversation.closed");
        await AssertRefusedAsync(chat, HttpMethod.Post, $"/support/conversations/{conversation:D}/snooze", "amal", Until(TimeSpan.FromHours(1)), HttpStatusCode.Conflict, "messaging.conversation.closed");

        ConversationId direct = (await chat.CallAsync(chats => chats.StartDirectAsync("amal", ["omar"], default))).Value!.Conversation;
        await AssertRefusedAsync(chat, HttpMethod.Put, $"/support/conversations/{direct.Value:D}/tags", "amal", """{ "tags": ["vip"] }""", HttpStatusCode.BadRequest, "messaging.conversation.invalid", "type");
        await AssertRefusedAsync(chat, HttpMethod.Post, $"/support/conversations/{direct.Value:D}/snooze", "amal", Until(TimeSpan.FromHours(1)), HttpStatusCode.BadRequest, "messaging.conversation.invalid", "type");
    }

    private Task<ChatHost> StartAsync(Action<Dictionary<string, string?>>? configure = null)
    {
        return SupportTests.StartSupportAsync(postgres, configuration =>
        {
            configuration["Hermes:Conversations:Tags:0"] = "vip";
            configuration["Hermes:Conversations:Tags:1"] = "refund";
            configure?.Invoke(configuration);
        });
    }

    private static string Name(int reply)
    {
        return "Reply " + reply.ToString(CultureInfo.InvariantCulture);
    }

    private static string Until(TimeSpan ahead)
    {
        return $$"""{ "until": "{{DateTimeOffset.UtcNow.Add(ahead).ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)}}" }""";
    }

    private static async Task<string[]> NamesAsync(ChatHost chat, string person, string query)
    {
        using HttpResponseMessage listed = await chat.SendAsync(HttpMethod.Get, "/support/replies" + query, person);
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        return [.. (await Browser.JsonAsync(listed)).GetProperty("replies").EnumerateArray().Select(reply => reply.GetProperty("name").GetString()!)];
    }

    private static async Task<JsonElement> StaffBodyAsync(ChatHost chat, string person, Guid conversation)
    {
        using HttpResponseMessage read = await chat.SendAsync(HttpMethod.Get, $"/conversations/{conversation:D}", person);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        return await Browser.JsonAsync(read);
    }

    private static async Task<string[]> TagsAsync(ChatHost chat, string person, Guid conversation)
    {
        return [.. (await StaffBodyAsync(chat, person, conversation)).GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()!)];
    }

    private static async Task<string?> StatusAsync(ChatHost chat, Guid conversation)
    {
        return await chat.ScalarAsync(database => With(new NpgsqlCommand("SELECT status FROM messaging.conversations WHERE id = @id", database), conversation)) as string;
    }

    private static async Task AssertRefusedAsync(ChatHost chat, HttpMethod method, string route, string person, string? json, HttpStatusCode status, string code, string? field = null)
    {
        using HttpResponseMessage response = await chat.SendAsync(method, route, person, json);
        JsonElement body = await Browser.JsonAsync(response);
        Assert.Equal((status, code), (response.StatusCode, body.GetProperty("code").GetString()));
        if (field is not null)
        {
            JsonElement details = body.GetProperty("details");
            string? named = details.TryGetProperty("field", out JsonElement value) ? value.GetString() : details.GetProperty("member").GetString();
            Assert.Equal(field, named);
        }
    }

    private static NpgsqlCommand With(NpgsqlCommand command, Guid id)
    {
        command.Parameters.AddWithValue("id", id);
        return command;
    }
}