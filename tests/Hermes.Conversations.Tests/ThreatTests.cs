using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Xunit;

namespace Hermes.Conversations.Tests;

/// <summary>The conversations' threats (<c>10</c> MSG-THR-001, M-038): what the logs of every logger hold while conversations are used.</summary>
[Trait("Category", "integration")]
public sealed class ThreatTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Words = "WORDS-5D21-private";

    private const string Bytes = "BYTES-7E44-private";

    [Fact]
    public async Task MSG_THR_001_AC3_NoLogLineHoldsMessageTextOrAttachmentBytes()
    {
        using LogCapture capture = new(everyCategory: true);
        StoreFake store = new();
        ScannerFake scanner = new() { Answer = ScanResult.Failed };
        await using ChatHost chat = await SupportTests.StartSupportAsync(
            postgres,
            configuration =>
            {
                configuration["Hermes:Conversations:Attachments:Enabled"] = "true";
                configuration["Hermes:Conversations:Attachments:Public"] = "true";
                configuration["Hermes:Conversations:Attachments:Types:0"] = "text/plain";
            },
            services: services =>
            {
                services.AddSingleton<ILoggerProvider>(capture);
                services.Configure<LoggerFilterOptions>(filter => filter.MinLevel = LogLevel.Trace);
                services.AddSingleton<IAttachmentStore>(store);
                services.AddSingleton<IAttachmentScanner>(scanner);
            });
        await using HubConnection amal = await SupportTests.AvailableAsync(chat, "amal");

        // A customer's conversation, its messages, a file, its scans failing and then passing, its download, its export and its end.
        Browser carla = new(chat, person: "carla");
        Guid conversation = await carla.StartAsync(text: Words + " first");
        byte[] file = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(Bytes + " ", 9_000)));
        using HttpResponseMessage uploaded = await carla.SendAsync(HttpMethod.Post, $"/support/conversations/{conversation:D}/attachments", () => AttachmentTests.File(file, "notes.txt", "text/plain"));
        string attachment = (await Browser.JsonAsync(uploaded)).GetProperty("id").GetString()!;
        using HttpResponseMessage written = await carla.SendAsync(HttpMethod.Post, $"/support/conversations/{conversation:D}/messages", $$"""{ "text": "{{Words}} second", "attachments": ["{{attachment}}"] }""");
        Assert.Equal(HttpStatusCode.Created, written.StatusCode);
        Assert.True((await chat.CallAsync(chats => chats.WriteAsync("amal", new ConversationId(conversation), Words + " note", MessageType.Note, null, null, null, default))).IsSuccess);
        await AttachmentTests.PassAsync(chat, DateTimeOffset.UtcNow.AddSeconds(1));
        scanner.Answer = ScanResult.Clean;
        await AttachmentTests.PassAsync(chat, DateTimeOffset.UtcNow.AddMinutes(2));
        using HttpResponseMessage read = await carla.SendAsync(HttpMethod.Get, $"/support/conversations/{conversation:D}/attachments/{attachment}");
        Assert.Equal(file, await read.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        using HttpResponseMessage page = await carla.SendAsync(HttpMethod.Get, $"/support/conversations/{conversation:D}/messages");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.True((await chat.SubjectsAsync(subjects => subjects.ExportAsync("carla", default))).IsSuccess);
        Assert.True((await chat.CallAsync(chats => chats.CloseAsync("amal", new ConversationId(conversation), default))).IsSuccess);
        using HttpResponseMessage transcript = await carla.SendAsync(HttpMethod.Get, $"/support/conversations/{conversation:D}/transcript");
        Assert.Equal(HttpStatusCode.OK, transcript.StatusCode);

        // A file refused for its bytes, and a staff chat's messages.
        using HttpResponseMessage refused = await carla.SendAsync(HttpMethod.Post, $"/support/conversations/{conversation:D}/attachments", () => AttachmentTests.File(file, "notes.png", "image/png"));
        Guid direct = (await chat.CallAsync(chats => chats.StartDirectAsync("amal", ["omar"], default))).Value!.Conversation.Value;
        Assert.True((await chat.CallAsync(chats => chats.WriteAsync("amal", new ConversationId(direct), Words + " direct", MessageType.Text, null, ["omar"], null, default))).IsSuccess);

        Assert.NotEmpty(capture.Entries);
        Assert.DoesNotContain(capture.Entries, entry => entry.Message.Contains(Words, StringComparison.Ordinal) || entry.Message.Contains(Bytes, StringComparison.Ordinal));
    }
}