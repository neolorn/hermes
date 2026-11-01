using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.AspNetCore.SignalR.Client;

using Npgsql;

using Xunit;

namespace Hermes.Conversations.Tests;

/// <summary>
/// The end of a support conversation (<c>09</c> MSG-CONV-004; <c>05</c> MSG-API-010): its
/// transcript, read or sent by email, and its survey, on the topics of
/// <see cref="SupportTests"/> in English and Arabic.
/// </summary>
[Trait("Category", "integration")]
public sealed class EndTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task MSG_CONV_004_AC1_TheTranscriptHoldsEveryMessageButNotesInItsLanguageNamesNoStaffAndIsServedOnlyClosedToItsVisitor()
    {
        await using ChatHost chat = await StartAsync();
        await using HubConnection amal = await SupportTests.AvailableAsync(chat, "amal");
        Browser visitor = await SupportTests.AcknowledgedAsync(chat, "192.0.2.1");
        visitor.Language = "ar";
        Guid conversation = await visitor.StartAsync(text: "My order is late");
        Assert.True((await chat.CallAsync(chats => chats.WriteAsync("amal", new ConversationId(conversation), "Only for staff", MessageType.Note, null, null, null, default))).IsSuccess);
        Assert.True((await chat.CallAsync(chats => chats.WriteAsync("amal", new ConversationId(conversation), "It ships today", MessageType.Text, null, null, null, default))).IsSuccess);
        string route = $"/support/conversations/{conversation:D}/transcript";

        using HttpResponseMessage open = await visitor.SendAsync(HttpMethod.Get, route);
        Assert.Equal((HttpStatusCode.Conflict, "messaging.support.notclosed"), (open.StatusCode, await CodeAsync(open)));

        Assert.True((await chat.CallAsync(chats => chats.CloseAsync("amal", new ConversationId(conversation), default))).IsSuccess);
        using HttpResponseMessage read = await visitor.SendAsync(HttpMethod.Get, route);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(("text/plain", "utf-8"), (read.Content.Headers.ContentType!.MediaType, read.Content.Headers.ContentType.CharSet));
        Assert.Equal(("attachment", $"transcript-{conversation:D}.txt"), (read.Content.Headers.ContentDisposition!.DispositionType, read.Content.Headers.ContentDisposition.FileName!.Trim('"')));
        string transcript = await read.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.StartsWith("نص محادثتك بشأن billing", transcript, StringComparison.Ordinal);
        Assert.Contains("My order is late", transcript, StringComparison.Ordinal);
        Assert.Contains("It ships today", transcript, StringComparison.Ordinal);
        Assert.Contains("الدعم", transcript, StringComparison.Ordinal);
        Assert.DoesNotContain("Only for staff", transcript, StringComparison.Ordinal);
        Assert.DoesNotContain("amal", transcript, StringComparison.Ordinal);

        Browser stranger = await SupportTests.AcknowledgedAsync(chat, "192.0.2.2");
        using HttpResponseMessage theirs = await stranger.SendAsync(HttpMethod.Get, route);
        Assert.Equal((HttpStatusCode.NotFound, "messaging.conversation.notfound"), (theirs.StatusCode, await CodeAsync(theirs)));
    }

    [Fact]
    public async Task Transcript_PastItsSteps_LeavesOutTheOldestMessagesAndSaysHowMany()
    {
        await using ChatHost chat = await SupportTests.StartSupportAsync(postgres, configuration => configuration["Hermes:Templates:MaxSteps"] = "1000");
        await using HubConnection amal = await SupportTests.AvailableAsync(chat, "amal");
        Browser visitor = await SupportTests.AcknowledgedAsync(chat, "192.0.2.1");
        Guid conversation = await visitor.StartAsync(text: "The first of many");
        await chat.ScalarAsync(database => With(
            new NpgsqlCommand(
                """
                INSERT INTO messaging.messages (id, conversation_id, author_kind, type, sent_at)
                SELECT gen_random_uuid(), @id, 'library', 'automatic', now() + make_interval(secs => n) FROM generate_series(1, 4000) AS n
                """,
                database),
            conversation));
        Assert.True((await chat.CallAsync(chats => chats.WriteAsync("amal", new ConversationId(conversation), "The last word", MessageType.Text, null, null, null, default))).IsSuccess);
        await chat.ScalarAsync(database => With(new NpgsqlCommand("UPDATE messaging.messages SET sent_at = now() + interval '2 hours' WHERE conversation_id = @id AND author = 'amal'", database), conversation));
        Assert.True((await chat.CallAsync(chats => chats.CloseAsync("amal", new ConversationId(conversation), default))).IsSuccess);

        using HttpResponseMessage read = await visitor.SendAsync(HttpMethod.Get, $"/support/conversations/{conversation:D}/transcript");

        string transcript = await read.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Contains("earlier messages are left out.", transcript, StringComparison.Ordinal);
        Assert.DoesNotContain("The first of many", transcript, StringComparison.Ordinal);
        Assert.Contains("The last word", transcript, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MSG_CONV_004_AC2_EmailingItSendsTheDeclaredKindAtMostThreeTimes()
    {
        await using ChatHost chat = await StartAsync();
        await using HubConnection amal = await SupportTests.AvailableAsync(chat, "amal");
        Browser visitor = await SupportTests.AcknowledgedAsync(chat, "192.0.2.1");
        Guid conversation = await visitor.StartAsync();
        string route = $"/support/conversations/{conversation:D}/transcript/email";

        using HttpResponseMessage open = await visitor.SendAsync(HttpMethod.Post, route, """{ "email": "visitor@example.com" }""");
        Assert.Equal((HttpStatusCode.Conflict, "messaging.support.notclosed"), (open.StatusCode, await CodeAsync(open)));
        Assert.True((await chat.CallAsync(chats => chats.CloseAsync("amal", new ConversationId(conversation), default))).IsSuccess);

        using HttpResponseMessage malformed = await visitor.SendAsync(HttpMethod.Post, route, """{ "email": "not an address" }""");
        Assert.Equal((HttpStatusCode.BadRequest, "messaging.request.malformed"), (malformed.StatusCode, await CodeAsync(malformed)));
        for (int send = 0; send < 3; send++)
        {
            using HttpResponseMessage sent = await visitor.SendAsync(HttpMethod.Post, route, """{ "email": "visitor@example.com" }""");
            Assert.Equal(HttpStatusCode.Accepted, sent.StatusCode);
        }

        using HttpResponseMessage fourth = await visitor.SendAsync(HttpMethod.Post, route, """{ "email": "visitor@example.com" }""");
        Assert.Equal((HttpStatusCode.Conflict, "messaging.support.transcriptlimit"), (fourth.StatusCode, await CodeAsync(fourth)));
        Assert.Equal(3L, await chat.ScalarAsync(database => new NpgsqlCommand($"SELECT count(*) FROM messaging.sends WHERE kind = '{SupportTests.TranscriptKind}'", database)));
        Assert.Equal(3, await chat.ScalarAsync(database => With(new NpgsqlCommand("SELECT transcript_sends FROM messaging.conversations WHERE id = @id", database), conversation)));
    }

    [Fact]
    public async Task MSG_CONV_004_AC3_TheSurveyIsTakenOnceWithinSevenDaysAndItsCommentIsProtected()
    {
        await using ChatHost chat = await StartAsync();
        await using HubConnection amal = await SupportTests.AvailableAsync(chat, "amal");
        Browser visitor = await SupportTests.AcknowledgedAsync(chat, "192.0.2.1");
        Guid conversation = await visitor.StartAsync();
        string route = $"/support/conversations/{conversation:D}/survey";

        await AssertRefusedAsync(visitor, route, """{ "resolved": true, "rating": 5 }""", HttpStatusCode.Conflict, "messaging.support.notclosed");
        Assert.True((await chat.CallAsync(chats => chats.CloseAsync("amal", new ConversationId(conversation), default))).IsSuccess);

        await AssertRefusedAsync(visitor, route, """{ "rating": 5 }""", HttpStatusCode.BadRequest, "messaging.support.surveyinvalid", "resolved");
        await AssertRefusedAsync(visitor, route, """{ "resolved": true, "rating": 6 }""", HttpStatusCode.BadRequest, "messaging.support.surveyinvalid", "rating");
        await AssertRefusedAsync(visitor, route, $$"""{ "resolved": true, "rating": 4, "comment": "{{new string('c', 1001)}}" }""", HttpStatusCode.BadRequest, "messaging.support.surveyinvalid", "comment");

        using HttpResponseMessage answered = await visitor.SendAsync(HttpMethod.Post, route, """{ "resolved": true, "rating": 4, "comment": "Quick and kind" }""");
        Assert.Equal(HttpStatusCode.NoContent, answered.StatusCode);
        await AssertRefusedAsync(visitor, route, """{ "resolved": false, "rating": 1 }""", HttpStatusCode.Conflict, "messaging.support.surveyanswered");

        byte[] stored = (byte[])(await chat.ScalarAsync(database => With(new NpgsqlCommand("SELECT survey_comment FROM messaging.conversations WHERE id = @id", database), conversation)))!;
        Assert.DoesNotContain("Quick and kind", Encoding.UTF8.GetString(stored), StringComparison.Ordinal);
        Assert.Equal("amal", await chat.ScalarAsync(database => With(new NpgsqlCommand("SELECT survey_person FROM messaging.conversations WHERE id = @id AND survey_rating = 4 AND survey_resolved", database), conversation)));
    }

    [Fact]
    public async Task Survey_MoreThanSevenDaysAfterTheClose_IsClosed()
    {
        await using ChatHost chat = await StartAsync();
        await using HubConnection amal = await SupportTests.AvailableAsync(chat, "amal");
        Browser visitor = await SupportTests.AcknowledgedAsync(chat, "192.0.2.1");
        Guid conversation = await visitor.StartAsync();
        Assert.True((await chat.CallAsync(chats => chats.CloseAsync("amal", new ConversationId(conversation), default))).IsSuccess);
        await chat.ScalarAsync(database => With(new NpgsqlCommand("UPDATE messaging.conversations SET closed_at = now() - interval '7 days 1 minute' WHERE id = @id", database), conversation));

        await AssertRefusedAsync(visitor, $"/support/conversations/{conversation:D}/survey", """{ "resolved": true, "rating": 5 }""", HttpStatusCode.Conflict, "messaging.support.surveyclosed");
    }

    [Fact]
    public async Task Start_TopicsWithoutATranscriptKind_IsConfigurationInvalidNamingIt()
    {
        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => SupportTests.StartSupportAsync(postgres, configuration => configuration.Remove("Hermes:Conversations:Support:TranscriptKind")));

        Assert.Equal(("messaging.startup.configurationinvalid", "Conversations:Support:TranscriptKind"), (failure.Code, failure.Details["key"]));
    }

    [Fact]
    public async Task Start_ALanguageWithoutATranscriptPage_IsDeclarationInvalidNamingTheTranscript()
    {
        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => SupportTests.StartSupportAsync(postgres, configuration => configuration["Hermes:Languages:1"] = "fr"));

        Assert.Equal(("messaging.startup.declarationinvalid", "transcript", "fr.text"), (failure.Code, failure.Details["declaration"], failure.Details["field"]));
    }

    private Task<ChatHost> StartAsync()
    {
        return SupportTests.StartSupportAsync(postgres, configuration => configuration["Hermes:Languages:1"] = "ar");
    }

    private static async Task AssertRefusedAsync(Browser visitor, string route, string json, HttpStatusCode status, string code, string? field = null)
    {
        using HttpResponseMessage response = await visitor.SendAsync(HttpMethod.Post, route, json);
        JsonElement body = await Browser.JsonAsync(response);
        Assert.Equal((status, code), (response.StatusCode, body.GetProperty("code").GetString()));
        if (field is not null)
        {
            Assert.Equal(field, body.GetProperty("details").GetProperty("field").GetString());
        }
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