using System;
using System.Collections.Generic;
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
/// Support's public and staff sides (<c>09</c> MSG-CONV-001, MSG-CONV-002, MSG-CONV-010,
/// MSG-CONV-011; <c>05</c> MSG-API-009, MSG-API-010): the topic <c>billing</c>, open all week,
/// staffed by the group <c>team-billing</c>; the topic <c>night</c>, closed every day.
/// </summary>
[Trait("Category", "integration")]
public sealed class SupportTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    internal const string ReplyKind = "support.reply";

    internal static readonly ConversationScope Team = new() { Kind = ScopeKind.Group, Reference = "team-billing" };

    [Fact]
    public async Task MSG_CONV_002_AC1_AConversationGoesToTheAvailableMemberWithTheFewestOpenTiesToTheLongestSinceTheirLast()
    {
        await using ChatHost chat = await StartAsync();
        await using HubConnection amal = await AvailableAsync(chat, "amal");
        await using HubConnection omar = await AvailableAsync(chat, "omar");

        Guid first = await VisitorAsync(chat, "192.0.2.1");
        Guid second = await VisitorAsync(chat, "192.0.2.2");
        Guid third = await VisitorAsync(chat, "192.0.2.3");

        Assert.Equal("amal", await AssigneeAsync(chat, first));
        Assert.Equal("omar", await AssigneeAsync(chat, second));
        Assert.Equal("amal", await AssigneeAsync(chat, third));
    }

    [Fact]
    public async Task MSG_CONV_002_AC1_TheQueueIsFirstInFirstOutAndAMemberWhoFreesUpTakesTheOldest()
    {
        await using ChatHost chat = await StartAsync(configure: configuration => configuration["Hermes:Conversations:Support:Limit"] = "1");
        await using HubConnection amal = await AvailableAsync(chat, "amal");
        Guid taken = await VisitorAsync(chat, "192.0.2.1");

        // With amal at her limit nobody is available, so the next two come through the offline form, queued.
        Guid older = await OfflineAsync(chat, "192.0.2.2");
        Guid newer = await OfflineAsync(chat, "192.0.2.3");
        Assert.Null(await AssigneeAsync(chat, older));

        Assert.True((await chat.CallAsync(chats => chats.CloseAsync("amal", new ConversationId(taken), default))).IsSuccess);

        Assert.Equal("amal", await AssigneeAsync(chat, older));
        Assert.Null(await AssigneeAsync(chat, newer));
    }

    [Fact]
    public async Task Assign_AMemberSetAvailableWhoConnects_TakesTheQueuedConversation()
    {
        await using ChatHost chat = await StartAsync();
        Guid queued = await OfflineAsync(chat, "192.0.2.1");
        Assert.True((await chat.CallAsync(chats => chats.SetAvailableAsync("omar", true, default))).IsSuccess);
        Assert.Null(await AssigneeAsync(chat, queued));

        await using HubConnection omar = await chat.ConnectAsync("omar");

        for (int wait = 0; wait < 100 && await AssigneeAsync(chat, queued) is null; wait++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        Assert.Equal("omar", await AssigneeAsync(chat, queued));
    }

    [Fact]
    public async Task MSG_CONV_002_AC2_TheVisitorsPlaceIsRight()
    {
        await using ChatHost chat = await StartAsync();
        Browser first = await AcknowledgedAsync(chat, "192.0.2.1");
        Browser second = await AcknowledgedAsync(chat, "192.0.2.2");
        Guid older = await OfflineAsync(chat, first);
        await OfflineAsync(chat, second);

        Assert.Equal(2, await PlaceAsync(second));
        Assert.Equal(1, await PlaceAsync(first));

        await chat.CallAsync(chats => chats.SetAvailableAsync("amal", true, default));
        Assert.True((await chat.CallAsync(chats => chats.CloseAsync("amal", new ConversationId(older), default))).IsSuccess);
        Assert.Equal(1, await PlaceAsync(second));
    }

    [Fact]
    public async Task MSG_CONV_002_AC3_ATransferMovesTheConversationToAMemberOrAnotherTopicsQueue()
    {
        await using ChatHost chat = await StartAsync();
        await using HubConnection amal = await AvailableAsync(chat, "amal");
        chat.Membership.Add(Team, "omar");
        Guid conversation = await VisitorAsync(chat, "192.0.2.1");

        Assert.True((await chat.CallAsync(chats => chats.TransferAsync("amal", new ConversationId(conversation), "omar", null, default))).IsSuccess);
        Assert.Equal("omar", await AssigneeAsync(chat, conversation));

        Assert.True((await chat.CallAsync(chats => chats.TransferAsync("omar", new ConversationId(conversation), null, "night", default))).IsSuccess);
        Assert.Null(await AssigneeAsync(chat, conversation));
        Assert.Equal("night", await chat.ScalarAsync(database => With(new NpgsqlCommand("SELECT topic FROM messaging.conversations WHERE id = @id", database), conversation)));

        Result outside = await chat.CallAsync(chats => chats.TransferAsync("omar", new ConversationId(conversation), "stranger", null, default));
        Assert.Equal(("messaging.conversation.invalid", "person"), (outside.Failure!.Code, outside.Failure.Details["field"]));
        Result unknown = await chat.CallAsync(chats => chats.TransferAsync("omar", new ConversationId(conversation), null, "unknown", default));
        Assert.Equal(("messaging.conversation.invalid", "topic"), (unknown.Failure!.Code, unknown.Failure.Details["field"]));
    }

    [Fact]
    public async Task MSG_CONV_002_AC4_OutsideHoursOrWithNobodyAvailableTheStartIsOfflineAndTheOfflineFormIsAnsweredByEmail()
    {
        await using ChatHost chat = await StartAsync();
        Browser visitor = await AcknowledgedAsync(chat, "192.0.2.1");

        using HttpResponseMessage nobody = await visitor.SendAsync(HttpMethod.Post, "/support/conversations", """{ "topic": "billing", "text": "Hi" }""");
        Assert.Equal((HttpStatusCode.Conflict, "messaging.support.offline"), (nobody.StatusCode, await CodeAsync(nobody)));

        await using HubConnection amal = await AvailableAsync(chat, "amal");
        using HttpResponseMessage night = await visitor.SendAsync(HttpMethod.Post, "/support/conversations", """{ "topic": "night", "text": "Hi" }""");
        Assert.Equal((HttpStatusCode.Conflict, "messaging.support.offline"), (night.StatusCode, await CodeAsync(night)));

        using HttpResponseMessage form = await visitor.SendAsync(HttpMethod.Post, "/support/offline", """{ "topic": "night", "email": "visitor@example.com", "text": "Call me" }""");
        Assert.Equal(HttpStatusCode.Created, form.StatusCode);
        JsonElement offline = await Browser.JsonAsync(form);
        Assert.True(offline.GetProperty("offline").GetBoolean());
        Guid conversation = Guid.Parse(offline.GetProperty("id").GetString()!);
        Assert.Equal(0L, await chat.ScalarAsync(database => new NpgsqlCommand("SELECT count(*) FROM messaging.conversations WHERE email::text LIKE '%visitor@example.com%'", database)));

        Assert.True((await chat.CallAsync(chats => chats.TransferAsync("amal", new ConversationId(conversation), "amal", null, default))).IsSuccess);
        Assert.True((await chat.CallAsync(chats => chats.WriteAsync("amal", new ConversationId(conversation), "We will call", MessageType.Text, null, null, default))).IsSuccess);
        Assert.True((await chat.CallAsync(chats => chats.WriteAsync("amal", new ConversationId(conversation), "An inside note", MessageType.Note, null, null, default))).IsSuccess);

        Assert.Equal(1L, await chat.ScalarAsync(database => new NpgsqlCommand($"SELECT count(*) FROM messaging.sends WHERE kind = '{ReplyKind}'", database)));
        Assert.Equal("email", await chat.ScalarAsync(database => new NpgsqlCommand("SELECT channel FROM messaging.deliveries", database)));
    }

    [Fact]
    public async Task OfflineReply_AnAddressCopiedFromAnotherRow_SendsNothingAndRaisesContentRejectedForConversations()
    {
        await using ChatHost chat = await StartAsync();
        Guid first = await OfflineAsync(chat, "192.0.2.1");
        Guid second = await OfflineAsync(chat, "192.0.2.2");
        await chat.ScalarAsync(database =>
        {
            NpgsqlCommand command = new("UPDATE messaging.conversations SET email = (SELECT email FROM messaging.conversations WHERE id = @from) WHERE id = @to", database);
            command.Parameters.AddWithValue("from", first);
            command.Parameters.AddWithValue("to", second);
            return command;
        });

        Assert.True((await chat.CallAsync(chats => chats.TransferAsync("amal", new ConversationId(second), "amal", null, default))).IsSuccess);
        Assert.True((await chat.CallAsync(chats => chats.WriteAsync("amal", new ConversationId(second), "We will call", MessageType.Text, null, null, default))).IsSuccess);

        Assert.Equal(0L, await chat.ScalarAsync(database => new NpgsqlCommand("SELECT count(*) FROM messaging.sends", database)));
        Assert.Equal(1L, await chat.ScalarAsync(database => new NpgsqlCommand("SELECT count(*) FROM messaging.alerts WHERE condition = 'content-rejected' AND scope = 'conversations' AND holding", database)));
    }

    [Fact]
    public async Task MSG_CONV_001_AC3_TheVisitorsReferenceIsNeverStoredInClearAndAnotherBrowsersCookieOpensNothing()
    {
        await using ChatHost chat = await StartAsync();
        await using HubConnection amal = await AvailableAsync(chat, "amal");
        Browser visitor = await AcknowledgedAsync(chat, "192.0.2.1");
        Guid conversation = await visitor.StartAsync();
        Assert.Contains("httponly", visitor.SetCookie!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", visitor.SetCookie!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", visitor.SetCookie!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/public", visitor.SetCookie!, StringComparison.OrdinalIgnoreCase);

        string reference = Uri.UnescapeDataString(visitor.Cookie!);
        Assert.StartsWith("visitor:", reference, StringComparison.Ordinal);
        object? found = await chat.ScalarAsync(database =>
        {
            NpgsqlCommand command = new(
                """
                SELECT count(*) FROM messaging.participants AS p, messaging.support_acknowledgements AS a
                WHERE position(convert_to(@reference, 'UTF8') in p.visitor) > 0 OR position(convert_to(@reference, 'UTF8') in a.visitor) > 0
                   OR EXISTS (SELECT 1 FROM messaging.conversations AS c WHERE c.customer = @reference)
                """,
                database);
            command.Parameters.AddWithValue("reference", reference);
            return command;
        });
        Assert.Equal(0L, found);

        Browser other = await AcknowledgedAsync(chat, "192.0.2.2");
        using HttpResponseMessage read = await other.SendAsync(HttpMethod.Get, $"/support/conversations/{conversation:D}/messages");
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        using HttpResponseMessage current = await other.SendAsync(HttpMethod.Get, "/support/conversations/current");
        Assert.Equal(HttpStatusCode.NotFound, current.StatusCode);
        using HttpResponseMessage mine = await visitor.SendAsync(HttpMethod.Get, $"/support/conversations/{conversation:D}/messages");
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
    }

    [Fact]
    public async Task MSG_CONV_001_AC4_AVisitorNeverReadsANote()
    {
        await using ChatHost chat = await StartAsync();
        await using HubConnection amal = await AvailableAsync(chat, "amal");
        Browser visitor = await AcknowledgedAsync(chat, "192.0.2.1");
        Guid conversation = await visitor.StartAsync();
        await chat.CallAsync(chats => chats.WriteAsync("amal", new ConversationId(conversation), "Only for staff", MessageType.Note, null, null, default));
        await chat.CallAsync(chats => chats.WriteAsync("amal", new ConversationId(conversation), "Hello there", MessageType.Text, null, null, default));

        using HttpResponseMessage read = await visitor.SendAsync(HttpMethod.Get, $"/support/conversations/{conversation:D}/messages");
        string[] texts = [.. (await Browser.JsonAsync(read)).GetProperty("messages").EnumerateArray().Select(message => message.GetProperty("text").GetString()!)];

        Assert.Equal(["Hello there", "Hello"], texts);
        MessagePage staff = (await chat.CallAsync(chats => chats.MessagesAsync("amal", new ConversationId(conversation), null, null, null, default))).Value!;
        Assert.Equal(3, staff.Messages.Count);
    }

    [Fact]
    public async Task MSG_CONV_010_AC1_NoVisitorMessageIsTakenBeforeTheCurrentNoticeIsAcknowledgedAndACustomerIsNeverAsked()
    {
        await using ChatHost chat = await StartAsync();
        await using HubConnection amal = await AvailableAsync(chat, "amal");
        Browser visitor = new(chat, source: "192.0.2.1");

        using HttpResponseMessage before = await visitor.SendAsync(HttpMethod.Post, "/support/conversations", """{ "topic": "billing", "text": "Hi" }""");
        Assert.Equal((HttpStatusCode.Conflict, "messaging.support.noticerequired"), (before.StatusCode, await CodeAsync(before)));
        using HttpResponseMessage offline = await visitor.SendAsync(HttpMethod.Post, "/support/offline", """{ "topic": "billing", "email": "v@example.com", "text": "Hi" }""");
        Assert.Equal((HttpStatusCode.Conflict, "messaging.support.noticerequired"), (offline.StatusCode, await CodeAsync(offline)));
        using HttpResponseMessage old = await visitor.AcknowledgeAsync("v0");
        using HttpResponseMessage stillBefore = await visitor.SendAsync(HttpMethod.Post, "/support/conversations", """{ "topic": "billing", "text": "Hi" }""");
        Assert.Equal("messaging.support.noticerequired", await CodeAsync(stillBefore));

        using HttpResponseMessage acknowledged = await visitor.AcknowledgeAsync();
        Assert.Equal(HttpStatusCode.NoContent, acknowledged.StatusCode);
        await visitor.StartAsync();
        Assert.Equal(0L, await chat.ScalarAsync(database => new NpgsqlCommand("SELECT count(*) FROM messaging.messages WHERE text IS NULL", database)));

        Browser customer = new(chat, person: "customer-41", source: "192.0.2.2");
        await customer.StartAsync();
        Assert.Null(customer.Cookie);
        Assert.Equal(2L, await chat.ScalarAsync(database => new NpgsqlCommand("SELECT count(*) FROM messaging.support_acknowledgements", database)));
    }

    [Fact]
    public async Task MSG_CONV_010_AC2_ErasingACustomerDeletesTheirSupportConversations()
    {
        await using ChatHost chat = await StartAsync();
        await using HubConnection amal = await AvailableAsync(chat, "amal");
        Browser customer = new(chat, person: "customer-41", source: "192.0.2.1");
        Guid conversation = await customer.StartAsync();
        await chat.CallAsync(chats => chats.WriteAsync("amal", new ConversationId(conversation), "A reply", MessageType.Text, null, null, default));
        Guid kept = await (await AcknowledgedAsync(chat, "192.0.2.2")).StartAsync();

        Assert.True((await chat.SubjectsAsync(subjects => subjects.EraseAsync("customer-41", default))).IsSuccess);

        Assert.Equal(0L, await chat.ScalarAsync(database => With(new NpgsqlCommand("SELECT count(*) FROM messaging.messages WHERE conversation_id = @id", database), conversation)));
        Assert.Equal(1L, await chat.ScalarAsync(database => new NpgsqlCommand("SELECT count(*) FROM messaging.conversations", database)));
        Assert.Equal(1L, await chat.ScalarAsync(database => With(new NpgsqlCommand("SELECT count(*) FROM messaging.messages WHERE conversation_id = @id", database), kept)));
    }

    [Fact]
    public async Task MSG_CONV_011_AC1_EachLimitAnswers429AtItsEdgeAcrossTwoProcesses()
    {
        await using ChatHost one = await StartAsync(configure: Limits);
        await using ChatHost two = await StartAsync(Limits, one);
        await using HubConnection amal = await AvailableAsync(one, "amal");

        // Per visitor: two starts a day, the third refused, one started through each process.
        Browser visitor = await AcknowledgedAsync(one, "192.0.2.1");
        Guid first = await visitor.StartAsync("billing");
        Browser again = new(two, source: "192.0.2.9") { Cookie = visitor.Cookie };
        await again.StartAsync("other");
        using HttpResponseMessage third = await again.SendAsync(HttpMethod.Post, "/support/conversations", """{ "topic": "late", "text": "Hi" }""");
        Assert.Equal((HttpStatusCode.TooManyRequests, "messaging.support.limited"), (third.StatusCode, await CodeAsync(third)));
        Assert.True(third.Headers.RetryAfter?.Delta > TimeSpan.Zero);

        // Per visitor: three messages a minute, the start's own not counted. The minute's window is
        // fixed, so the four writes wait until they all fall in one.
        while (DateTimeOffset.UtcNow.Second >= 50)
        {
            await Task.Delay(200, TestContext.Current.CancellationToken);
        }

        for (int index = 0; index < 3; index++)
        {
            using HttpResponseMessage written = await (index % 2 == 0 ? visitor : again).SendAsync(HttpMethod.Post, $"/support/conversations/{first:D}/messages", """{ "text": "More" }""");
            Assert.Equal(HttpStatusCode.Created, written.StatusCode);
        }

        using HttpResponseMessage fourth = await visitor.SendAsync(HttpMethod.Post, $"/support/conversations/{first:D}/messages", """{ "text": "More" }""");
        Assert.Equal(HttpStatusCode.TooManyRequests, fourth.StatusCode);

        // Per source address: three starts an hour, from four visitors through both processes.
        string[] seen = [];
        for (int index = 0; index < 4; index++)
        {
            Browser each = await AcknowledgedAsync(index % 2 == 0 ? one : two, "203.0.113." + (index + 10).ToString(System.Globalization.CultureInfo.InvariantCulture));
            each.Source = "198.51.100.7";
            using HttpResponseMessage started = await each.SendAsync(HttpMethod.Post, "/support/conversations", """{ "topic": "billing", "text": "Hi" }""");
            seen = [.. seen, started.StatusCode.ToString() + (started.IsSuccessStatusCode ? string.Empty : " " + await CodeAsync(started))];
        }

        Assert.Equal(["Created", "Created", "Created", "TooManyRequests messaging.support.limited"], seen);
        Browser blockedSource = new(two, source: "198.51.100.7") { Cookie = (await AcknowledgedAsync(one, "203.0.113.1")).Cookie };
        using HttpResponseMessage notice = await blockedSource.AcknowledgeAsync();
        Assert.Equal(HttpStatusCode.TooManyRequests, notice.StatusCode);
    }

    [Fact]
    public async Task MSG_CONV_011_AC2_ABlockedVisitorIsRefusedUntilTheBlockEndsOrIsLifted()
    {
        await using ChatHost chat = await StartAsync();
        await using HubConnection amal = await AvailableAsync(chat, "amal");
        Browser visitor = await AcknowledgedAsync(chat, "192.0.2.1");
        Guid conversation = await visitor.StartAsync();

        Assert.True((await chat.CallAsync(chats => chats.BlockAsync("amal", new ConversationId(conversation), null, default))).IsSuccess);
        using HttpResponseMessage written = await visitor.SendAsync(HttpMethod.Post, $"/support/conversations/{conversation:D}/messages", """{ "text": "Let me in" }""");
        Assert.Equal((HttpStatusCode.Forbidden, "messaging.support.blocked"), (written.StatusCode, await CodeAsync(written)));
        using HttpResponseMessage started = await visitor.SendAsync(HttpMethod.Post, "/support/conversations", """{ "topic": "other", "text": "Hi" }""");
        Assert.Equal(HttpStatusCode.Forbidden, started.StatusCode);

        Assert.True((await chat.CallAsync(chats => chats.UnblockAsync("amal", new ConversationId(conversation), default))).IsSuccess);
        using HttpResponseMessage lifted = await visitor.SendAsync(HttpMethod.Post, $"/support/conversations/{conversation:D}/messages", """{ "text": "Thanks" }""");
        Assert.Equal(HttpStatusCode.Created, lifted.StatusCode);

        Assert.True((await chat.CallAsync(chats => chats.BlockAsync("amal", new ConversationId(conversation), DateTimeOffset.UtcNow.AddSeconds(-1), default))).IsSuccess);
        using HttpResponseMessage ended = await visitor.SendAsync(HttpMethod.Post, $"/support/conversations/{conversation:D}/messages", """{ "text": "Ended" }""");
        Assert.Equal(HttpStatusCode.Created, ended.StatusCode);
    }

    [Fact]
    public async Task Block_AnUntilThroughTheRoute_IsReadInUtcWhateverTheHostsZone()
    {
        await using ChatHost chat = await StartAsync();
        await using HubConnection amal = await AvailableAsync(chat, "amal");
        Browser visitor = await AcknowledgedAsync(chat, "192.0.2.1");
        Guid conversation = await visitor.StartAsync();
        DateTimeOffset until = DateTimeOffset.UtcNow.AddHours(1);

        using HttpResponseMessage blocked = await chat.SendAsync(HttpMethod.Post, $"/support/conversations/{conversation:D}/block", "amal", $$"""{ "until": "{{until.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture)}}" }""");
        Assert.Equal(HttpStatusCode.NoContent, blocked.StatusCode);

        DateTimeOffset stored = new((DateTime)(await chat.ScalarAsync(database => new NpgsqlCommand("SELECT until FROM messaging.support_blocks", database)))!);
        Assert.True((stored - until).Duration() < TimeSpan.FromSeconds(1));
        using HttpResponseMessage written = await visitor.SendAsync(HttpMethod.Post, $"/support/conversations/{conversation:D}/messages", """{ "text": "Let me in" }""");
        Assert.Equal(HttpStatusCode.Forbidden, written.StatusCode);
    }

    [Fact]
    public async Task Close_ByStaffOrByTheVisitor_WritesOneSystemMessageWithoutTextPushedAsAnyMessage()
    {
        await using ChatHost chat = await StartAsync();
        await using HubConnection amal = await AvailableAsync(chat, "amal");
        HubInbox staff = HubInbox.Of(amal);
        Browser visitor = await AcknowledgedAsync(chat, "192.0.2.1");
        Guid byStaff = await visitor.StartAsync();
        Guid byVisitor = await visitor.StartAsync("other");

        Assert.True((await chat.CallAsync(chats => chats.CloseAsync("amal", new ConversationId(byStaff), default))).IsSuccess);
        using HttpResponseMessage left = await visitor.SendAsync(HttpMethod.Post, $"/support/conversations/{byVisitor:D}/close", "{ }");
        Assert.Equal(HttpStatusCode.NoContent, left.StatusCode);
        Assert.True((await chat.CallAsync(chats => chats.CloseAsync("amal", new ConversationId(byStaff), default))).IsSuccess);

        foreach (Guid conversation in new[] { byStaff, byVisitor })
        {
            using HttpResponseMessage read = await visitor.SendAsync(HttpMethod.Get, $"/support/conversations/{conversation:D}/messages");
            JsonElement[] messages = [.. (await Browser.JsonAsync(read)).GetProperty("messages").EnumerateArray()];
            Assert.Equal(("system", "library", JsonValueKind.Null), (messages[0].GetProperty("type").GetString(), messages[0].GetProperty("authorKind").GetString(), messages[0].GetProperty("text").ValueKind));
            Assert.Single(messages, message => message.GetProperty("type").GetString() == "system");
            await staff.NextAsync("message", arguments => arguments[0].GetString() == conversation.ToString("D") && arguments[1].GetProperty("type").GetString() == "system");
        }
    }

    [Fact]
    public async Task Counters_EachStartAndEachClose_AreCountedByTopic()
    {
        System.Collections.Concurrent.ConcurrentBag<(string Name, long Value, string? Topic)> measured = [];
        using System.Diagnostics.Metrics.MeterListener listener = new()
        {
            InstrumentPublished = (instrument, listening) =>
            {
                if (instrument.Meter.Name == "Hermes" && instrument.Name.StartsWith("hermes.conversations.", StringComparison.Ordinal))
                {
                    listening.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            string? topic = null;
            foreach (KeyValuePair<string, object?> tag in tags)
            {
                topic = tag.Key == "topic" ? tag.Value as string : topic;
            }

            measured.Add((instrument.Name, value, topic));
        });
        listener.Start();
        await using ChatHost chat = await StartAsync();
        await using HubConnection amal = await AvailableAsync(chat, "amal");
        Browser visitor = await AcknowledgedAsync(chat, "192.0.2.1");

        Guid started = await visitor.StartAsync("late");
        Assert.True((await chat.CallAsync(chats => chats.CloseAsync("amal", new ConversationId(started), default))).IsSuccess);
        Assert.True((await chat.CallAsync(chats => chats.CloseAsync("amal", new ConversationId(started), default))).IsSuccess);

        Assert.Equal(1, measured.Where(each => each.Topic == "late" && each.Name == "hermes.conversations.started").Sum(each => each.Value));
        Assert.Equal(1, measured.Where(each => each.Topic == "late" && each.Name == "hermes.conversations.closed").Sum(each => each.Value));
    }

    [Fact]
    public async Task Start_ASecondOnTheSameTopic_IsAlreadyOpenNamingTheOpenOneAndCurrentIsTheNewest()
    {
        await using ChatHost chat = await StartAsync();
        await using HubConnection amal = await AvailableAsync(chat, "amal");
        Browser visitor = await AcknowledgedAsync(chat, "192.0.2.1");
        Guid first = await visitor.StartAsync();

        using HttpResponseMessage second = await visitor.SendAsync(HttpMethod.Post, "/support/conversations", """{ "topic": "billing", "text": "Again" }""");
        Assert.Equal((HttpStatusCode.Conflict, "messaging.support.alreadyopen"), (second.StatusCode, await CodeAsync(second)));
        Assert.Equal(first.ToString("D"), (await Browser.JsonAsync(second)).GetProperty("details").GetProperty("conversation").GetString());

        Guid other = await visitor.StartAsync("other");
        using HttpResponseMessage current = await visitor.SendAsync(HttpMethod.Get, "/support/conversations/current");
        Assert.Equal(other.ToString("D"), (await Browser.JsonAsync(current)).GetProperty("id").GetString());
    }

    [Fact]
    public async Task StaffAccess_AMemberReadsAndOnlyTheAssigneeWritesAndAnyoneElseIsNotFound()
    {
        await using ChatHost chat = await StartAsync();
        await using HubConnection amal = await AvailableAsync(chat, "amal");
        chat.Membership.Add(Team, "omar");
        Guid conversation = await VisitorAsync(chat, "192.0.2.1");
        ConversationId id = new(conversation);

        Assert.Equal("billing", (await chat.CallAsync(chats => chats.ReadAsync("omar", id, default))).Value!.Topic);
        Assert.Equal("messaging.conversation.notfound", (await chat.CallAsync(chats => chats.WriteAsync("omar", id, "Me too", MessageType.Text, null, null, default))).Failure!.Code);
        Assert.Equal("messaging.conversation.notfound", (await chat.CallAsync(chats => chats.ReadAsync("stranger", id, default))).Failure!.Code);
        Assert.Equal("messaging.conversation.notfound", (await chat.CallAsync(chats => chats.CloseAsync("stranger", id, default))).Failure!.Code);
        Assert.Empty((await chat.CallAsync(chats => chats.QueueAsync("stranger", "billing", default))).Value!.Conversations);
        Assert.Equal("messaging.request.malformed", (await chat.CallAsync(chats => chats.QueueAsync("amal", "unknown", default))).Failure!.Code);

        ConversationRecord listed = Assert.Single((await chat.CallAsync(chats => chats.ListAsync("amal", null, null, default))).Value!.Conversations);
        Assert.Equal((ConversationType.Support, ConversationStatus.Open, (int?)null, false), (listed.Type, listed.Status!.Value, listed.Place, listed.Offline!.Value));
        Assert.Empty((await chat.CallAsync(chats => chats.ListAsync("omar", null, null, default))).Value!.Conversations);
    }

    [Fact]
    public async Task Start_OfflineReplyKindNotDeclared_StopsTheStartNamingTheKey()
    {
        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => ChatHost.StartAsync(postgres, configure: configuration =>
        {
            Topics(configuration);
            configuration["Hermes:Conversations:Support:OfflineReplyKind"] = "support.missing";
        }));

        Assert.Equal(("messaging.startup.configurationinvalid", "Conversations:Support:OfflineReplyKind"), (failure.Code, failure.Details["key"]));
    }

    /// <summary>Starts a host with the topics, the reply kind and the team's members; on the database of <paramref name="shared"/> where given.</summary>
    private Task<ChatHost> StartAsync(Action<Dictionary<string, string?>>? configure = null, ChatHost? shared = null)
    {
        return StartSupportAsync(postgres, configure, shared);
    }

    /// <summary>Starts a host with the topics, the reply kind and the team's members; on the database of <paramref name="shared"/> where given.</summary>
    internal static async Task<ChatHost> StartSupportAsync(PostgresFixture postgres, Action<Dictionary<string, string?>>? configure = null, ChatHost? shared = null)
    {
        ChatHost chat = await ChatHost.StartAsync(
            postgres,
            shared: shared,
            configure: configuration =>
            {
                Topics(configuration);
                configure?.Invoke(configuration);
            },
            declare: declare =>
            {
                declare.Category("support").Essential();
                declare.Kind(ReplyKind, "support").Email();
            });
        chat.Membership.Add(Team, "amal", "omar");
        chat.Membership.Add(new ConversationScope { Kind = ScopeKind.Group, Reference = "team-other" }, "amal");
        return chat;
    }
    /// <summary>The topics <c>billing</c> and <c>other</c>, open all week, and <c>night</c> and <c>late</c>, closed every day.</summary>
    internal static void Topics(Dictionary<string, string?> configuration)
    {
        foreach ((string name, string scope, bool open) in new[] { ("billing", "team-billing", true), ("other", "team-other", true), ("night", "team-billing", false), ("late", "team-other", true) })
        {
            string topic = "Hermes:Conversations:Topics:" + name;
            configuration[topic + ":TimeZone"] = "Europe/Berlin";
            configuration[topic + ":Scope:Kind"] = "Group";
            configuration[topic + ":Scope:Reference"] = scope;
            if (open)
            {
                foreach (string day in new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" })
                {
                    configuration[topic + ":Hours:" + day + ":0"] = "00:00-24:00";
                }
            }
        }

        configuration["Hermes:Conversations:Support:OfflineReplyKind"] = ReplyKind;
        configuration["Hermes:Conversations:Support:NoticeVersion"] = "v1";
        configuration["Hermes:PublicBaseAddress"] = "https://shop.example";
        configuration["Hermes:Email:From:Address"] = "support@shop.example";
    }

    private static void Limits(Dictionary<string, string?> configuration)
    {
        configuration["Hermes:Conversations:Limits:VisitorStarts"] = "2";
        configuration["Hermes:Conversations:Limits:VisitorMessages"] = "3";
        configuration["Hermes:Conversations:Limits:SourceStarts"] = "3";
        configuration["Hermes:Conversations:Support:Limit"] = "10";
    }

    /// <summary>Connects <paramref name="person"/> to the staff hub and sets them available.</summary>
    internal static async Task<HubConnection> AvailableAsync(ChatHost chat, string person)
    {
        HubConnection hub = await chat.ConnectAsync(person);
        Assert.True((await chat.CallAsync(chats => chats.SetAvailableAsync(person, true, default))).IsSuccess);
        return hub;
    }

    /// <summary>A visitor from <paramref name="source"/> who acknowledged the current notice.</summary>
    internal static async Task<Browser> AcknowledgedAsync(ChatHost chat, string source)
    {
        Browser visitor = new(chat, source: source);
        using HttpResponseMessage acknowledged = await visitor.AcknowledgeAsync();
        Assert.Equal(HttpStatusCode.NoContent, acknowledged.StatusCode);
        return visitor;
    }

    private static async Task<Guid> VisitorAsync(ChatHost chat, string source)
    {
        return await (await AcknowledgedAsync(chat, source)).StartAsync();
    }

    private static async Task<Guid> OfflineAsync(ChatHost chat, string source)
    {
        return await OfflineAsync(chat, await AcknowledgedAsync(chat, source));
    }

    private static async Task<Guid> OfflineAsync(ChatHost chat, Browser visitor)
    {
        _ = chat;
        using HttpResponseMessage form = await visitor.SendAsync(HttpMethod.Post, "/support/offline", """{ "topic": "billing", "email": "visitor@example.com", "text": "Call me" }""");
        Assert.Equal(HttpStatusCode.Created, form.StatusCode);
        return Guid.Parse((await Browser.JsonAsync(form)).GetProperty("id").GetString()!);
    }

    private static async Task<int?> PlaceAsync(Browser visitor)
    {
        using HttpResponseMessage current = await visitor.SendAsync(HttpMethod.Get, "/support/conversations/current");
        JsonElement place = (await Browser.JsonAsync(current)).GetProperty("place");
        return place.ValueKind == JsonValueKind.Null ? null : place.GetInt32();
    }

    private static async Task<string?> AssigneeAsync(ChatHost chat, Guid conversation)
    {
        return await chat.ScalarAsync(database => With(new NpgsqlCommand("SELECT assignee FROM messaging.conversations WHERE id = @id", database), conversation)) as string;
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        return (await Browser.JsonAsync(response)).GetProperty("code").GetString();
    }

    private static NpgsqlCommand With(NpgsqlCommand command, Guid id)
    {
        command.Parameters.AddWithValue("id", id);
        return command;
    }
}