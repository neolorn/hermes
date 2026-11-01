using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using Hermes.Conversations.Chats;
using Hermes.Conversations.Support;
using Hermes.Core;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Npgsql;

using Xunit;

namespace Hermes.Conversations.Tests;

/// <summary>
/// Automatic replies fired and support's timers (<c>09</c> MSG-CONV-003, MSG-CONV-005): the
/// rules <c>busy</c> (<c>unavailable</c>), <c>closed</c> (<c>outsidehours</c>, the topic
/// <c>night</c>), <c>farewell</c> (<c>closing</c>), <c>patience</c> (<c>waiting</c>, after a
/// minute) and <c>welcome</c> (<c>started</c>), in English and Arabic; a conversation closing
/// after an hour's silence, warned five minutes before. A pass of the timers is run at the
/// instant a test names.
/// </summary>
[Trait("Category", "integration")]
public sealed class TimerTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly string[] Fired = ["busy", "long", "welcome"];

    [Fact]
    public async Task MSG_CONV_003_AC1_EachTriggerWritesOneAutomaticMessageInTheConversationsLanguageOnce()
    {
        await using ChatHost chat = await StartAsync();
        Browser visitor = await SupportTests.AcknowledgedAsync(chat, "192.0.2.1");
        visitor.Language = "ar-EG, en;q=0.5";

        // Nobody is available within billing's hours: the offline form's conversation fires busy, then welcome.
        Guid conversation = await OfflineAsync(visitor, "billing");
        DateTimeOffset started = await QueuedAtAsync(chat, conversation);
        Assert.Equal(["busy ar: billing 1 0 ", "welcome ar: billing 1 0 "], await AutomaticAsync(visitor, conversation));
        Assert.Equal("ar", await LanguageAsync(chat, conversation));

        // After a minute queued, patience fires once, however many passes run.
        await PassAsync(chat, started.AddMinutes(2));
        await PassAsync(chat, started.AddMinutes(3));
        Assert.Equal(["busy ar: billing 1 0 ", "welcome ar: billing 1 0 ", "patience ar: billing 1 2 "], await AutomaticAsync(visitor, conversation));

        // Five minutes before the hour's silence ends, farewell; at its end, the close, a system message without text.
        await PassAsync(chat, started.AddMinutes(56));
        await PassAsync(chat, started.AddMinutes(57));
        Assert.Equal("farewell ar: billing 1 56 ", (await AutomaticAsync(visitor, conversation))[^1]);
        await PassAsync(chat, started.AddMinutes(61));
        Assert.Equal("closed", await StatusAsync(chat, conversation));
        JsonElement closing = (await MessagesAsync(visitor, conversation))[0];
        Assert.Equal(("system", "library", JsonValueKind.Null), (closing.GetProperty("type").GetString(), closing.GetProperty("authorKind").GetString(), closing.GetProperty("text").ValueKind));
        Assert.Equal(4, (await AutomaticAsync(visitor, conversation)).Length);

        // Outside its hours, night's form fires closed and welcome, in English for an English visitor.
        Browser other = await SupportTests.AcknowledgedAsync(chat, "192.0.2.2");
        Guid night = await OfflineAsync(other, "night");
        Assert.Equal(["closed en: night 1 0 ", "welcome en: night 1 0 "], await AutomaticAsync(other, night));
    }

    [Fact]
    public async Task MSG_CONV_003_AC1_ATransferFiresNoStartAndAnAssignedConversationNoWait()
    {
        await using ChatHost chat = await StartAsync();
        await using HubConnection amal = await SupportTests.AvailableAsync(chat, "amal");
        Browser visitor = await SupportTests.AcknowledgedAsync(chat, "192.0.2.1");
        Guid conversation = await visitor.StartAsync();
        DateTimeOffset started = await QueuedAtAsync(chat, conversation);
        Assert.Equal(["welcome en: billing  0 "], await AutomaticAsync(visitor, conversation));

        Assert.True((await chat.CallAsync(chats => chats.TransferAsync("amal", new ConversationId(conversation), null, "other", default))).IsSuccess);
        await PassAsync(chat, started.AddMinutes(2));

        Assert.Single(await AutomaticAsync(visitor, conversation));
    }

    [Fact]
    public async Task MSG_CONV_003_AC1_AReplyRenderedPastItsBoundWritesNothingAndCountsAsFired()
    {
        await using ChatHost chat = await StartAsync(configuration => configuration["Hermes:Conversations:Replies:long:Trigger"] = "started");
        Browser visitor = await SupportTests.AcknowledgedAsync(chat, "192.0.2.1");

        Guid conversation = await OfflineAsync(visitor, "billing");

        Assert.Equal(["busy en: billing 1 0 ", "welcome en: billing 1 0 "], await AutomaticAsync(visitor, conversation));
        Assert.Equal(Fired, (string[])(await chat.ScalarAsync(database => With(new NpgsqlCommand("SELECT replies_fired FROM messaging.conversations WHERE id = @id", database), conversation)))!);
    }

    [Fact]
    public async Task MSG_CONV_005_AC5_ATextAfterTheWarningRestartsTheSilenceAndANoteDoesNot()
    {
        await using ChatHost chat = await StartAsync();
        await using HubConnection amal = await SupportTests.AvailableAsync(chat, "amal");
        Browser visitor = await SupportTests.AcknowledgedAsync(chat, "192.0.2.1");
        Guid conversation = await visitor.StartAsync();
        DateTimeOffset first = await CloseAtAsync(chat, conversation);

        await PassAsync(chat, first.AddMinutes(-4));
        Assert.Equal("farewell en: billing  56 ", (await AutomaticAsync(visitor, conversation))[^1]);

        Assert.True((await chat.CallAsync(chats => chats.WriteAsync("amal", new ConversationId(conversation), "A note", MessageType.Note, null, [], default))).IsSuccess);
        Assert.Equal(first, await CloseAtAsync(chat, conversation));

        Assert.True((await chat.CallAsync(chats => chats.WriteAsync("amal", new ConversationId(conversation), "Still here", MessageType.Text, null, [], default))).IsSuccess);
        DateTimeOffset second = await CloseAtAsync(chat, conversation);
        Assert.True(second > first);

        await PassAsync(chat, first);
        Assert.Equal("open", await StatusAsync(chat, conversation));

        // The warning fired once already, so the restarted silence ends without another.
        await PassAsync(chat, second);
        Assert.Equal("closed", await StatusAsync(chat, conversation));
        Assert.Equal(2, (await AutomaticAsync(visitor, conversation)).Length);
    }

    [Fact]
    public async Task MSG_CONV_005_AC4_ASnoozedConversationWakesAtItsInstant()
    {
        await using ChatHost chat = await StartAsync();
        await using HubConnection amal = await SupportTests.AvailableAsync(chat, "amal");
        Guid conversation = await (await SupportTests.AcknowledgedAsync(chat, "192.0.2.1")).StartAsync();
        DateTimeOffset until = DateTimeOffset.UtcNow.AddMinutes(10);
        Assert.True((await chat.CallAsync(chats => chats.SnoozeAsync("amal", new ConversationId(conversation), until, default))).IsSuccess);

        await PassAsync(chat, until.AddSeconds(-1));
        Assert.Equal("snoozed", await StatusAsync(chat, conversation));

        await PassAsync(chat, until);
        Assert.Equal("open", await StatusAsync(chat, conversation));
    }

    [Fact]
    public async Task MSG_CONV_005_AC6_ARestartFiresNoTimerTwiceAndMissesNoneAndNothingFiresOnAClosedConversation()
    {
        await using ChatHost first = await StartAsync();
        Browser visitor = await SupportTests.AcknowledgedAsync(first, "192.0.2.1");
        Browser leaving = await SupportTests.AcknowledgedAsync(first, "192.0.2.2");
        DateTimeOffset started = DateTimeOffset.UtcNow;
        Guid conversation = await OfflineAsync(visitor, "billing");
        Guid closed = await OfflineAsync(leaving, "billing");
        using HttpResponseMessage left = await leaving.SendAsync(HttpMethod.Post, $"/support/conversations/{closed:D}/close", "{ }");
        Assert.Equal(HttpStatusCode.NoContent, left.StatusCode);
        await PassAsync(first, started.AddMinutes(2));
        Assert.Equal(3, (await AutomaticAsync(visitor, conversation)).Length);

        await using ChatHost second = await SupportTests.StartSupportAsync(postgres, Rules, shared: first);
        await PassAsync(second, started.AddMinutes(2));
        Assert.Equal(3, (await AutomaticAsync(visitor, conversation)).Length);

        await PassAsync(second, started.AddMinutes(58));
        Assert.Equal(4, (await AutomaticAsync(visitor, conversation)).Length);

        await PassAsync(second, started.AddHours(2));
        Assert.Equal(2, (await AutomaticAsync(leaving, closed)).Length);
        Assert.Equal(0L, await second.ScalarAsync(database => With(new NpgsqlCommand("SELECT count(*) FROM messaging.messages WHERE conversation_id = @id AND type = 'system'", database), closed)));
    }

    [Fact]
    public async Task RunAsync_TwiceWithinItsInterval_TakesTheClaimOnce()
    {
        await using ChatHost chat = await StartAsync();
        ConversationTimersJob job = chat.Application.Services.GetServices<IHostedService>().OfType<ConversationTimersJob>().Single();

        Assert.True(await job.RunAsync(TestContext.Current.CancellationToken));
        Assert.False(await job.RunAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>The rules of the class, with an hour's silence and a five minutes' warning.</summary>
    internal static void Rules(Dictionary<string, string?> configuration)
    {
        configuration["Hermes:Languages:1"] = "ar";
        configuration["Hermes:Conversations:Replies:welcome:Trigger"] = "started";
        configuration["Hermes:Conversations:Replies:busy:Trigger"] = "unavailable";
        configuration["Hermes:Conversations:Replies:closed:Trigger"] = "outsidehours";
        configuration["Hermes:Conversations:Replies:closed:Topics:0"] = "night";
        configuration["Hermes:Conversations:Replies:patience:Trigger"] = "waiting";
        configuration["Hermes:Conversations:Replies:patience:After"] = "00:01:00";
        configuration["Hermes:Conversations:Replies:farewell:Trigger"] = "closing";
        configuration["Hermes:Conversations:Support:CloseAfter"] = "01:00:00";
        configuration["Hermes:Conversations:Support:CloseWarning"] = "00:05:00";
    }

    /// <summary>Runs one pass of the timers at <paramref name="now"/>.</summary>
    internal static async Task PassAsync(ChatHost chat, DateTimeOffset now)
    {
        AsyncServiceScope scope = chat.Application.Services.CreateAsyncScope();
        await using (scope)
        {
            await scope.ServiceProvider.GetRequiredService<StaffChats>().TimersAsync(now, _ => Task.FromResult(true), TestContext.Current.CancellationToken);
        }
    }

    private Task<ChatHost> StartAsync(Action<Dictionary<string, string?>>? configure = null)
    {
        return SupportTests.StartSupportAsync(postgres, configuration =>
        {
            Rules(configuration);
            configure?.Invoke(configuration);
        });
    }

    private static async Task<Guid> OfflineAsync(Browser visitor, string topic)
    {
        using HttpResponseMessage form = await visitor.SendAsync(HttpMethod.Post, "/support/offline", $$"""{ "topic": "{{topic}}", "email": "visitor@example.com", "text": "Call me" }""");
        Assert.Equal(HttpStatusCode.Created, form.StatusCode);
        return Guid.Parse((await Browser.JsonAsync(form)).GetProperty("id").GetString()!);
    }

    /// <summary>The conversation's messages as its visitor reads them, newest first.</summary>
    private static async Task<JsonElement[]> MessagesAsync(Browser visitor, Guid conversation)
    {
        using HttpResponseMessage read = await visitor.SendAsync(HttpMethod.Get, $"/support/conversations/{conversation:D}/messages");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        return [.. (await Browser.JsonAsync(read)).GetProperty("messages").EnumerateArray()];
    }

    /// <summary>The texts of the conversation's <c>automatic</c> messages, oldest first, each written by the library.</summary>
    private static async Task<string[]> AutomaticAsync(Browser visitor, Guid conversation)
    {
        JsonElement[] automatic = [.. (await MessagesAsync(visitor, conversation)).Where(message => message.GetProperty("type").GetString() == "automatic").Reverse()];
        Assert.All(automatic, message => Assert.Equal("library", message.GetProperty("authorKind").GetString()));
        return [.. automatic.Select(message => message.GetProperty("text").GetString()!)];
    }

    private static async Task<DateTimeOffset> QueuedAtAsync(ChatHost chat, Guid conversation)
    {
        return new DateTimeOffset((DateTime)(await chat.ScalarAsync(database => With(new NpgsqlCommand("SELECT queued_at FROM messaging.conversations WHERE id = @id", database), conversation)))!);
    }

    private static async Task<DateTimeOffset> CloseAtAsync(ChatHost chat, Guid conversation)
    {
        return new DateTimeOffset((DateTime)(await chat.ScalarAsync(database => With(new NpgsqlCommand("SELECT close_at FROM messaging.conversations WHERE id = @id", database), conversation)))!);
    }

    private static async Task<string?> StatusAsync(ChatHost chat, Guid conversation)
    {
        return await chat.ScalarAsync(database => With(new NpgsqlCommand("SELECT status FROM messaging.conversations WHERE id = @id", database), conversation)) as string;
    }

    private static async Task<string?> LanguageAsync(ChatHost chat, Guid conversation)
    {
        return await chat.ScalarAsync(database => With(new NpgsqlCommand("SELECT language FROM messaging.conversations WHERE id = @id", database), conversation)) as string;
    }

    private static NpgsqlCommand With(NpgsqlCommand command, Guid id)
    {
        command.Parameters.AddWithValue("id", id);
        return command;
    }
}