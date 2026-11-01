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
/// A topic's supervision (<c>09</c> MSG-CONV-005; <c>05</c> MSG-API-009): the topic
/// <c>billing</c> of <see cref="SupportTests"/> supervised by the group <c>team-leads</c>, whose
/// one member is <c>sara</c>; the topic <c>other</c> supervised by nobody.
/// </summary>
[Trait("Category", "integration")]
public sealed class SupervisionTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly int[] Places = [1, 2];

    [Fact]
    public async Task MSG_CONV_005_AC7_OnlyASupervisorOfTheTopicReadsItsLiveViewAndReports()
    {
        await using ChatHost chat = await StartAsync();
        await using HubConnection amal = await SupportTests.AvailableAsync(chat, "amal");
        Guid assigned = await (await SupportTests.AcknowledgedAsync(chat, "192.0.2.1")).StartAsync();
        Assert.True((await chat.CallAsync(chats => chats.SetAvailableAsync("amal", false, default))).IsSuccess);
        Guid second = await OfflineAsync(chat, "192.0.2.2", "billing");
        Guid third = await OfflineAsync(chat, "192.0.2.3", "billing");

        using HttpResponseMessage live = await chat.SendAsync(HttpMethod.Get, "/support/live?topic=billing", "sara");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        JsonElement[] conversations = [.. (await Browser.JsonAsync(live)).GetProperty("conversations").EnumerateArray()];
        Assert.Equal([assigned.ToString("D"), second.ToString("D"), third.ToString("D")], conversations.Select(each => each.GetProperty("id").GetString()));
        JsonElement first = conversations.Single(each => each.GetProperty("id").GetString() == assigned.ToString("D"));
        Assert.Equal(("open", "amal", JsonValueKind.Null, 0), (first.GetProperty("status").GetString(), first.GetProperty("assignee").GetString(), first.GetProperty("place").ValueKind, first.GetProperty("waited").GetInt32()));
        Assert.Equal(Places, conversations.Where(each => each.GetProperty("assignee").ValueKind == JsonValueKind.Null).Select(each => each.GetProperty("place").GetInt32()));

        // Its staff, a stranger and anyone on a topic nobody supervises are answered as for nothing there.
        foreach ((string person, string topic) in new[] { ("amal", "billing"), ("stranger", "billing"), ("sara", "other") })
        {
            await AssertRefusedAsync(chat, $"/support/live?topic={topic}", person, HttpStatusCode.NotFound, "messaging.conversation.notfound");
            await AssertRefusedAsync(chat, $"/support/reports?topic={topic}&from=2026-11-01&to=2026-11-01", person, HttpStatusCode.NotFound, "messaging.conversation.notfound");
        }

        await AssertRefusedAsync(chat, "/support/live?topic=unknown", "sara", HttpStatusCode.BadRequest, "messaging.request.malformed", "topic");
    }

    [Fact]
    public async Task MSG_CONV_005_AC7_ASupervisorReadsNotesButNeitherWritesNorListsTheTopicsConversations()
    {
        await using ChatHost chat = await StartAsync();
        await using HubConnection amal = await SupportTests.AvailableAsync(chat, "amal");
        Guid conversation = await (await SupportTests.AcknowledgedAsync(chat, "192.0.2.1")).StartAsync();
        Assert.True((await chat.CallAsync(chats => chats.WriteAsync("amal", new ConversationId(conversation), "For staff only", MessageType.Note, null, null, default))).IsSuccess);

        Result<MessagePage> read = await chat.CallAsync(chats => chats.MessagesAsync("sara", new ConversationId(conversation), null, null, null, default));
        Assert.Contains(read.Value!.Messages, message => message.Type == MessageType.Note && message.Text == "For staff only");

        Result<MessageRecord> written = await chat.CallAsync(chats => chats.WriteAsync("sara", new ConversationId(conversation), "Hi", MessageType.Text, null, null, default));
        Assert.Equal("messaging.conversation.notfound", written.Failure!.Code);
        Result tagged = await chat.CallAsync(chats => chats.SnoozeAsync("sara", new ConversationId(conversation), DateTimeOffset.UtcNow.AddHours(1), default));
        Assert.Equal("messaging.conversation.notfound", tagged.Failure!.Code);
        Result closed = await chat.CallAsync(chats => chats.CloseAsync("sara", new ConversationId(conversation), default));
        Assert.Equal("messaging.conversation.notfound", closed.Failure!.Code);
        Assert.DoesNotContain((await chat.CallAsync(chats => chats.ListAsync("sara", null, null, default))).Value!.Conversations, each => each.Conversation.Value == conversation);
    }

    [Fact]
    public async Task Push_ANoteOfTheTopic_ReachesItsConnectedSupervisor()
    {
        await using ChatHost chat = await StartAsync();
        await using HubConnection amal = await SupportTests.AvailableAsync(chat, "amal");
        await using HubConnection sara = await chat.ConnectAsync("sara");
        HubInbox supervised = HubInbox.Of(sara);
        Guid conversation = await (await SupportTests.AcknowledgedAsync(chat, "192.0.2.1")).StartAsync();

        Assert.True((await chat.CallAsync(chats => chats.WriteAsync("amal", new ConversationId(conversation), "For staff only", MessageType.Note, null, null, default))).IsSuccess);

        JsonElement[] note = await supervised.NextAsync("message", arguments => arguments[1].GetProperty("type").GetString() == "note");
        Assert.Equal(conversation.ToString("D"), note[0].GetString());
        Assert.DoesNotContain(supervised.All, push => push.Method == "conversation");
    }

    [Fact]
    public async Task MSG_CONV_005_AC8_ReportsGiveTheMediansThe90thPercentilesAndTheSurveysPerStaffMemberAndTopic()
    {
        await using ChatHost chat = await StartAsync();
        DateTimeOffset queued = DateTimeOffset.UtcNow.AddHours(-3);
        await InsertClosedAsync(chat, queued, "amal", assigned: 60, closed: 600, resolved: true, rating: 5);
        await InsertClosedAsync(chat, queued, "amal", assigned: 120, closed: 1200, resolved: false, rating: 3);
        await InsertClosedAsync(chat, queued, "omar", assigned: 30, closed: 300, resolved: null, rating: null);
        await InsertClosedAsync(chat, queued, null, assigned: null, closed: 100, resolved: true, rating: 4);
        await InsertClosedAsync(chat, queued.AddDays(-100), "amal", assigned: 10, closed: 20, resolved: true, rating: 1);
        DateOnly today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin")).DateTime);

        using HttpResponseMessage answered = await chat.SendAsync(HttpMethod.Get, $"/support/reports?topic=billing&from={Day(today.AddDays(-1))}&to={Day(today)}", "sara");
        Assert.Equal(HttpStatusCode.OK, answered.StatusCode);
        JsonElement report = await Browser.JsonAsync(answered);

        AssertFigures(report.GetProperty("topic"), null, 4, (60, 108), (540, 972), 3, 0.6667m, 4m);
        JsonElement[] staff = [.. report.GetProperty("staff").EnumerateArray()];
        Assert.Equal(2, staff.Length);
        AssertFigures(staff[0], "amal", 2, (90, 114), (810, 1026), 2, 0.5m, 4m);
        AssertFigures(staff[1], "omar", 1, (30, 30), (270, 270), 0, null, null);
    }

    [Theory]
    [InlineData("from=2026-11-01&to=2027-02-01", "to")]
    [InlineData("from=2026-11-02&to=2026-11-01", "to")]
    [InlineData("from=1%20November&to=2026-11-01", "from")]
    [InlineData("from=2026-11-01", "to")]
    public async Task Reports_APeriodThatDoesNotReadOrPasses92Days_IsMalformed(string period, string member)
    {
        await using ChatHost chat = await StartAsync();

        await AssertRefusedAsync(chat, "/support/reports?topic=billing&" + period, "sara", HttpStatusCode.BadRequest, "messaging.request.malformed", member);
    }

    [Fact]
    public async Task Reports_NinetyTwoDays_AreAPeriod()
    {
        await using ChatHost chat = await StartAsync();

        using HttpResponseMessage answered = await chat.SendAsync(HttpMethod.Get, "/support/reports?topic=billing&from=2026-08-01&to=2026-10-31", "sara");

        Assert.Equal(HttpStatusCode.OK, answered.StatusCode);
        JsonElement topic = (await Browser.JsonAsync(answered)).GetProperty("topic");
        Assert.Equal((0, JsonValueKind.Null, JsonValueKind.Null), (topic.GetProperty("closed").GetInt32(), topic.GetProperty("waitMedian").ValueKind, topic.GetProperty("person").ValueKind));
    }

    private Task<ChatHost> StartAsync()
    {
        return StartSupervisedAsync(postgres);
    }

    /// <summary>Starts a host whose topic <c>billing</c> is supervised by <c>team-leads</c>, whose one member is <c>sara</c>.</summary>
    internal static async Task<ChatHost> StartSupervisedAsync(PostgresFixture postgres, Action<Dictionary<string, string?>>? configure = null)
    {
        ChatHost chat = await SupportTests.StartSupportAsync(postgres, configuration =>
        {
            configuration["Hermes:Conversations:Topics:billing:Supervisors:Kind"] = "Group";
            configuration["Hermes:Conversations:Topics:billing:Supervisors:Reference"] = "team-leads";
            configure?.Invoke(configuration);
        });
        chat.Membership.Add(new ConversationScope { Kind = ScopeKind.Group, Reference = "team-leads" }, "sara");
        return chat;
    }

    private static string Day(DateOnly day)
    {
        return day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static void AssertFigures(JsonElement figures, string? person, int closed, (double Median, double P90) wait, (double Median, double P90) handle, int surveys, decimal? resolved, decimal? rating)
    {
        Assert.Equal(person, figures.GetProperty("person").ValueKind == JsonValueKind.Null ? null : figures.GetProperty("person").GetString());
        Assert.Equal(closed, figures.GetProperty("closed").GetInt32());
        Assert.Equal(wait, (Math.Round(figures.GetProperty("waitMedian").GetDouble(), 3), Math.Round(figures.GetProperty("waitP90").GetDouble(), 3)));
        Assert.Equal(handle, (Math.Round(figures.GetProperty("handleMedian").GetDouble(), 3), Math.Round(figures.GetProperty("handleP90").GetDouble(), 3)));
        Assert.Equal(surveys, figures.GetProperty("surveys").GetInt32());
        Assert.Equal(resolved, figures.GetProperty("resolvedShare").ValueKind == JsonValueKind.Null ? null : Math.Round(figures.GetProperty("resolvedShare").GetDecimal(), 4));
        Assert.Equal(rating, figures.GetProperty("meanRating").ValueKind == JsonValueKind.Null ? null : Math.Round(figures.GetProperty("meanRating").GetDecimal(), 4));
    }

    /// <summary>Writes a closed conversation of <c>billing</c>, queued at <paramref name="queued"/>, assigned and closed the seconds after it given.</summary>
    private static async Task InsertClosedAsync(ChatHost chat, DateTimeOffset queued, string? assignee, int? assigned, int closed, bool? resolved, int? rating)
    {
        await chat.ScalarAsync(database =>
        {
            NpgsqlCommand command = new(
                """
                INSERT INTO messaging.conversations (id, type, origin, status, created_at, topic, assignee, queued_at, assigned_at, closed_at, survey_at, survey_resolved, survey_rating, survey_person)
                VALUES (gen_random_uuid(), 'support', 'web', 'closed', @queued, 'billing', @assignee, @queued, @assigned, @closed, @survey, @resolved, @rating, @assignee)
                """,
                database);
            command.Parameters.AddWithValue("queued", queued);
            command.Parameters.AddWithValue("assignee", (object?)assignee ?? DBNull.Value);
            command.Parameters.AddWithValue("assigned", assigned is { } after ? queued.AddSeconds(after) : DBNull.Value);
            command.Parameters.AddWithValue("closed", queued.AddSeconds(closed));
            command.Parameters.AddWithValue("survey", rating is null ? DBNull.Value : queued.AddSeconds(closed + 60));
            command.Parameters.AddWithValue("resolved", (object?)resolved ?? DBNull.Value);
            command.Parameters.AddWithValue("rating", (object?)rating ?? DBNull.Value);
            return command;
        });
    }

    private static async Task<Guid> OfflineAsync(ChatHost chat, string source, string topic)
    {
        Browser visitor = await SupportTests.AcknowledgedAsync(chat, source);
        using HttpResponseMessage form = await visitor.SendAsync(HttpMethod.Post, "/support/offline", $$"""{ "topic": "{{topic}}", "email": "visitor@example.com", "text": "Call me" }""");
        Assert.Equal(HttpStatusCode.Created, form.StatusCode);
        return Guid.Parse((await Browser.JsonAsync(form)).GetProperty("id").GetString()!);
    }

    private static async Task AssertRefusedAsync(ChatHost chat, string route, string person, HttpStatusCode status, string code, string? member = null)
    {
        using HttpResponseMessage response = await chat.SendAsync(HttpMethod.Get, route, person);
        JsonElement body = await Browser.JsonAsync(response);
        Assert.Equal((status, code), (response.StatusCode, body.GetProperty("code").GetString()));
        if (member is not null)
        {
            Assert.Equal(member, body.GetProperty("details").GetProperty("member").GetString());
        }
    }
}