using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Hermes.Conversations.Attachments;
using Hermes.Core;
using Hermes.Operations;
using Hermes.Testing;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Xunit;

namespace Hermes.Conversations.Tests;

/// <summary>
/// Attachments (<c>09</c> MSG-CONV-007, MSG-CONV-009; <c>05</c> MSG-API-009, MSG-API-010):
/// uploaded on their own, named by their writer's message, scanned by the
/// <c>attachment-scans</c> job before anyone opens them, streamed after the access check, and
/// kept in the store only as protected chunks. The store and the scanner are fakes the test
/// holds, and a pass of the job runs at the instant a test names.
/// </summary>
[Trait("Category", "integration")]
public sealed class AttachmentTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private readonly StoreFake store = new();

    private readonly ScannerFake scanner = new();

    [Fact]
    public async Task MSG_CONV_007_AC1_NothingOpensBeforeACleanScan()
    {
        await using ChatHost chat = await StartAsync();
        Guid conversation = await DirectAsync(chat);
        byte[] png = Png(5_000);

        using HttpResponseMessage uploaded = await chat.SendAsync(HttpMethod.Post, $"/conversations/{conversation:D}/attachments", "amal", () => File(png, "photo.png", "image/png"));
        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        JsonElement body = await Browser.JsonAsync(uploaded);
        Assert.Equal(("photo.png", "image/png", 5_000, "pending"), (body.GetProperty("name").GetString(), body.GetProperty("mediaType").GetString(), body.GetProperty("size").GetInt64(), body.GetProperty("status").GetString()));
        string attachment = body.GetProperty("id").GetString()!;

        using HttpResponseMessage written = await chat.SendAsync(HttpMethod.Post, $"/conversations/{conversation:D}/messages", "amal", $$"""{ "text": "The photo", "attachments": ["{{attachment}}"] }""");
        Assert.Equal(HttpStatusCode.Created, written.StatusCode);
        JsonElement listed = (await Browser.JsonAsync(written)).GetProperty("attachments").EnumerateArray().Single();
        Assert.Equal((attachment, "photo.png", "pending"), (listed.GetProperty("id").GetString(), listed.GetProperty("name").GetString(), listed.GetProperty("status").GetString()));

        string route = $"/conversations/{conversation:D}/attachments/{attachment}";
        using HttpResponseMessage pending = await chat.SendAsync(HttpMethod.Get, route, "omar");
        Assert.Equal((HttpStatusCode.Conflict, "messaging.attachment.pending"), (pending.StatusCode, await CodeAsync(pending)));
        Assert.Empty(scanner.Scanned);

        await PassAsync(chat, DateTimeOffset.UtcNow.AddSeconds(1));
        Assert.Equal(png, Assert.Single(scanner.Scanned));
        using HttpResponseMessage clean = await chat.SendAsync(HttpMethod.Get, route, "omar");
        Assert.Equal(HttpStatusCode.OK, clean.StatusCode);
        Assert.Equal(png, await clean.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal("clean", (await StaffMessagesAsync(chat, conversation, "omar"))[0].GetProperty("attachments")[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task MSG_CONV_007_AC1_AnInfectedFileIsDeletedFromTheStoreAndRefusedItsRowKept()
    {
        await using ChatHost chat = await StartAsync();
        scanner.Answer = ScanResult.Infected;
        Guid conversation = await DirectAsync(chat);
        Guid attachment = await UploadAsync(chat, conversation, "amal", Png(3_000));
        await WriteAsync(chat, conversation, "amal", attachment);
        Assert.Single(store.Objects);

        await PassAsync(chat, DateTimeOffset.UtcNow.AddSeconds(1));

        Assert.Empty(store.Objects);
        using HttpResponseMessage refused = await chat.SendAsync(HttpMethod.Get, $"/conversations/{conversation:D}/attachments/{attachment:D}", "omar");
        Assert.Equal((HttpStatusCode.Conflict, "messaging.attachment.refused"), (refused.StatusCode, await CodeAsync(refused)));
        Assert.Equal("infected", (await StaffMessagesAsync(chat, conversation, "omar"))[0].GetProperty("attachments")[0].GetProperty("status").GetString());
        Assert.Equal(1L, await CountAsync(chat, database => new NpgsqlCommand("SELECT count(*) FROM messaging.attachments WHERE store_key IS NULL", database)));
    }

    [Fact]
    public async Task MSG_CONV_007_AC1_AFifthFailedScanMarksItFailedAndRaisesTheAlertWhichALaterCleanScanClears()
    {
        await using ChatHost chat = await StartAsync();
        scanner.Answer = ScanResult.Failed;
        Guid conversation = await DirectAsync(chat);
        Guid attachment = await UploadAsync(chat, conversation, "amal", Png(2_000));
        await WriteAsync(chat, conversation, "amal", attachment);
        DateTimeOffset start = DateTimeOffset.UtcNow.AddSeconds(1);

        // Each failure waits longer before the next try: a pass before it is due scans nothing.
        await PassAsync(chat, start);
        await PassAsync(chat, start.AddSeconds(30));
        Assert.Single(scanner.Scanned);
        foreach (int minutes in new[] { 2, 5, 10 })
        {
            await PassAsync(chat, start.AddMinutes(minutes));
        }

        Assert.Equal(4, scanner.Scanned.Count);
        Assert.Equal("pending", await StatusAsync(chat, attachment));
        Assert.Null(await HoldingAsync(chat));

        await PassAsync(chat, start.AddMinutes(20));
        Assert.Equal(5, scanner.Scanned.Count);
        Assert.Equal("failed", await StatusAsync(chat, attachment));
        Assert.True(await HoldingAsync(chat));
        using HttpResponseMessage refused = await chat.SendAsync(HttpMethod.Get, $"/conversations/{conversation:D}/attachments/{attachment:D}", "amal");
        Assert.Equal((HttpStatusCode.Conflict, "messaging.attachment.refused"), (refused.StatusCode, await CodeAsync(refused)));

        scanner.Answer = ScanResult.Clean;
        await UploadAsync(chat, conversation, "amal", Png(2_000));
        await PassAsync(chat, start.AddMinutes(30));
        Assert.False(await HoldingAsync(chat));
    }

    [Fact]
    public async Task MSG_CONV_007_AC2_ARefusedTypeOrBytesThatDenyTheirTypeAreRefusedAndAFileOverMaxBytesAnswers413()
    {
        await using ChatHost chat = await StartAsync(configuration => configuration["Hermes:Conversations:Attachments:MaxBytes"] = "102400");
        Guid conversation = await DirectAsync(chat);
        string route = $"/conversations/{conversation:D}/attachments";

        using HttpResponseMessage text = await chat.SendAsync(HttpMethod.Post, route, "amal", () => File("plain words"u8.ToArray(), "notes.txt", "text/plain"));
        Assert.Equal((HttpStatusCode.BadRequest, "messaging.attachment.type"), (text.StatusCode, await CodeAsync(text)));

        using HttpResponseMessage denied = await chat.SendAsync(HttpMethod.Post, route, "amal", () => File(Pdf(2_000), "photo.png", "image/png"));
        Assert.Equal((HttpStatusCode.BadRequest, "messaging.attachment.type"), (denied.StatusCode, await CodeAsync(denied)));

        using HttpResponseMessage webp = await chat.SendAsync(HttpMethod.Post, route, "amal", () => File(Png(2_000), "photo.webp", "image/webp"));
        Assert.Equal((HttpStatusCode.BadRequest, "messaging.attachment.type"), (webp.StatusCode, await CodeAsync(webp)));

        // Over the bound within the first chunk, and past it later in the stream.
        foreach (int size in new[] { 102_401, 300_000 })
        {
            using HttpResponseMessage large = await chat.SendAsync(HttpMethod.Post, route, "amal", () => File(Pdf(size), "report.pdf", "application/pdf"));
            Assert.Equal((HttpStatusCode.RequestEntityTooLarge, "messaging.attachment.toolarge"), (large.StatusCode, await CodeAsync(large)));
        }

        using HttpResponseMessage exact = await chat.SendAsync(HttpMethod.Post, route, "amal", () => File(Pdf(102_400), "report.pdf", "application/pdf; charset=binary"));
        Assert.Equal(HttpStatusCode.Created, exact.StatusCode);
        Assert.Equal("application/pdf", (await Browser.JsonAsync(exact)).GetProperty("mediaType").GetString());
        Assert.Single(store.Objects);
        Assert.Equal(0L, await CountAsync(chat, database => new NpgsqlCommand("SELECT count(*) FROM messaging.attachments WHERE conversation_id IS NULL", database)));
    }

    [Fact]
    public async Task MSG_CONV_007_AFileNameOutsideItsRuleOrABodyThatIsNotOneFileIsInvalid()
    {
        await using ChatHost chat = await StartAsync();
        Guid conversation = await DirectAsync(chat);
        string route = $"/conversations/{conversation:D}/attachments";

        foreach (string name in new[] { "../photo.png", "dir\\photo.png", "photo\u0001.png", new string('a', 256) })
        {
            using HttpResponseMessage named = await chat.SendAsync(HttpMethod.Post, route, "amal", () => File(Png(1_000), name, "image/png"));
            Assert.Equal((HttpStatusCode.BadRequest, "messaging.attachment.invalid", "name"), (named.StatusCode, await CodeAsync(named), await FieldAsync(named)));
        }

        using HttpResponseMessage json = await chat.SendAsync(HttpMethod.Post, route, "amal", """{ "file": "photo.png" }""");
        Assert.Equal((HttpStatusCode.BadRequest, "messaging.attachment.invalid", "file"), (json.StatusCode, await CodeAsync(json), await FieldAsync(json)));

        using HttpResponseMessage notFile = await chat.SendAsync(HttpMethod.Post, route, "amal", () => Multipart(("note", null, "text/plain", "not a file"u8.ToArray())));
        Assert.Equal((HttpStatusCode.BadRequest, "messaging.attachment.invalid", "file"), (notFile.StatusCode, await CodeAsync(notFile), await FieldAsync(notFile)));

        // A second file after the first forgets the first, whose bytes the job then deletes.
        using HttpResponseMessage both = await chat.SendAsync(HttpMethod.Post, route, "amal", () => Multipart(("file", "one.png", "image/png", Png(1_000)), ("file", "two.png", "image/png", Png(1_000))));
        Assert.Equal((HttpStatusCode.BadRequest, "messaging.attachment.invalid", "file"), (both.StatusCode, await CodeAsync(both), await FieldAsync(both)));
        await PassAsync(chat, DateTimeOffset.UtcNow.AddSeconds(1));
        Assert.Empty(store.Objects);
        Assert.Equal(0L, await CountAsync(chat, database => new NpgsqlCommand("SELECT count(*) FROM messaging.attachments", database)));
    }

    [Fact]
    public async Task MSG_CONV_007_AttachmentsDisabledOrThePublicGroupsAnswerAsAConversationUnknown()
    {
        await using ChatHost chat = await SupportTests.StartSupportAsync(postgres);
        Guid conversation = await DirectAsync(chat);
        using HttpResponseMessage upload = await chat.SendAsync(HttpMethod.Post, $"/conversations/{conversation:D}/attachments", "amal", () => File(Png(1_000), "photo.png", "image/png"));
        Assert.Equal((HttpStatusCode.NotFound, "messaging.conversation.notfound"), (upload.StatusCode, await CodeAsync(upload)));
        using HttpResponseMessage named = await chat.SendAsync(HttpMethod.Post, $"/conversations/{conversation:D}/messages", "amal", $$"""{ "text": "Here", "attachments": ["{{Guid.NewGuid():D}}"] }""");
        Assert.Equal((HttpStatusCode.BadRequest, "messaging.message.invalid", "attachments"), (named.StatusCode, await CodeAsync(named), await FieldAsync(named)));

        await using ChatHost staffOnly = await StartAsync(configuration => configuration["Hermes:Conversations:Attachments:Public"] = "false");
        await using HubConnection amal = await SupportTests.AvailableAsync(staffOnly, "amal");
        Browser visitor = await SupportTests.AcknowledgedAsync(staffOnly, "192.0.2.1");
        Guid support = await visitor.StartAsync();
        using HttpResponseMessage publicUpload = await visitor.SendAsync(HttpMethod.Post, $"/support/conversations/{support:D}/attachments", () => File(Png(1_000), "photo.png", "image/png"));
        Assert.Equal((HttpStatusCode.NotFound, "messaging.conversation.notfound"), (publicUpload.StatusCode, await CodeAsync(publicUpload)));
        using HttpResponseMessage publicDownload = await visitor.SendAsync(HttpMethod.Get, $"/support/conversations/{support:D}/attachments/{Guid.NewGuid():D}");
        Assert.Equal((HttpStatusCode.NotFound, "messaging.conversation.notfound"), (publicDownload.StatusCode, await CodeAsync(publicDownload)));
    }

    [Fact]
    public async Task MSG_CONV_007_AC3_DownloadsStreamAfterTheAccessCheckWithNosniffAndAsAnAttachment()
    {
        await using ChatHost chat = await StartAsync();
        Guid conversation = await DirectAsync(chat);
        byte[] pdf = Pdf(200_000);
        Guid attachment = await UploadAsync(chat, conversation, "amal", pdf, "Résumé report.pdf", "application/pdf");
        Guid unnamed = await UploadAsync(chat, conversation, "amal", Png(1_000));
        await WriteAsync(chat, conversation, "amal", attachment);
        await PassAsync(chat, DateTimeOffset.UtcNow.AddSeconds(1));
        Assert.Equal(4L, await CountAsync(chat, database => With(new NpgsqlCommand("SELECT chunks FROM messaging.attachments WHERE id = @id", database), attachment)));

        using HttpResponseMessage read = await chat.SendAsync(HttpMethod.Get, $"/conversations/{conversation:D}/attachments/{attachment:D}", "omar");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal("application/pdf", read.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", read.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("Résumé report.pdf", read.Content.Headers.ContentDisposition.FileNameStar);
        Assert.Equal("nosniff", Assert.Single(read.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal(pdf, await read.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));

        // A person outside the conversation learns nothing of it.
        using HttpResponseMessage stranger = await chat.SendAsync(HttpMethod.Get, $"/conversations/{conversation:D}/attachments/{attachment:D}", "badr");
        Assert.Equal((HttpStatusCode.NotFound, "messaging.conversation.notfound"), (stranger.StatusCode, await CodeAsync(stranger)));

        // An attachment no message names is its uploader's alone; one unknown is not held.
        using HttpResponseMessage other = await chat.SendAsync(HttpMethod.Get, $"/conversations/{conversation:D}/attachments/{unnamed:D}", "omar");
        Assert.Equal((HttpStatusCode.NotFound, "messaging.attachment.notfound"), (other.StatusCode, await CodeAsync(other)));
        using HttpResponseMessage own = await chat.SendAsync(HttpMethod.Get, $"/conversations/{conversation:D}/attachments/{unnamed:D}", "amal");
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        using HttpResponseMessage unknown = await chat.SendAsync(HttpMethod.Get, $"/conversations/{conversation:D}/attachments/{Guid.NewGuid():D}", "amal");
        Assert.Equal((HttpStatusCode.NotFound, "messaging.attachment.notfound"), (unknown.StatusCode, await CodeAsync(unknown)));
        using HttpResponseMessage unread = await chat.SendAsync(HttpMethod.Get, $"/conversations/{conversation:D}/attachments/x", "amal");
        Assert.Equal((HttpStatusCode.NotFound, "messaging.attachment.notfound"), (unread.StatusCode, await CodeAsync(unread)));

        // Another conversation does not hold it.
        Guid elsewhere = (await chat.CallAsync(chats => chats.StartDirectAsync("amal", ["badr"], default))).Value!.Conversation.Value;
        using HttpResponseMessage moved = await chat.SendAsync(HttpMethod.Get, $"/conversations/{elsewhere:D}/attachments/{attachment:D}", "amal");
        Assert.Equal((HttpStatusCode.NotFound, "messaging.attachment.notfound"), (moved.StatusCode, await CodeAsync(moved)));
    }

    [Fact]
    public async Task MSG_CONV_007_AC3_AVisitorUploadsNamesAndOpensAttachmentsButNeverOneInANote()
    {
        await using ChatHost chat = await StartAsync();
        await using HubConnection amal = await SupportTests.AvailableAsync(chat, "amal");
        Browser visitor = await SupportTests.AcknowledgedAsync(chat, "192.0.2.1");
        Guid conversation = await visitor.StartAsync();

        Guid inNote = await UploadAsync(chat, conversation, "amal", Png(1_000), "internal.png");
        Guid inText = await UploadAsync(chat, conversation, "amal", Png(1_500), "for-you.png");
        Assert.True((await chat.CallAsync(chats => chats.WriteAsync("amal", new ConversationId(conversation), "Staff only", MessageType.Note, null, null, [new AttachmentId(inNote)], default))).IsSuccess);
        Assert.True((await chat.CallAsync(chats => chats.WriteAsync("amal", new ConversationId(conversation), "For you", MessageType.Text, null, null, [new AttachmentId(inText)], default))).IsSuccess);

        using HttpResponseMessage uploaded = await visitor.SendAsync(HttpMethod.Post, $"/support/conversations/{conversation:D}/attachments", () => File(Pdf(3_000), "invoice.pdf", "application/pdf"));
        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        string mine = (await Browser.JsonAsync(uploaded)).GetProperty("id").GetString()!;

        // A visitor names only their own: another writer's is unknown to them.
        using HttpResponseMessage theirs = await visitor.SendAsync(HttpMethod.Post, $"/support/conversations/{conversation:D}/messages", $$"""{ "text": "Mine", "attachments": ["{{inText:D}}"] }""");
        Assert.Equal((HttpStatusCode.BadRequest, "messaging.message.invalid", "attachments"), (theirs.StatusCode, await CodeAsync(theirs), await FieldAsync(theirs)));
        using HttpResponseMessage written = await visitor.SendAsync(HttpMethod.Post, $"/support/conversations/{conversation:D}/messages", $$"""{ "text": "My invoice", "attachments": ["{{mine}}"] }""");
        Assert.Equal(HttpStatusCode.Created, written.StatusCode);
        Assert.Equal("invoice.pdf", (await Browser.JsonAsync(written)).GetProperty("attachments")[0].GetProperty("name").GetString());
        await PassAsync(chat, DateTimeOffset.UtcNow.AddSeconds(1));

        using HttpResponseMessage note = await visitor.SendAsync(HttpMethod.Get, $"/support/conversations/{conversation:D}/attachments/{inNote:D}");
        Assert.Equal((HttpStatusCode.NotFound, "messaging.attachment.notfound"), (note.StatusCode, await CodeAsync(note)));
        using HttpResponseMessage text = await visitor.SendAsync(HttpMethod.Get, $"/support/conversations/{conversation:D}/attachments/{inText:D}");
        Assert.Equal(HttpStatusCode.OK, text.StatusCode);
        Assert.Equal("nosniff", Assert.Single(text.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal(Png(1_500), await text.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        using HttpResponseMessage page = await visitor.SendAsync(HttpMethod.Get, $"/support/conversations/{conversation:D}/messages");
        string[] listed = [.. (await Browser.JsonAsync(page)).GetProperty("messages").EnumerateArray().SelectMany(message => message.GetProperty("attachments").EnumerateArray()).Select(each => each.GetProperty("name").GetString()!).Order(StringComparer.Ordinal)];
        Assert.Equal(["for-you.png", "invoice.pdf"], listed);

        using HttpResponseMessage staff = await chat.SendAsync(HttpMethod.Get, $"/conversations/{conversation:D}/attachments/{mine}", "amal");
        Assert.Equal(HttpStatusCode.OK, staff.StatusCode);
        Assert.Equal(Pdf(3_000), await staff.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));

        // Another visitor reaches neither the conversation nor its attachments.
        Browser stranger = await SupportTests.AcknowledgedAsync(chat, "192.0.2.2");
        using HttpResponseMessage strange = await stranger.SendAsync(HttpMethod.Get, $"/support/conversations/{conversation:D}/attachments/{inText:D}");
        Assert.Equal((HttpStatusCode.NotFound, "messaging.conversation.notfound"), (strange.StatusCode, await CodeAsync(strange)));
    }

    [Fact]
    public async Task MSG_CONV_007_AC4_AnUnnamedUploadIsGoneAfter24Hours()
    {
        await using ChatHost chat = await StartAsync();
        Guid conversation = await DirectAsync(chat);
        DateTimeOffset uploadedAt = DateTimeOffset.UtcNow;
        Guid unnamed = await UploadAsync(chat, conversation, "amal", Png(1_000));
        Guid named = await UploadAsync(chat, conversation, "amal", Png(1_200));
        await WriteAsync(chat, conversation, "amal", named);

        await PassAsync(chat, uploadedAt.AddHours(23));
        Assert.Equal(2, store.Objects.Count);

        await PassAsync(chat, uploadedAt.AddHours(25));
        Assert.Single(store.Objects);
        Assert.Equal(0L, await CountAsync(chat, database => With(new NpgsqlCommand("SELECT count(*) FROM messaging.attachments WHERE id = @id", database), unnamed)));
        Assert.Equal(1L, await CountAsync(chat, database => With(new NpgsqlCommand("SELECT count(*) FROM messaging.attachments WHERE id = @id", database), named)));

        // Let go of, it can no longer be named.
        Result<MessageRecord> late = await chat.CallAsync(chats => chats.WriteAsync("amal", new ConversationId(conversation), "Late", MessageType.Text, null, null, [new AttachmentId(unnamed)], default));
        Assert.Equal(("messaging.message.invalid", "attachments"), (late.Failure!.Code, late.Failure.Details["field"]));
    }

    [Fact]
    public async Task MSG_CONV_007_AMessageNamesUpToTenOfItsWritersOwnAttachmentsEachOnce()
    {
        await using ChatHost chat = await StartAsync();
        Guid conversation = await DirectAsync(chat);
        List<AttachmentId> eleven = [];
        for (int index = 0; index < 11; index++)
        {
            eleven.Add(new AttachmentId(await UploadAsync(chat, conversation, "amal", Png(1_000))));
        }

        AttachmentId others = new(await UploadAsync(chat, conversation, "omar", Png(1_000)));
        IReadOnlyList<AttachmentId>[] refused =
        [
            eleven,
            [others],
            [new AttachmentId(Guid.NewGuid())],
            [eleven[0], eleven[0]],
        ];
        foreach (IReadOnlyList<AttachmentId> named in refused)
        {
            Result<MessageRecord> written = await chat.CallAsync(chats => chats.WriteAsync("amal", new ConversationId(conversation), "Files", MessageType.Text, null, null, named, default));
            Assert.Equal(("messaging.message.invalid", "attachments"), (written.Failure!.Code, written.Failure.Details["field"]));
        }

        Result<MessageRecord> ten = await chat.CallAsync(chats => chats.WriteAsync("amal", new ConversationId(conversation), "Files", MessageType.Text, null, null, eleven[..10], default));
        Assert.Equal(eleven[..10], ten.Value!.Attachments.Select(each => each.Attachment));
        Result<MessageRecord> again = await chat.CallAsync(chats => chats.WriteAsync("amal", new ConversationId(conversation), "Again", MessageType.Text, null, null, [eleven[0]], default));
        Assert.Equal(("messaging.message.invalid", "attachments"), (again.Failure!.Code, again.Failure.Details["field"]));

        using HttpResponseMessage unread = await chat.SendAsync(HttpMethod.Post, $"/conversations/{conversation:D}/messages", "amal", """{ "text": "Files", "attachments": ["not-an-id"] }""");
        Assert.Equal((HttpStatusCode.BadRequest, "messaging.message.invalid", "attachments"), (unread.StatusCode, await CodeAsync(unread), await FieldAsync(unread)));
    }

    [Fact]
    public async Task MSG_CONV_009_AC3_TheStoreHoldsOnlyProtectedChunksAndAChunkMovedToAnotherAttachmentDoesNotOpen()
    {
        await using ChatHost chat = await StartAsync(configuration => configuration["Hermes:Conversations:Attachments:Types:0"] = "text/plain");
        Guid conversation = await DirectAsync(chat);
        byte[] words = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("the words of a private file. ", 5_000)));
        Guid first = await UploadAsync(chat, conversation, "amal", words, "first.txt", "text/plain");
        Guid second = await UploadAsync(chat, conversation, "amal", words, "second.txt", "text/plain");
        await WriteAsync(chat, conversation, "amal", first, second);
        await PassAsync(chat, DateTimeOffset.UtcNow.AddSeconds(1));
        Assert.Equal(2, scanner.Scanned.Count);
        Assert.All(scanner.Scanned, scanned => Assert.Equal(words, scanned));

        foreach (byte[] stored in store.Objects.Values)
        {
            Assert.DoesNotContain("private file", Encoding.UTF8.GetString(stored), StringComparison.Ordinal);
            Assert.DoesNotContain("first.txt", Encoding.UTF8.GetString(stored), StringComparison.Ordinal);
        }

        string firstKey = await StoreKeyAsync(chat, first);
        string secondKey = await StoreKeyAsync(chat, second);
        store.Objects[secondKey] = store.Objects[firstKey];

        using HttpResponseMessage moved = await chat.SendAsync(HttpMethod.Get, $"/conversations/{conversation:D}/attachments/{second:D}", "omar");
        Assert.Equal((HttpStatusCode.NotFound, "messaging.attachment.notfound"), (moved.StatusCode, await CodeAsync(moved)));
        Assert.True((bool)(await chat.ScalarAsync(database => new NpgsqlCommand("SELECT holding FROM messaging.alerts WHERE condition = 'content-rejected' AND scope = 'attachments'", database)))!);
        using HttpResponseMessage kept = await chat.SendAsync(HttpMethod.Get, $"/conversations/{conversation:D}/attachments/{first:D}", "omar");
        Assert.Equal(words, await kept.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MSG_CONV_009_AC4_TheMessagesGoingAndErasureDeleteTheStoredBytesAStoreAwayTriedAgain()
    {
        await using ChatHost chat = await StartAsync();
        Guid conversation = await DirectAsync(chat);
        Guid amals = await UploadAsync(chat, conversation, "amal", Png(1_000));
        MessageId deleted = await WriteAsync(chat, conversation, "amal", amals);
        Guid omars = await UploadAsync(chat, conversation, "omar", Png(1_100));
        await WriteAsync(chat, conversation, "omar", omars);
        Guid kept = await UploadAsync(chat, conversation, "amal", Png(1_200));
        await WriteAsync(chat, conversation, "amal", kept);
        await PassAsync(chat, DateTimeOffset.UtcNow.AddSeconds(1));
        Assert.Equal(3, store.Objects.Count);

        Assert.True((await chat.CallAsync(chats => chats.DeleteAsync("amal", new ConversationId(conversation), deleted, default))).IsSuccess);
        Assert.True((await chat.SubjectsAsync(subjects => subjects.EraseAsync("omar", default))).IsSuccess);

        // The store is out of reach: the rows stay until a pass deletes their bytes.
        store.Away = true;
        await PassAsync(chat, DateTimeOffset.UtcNow.AddSeconds(2));
        Assert.Equal(3, store.Objects.Count);
        Assert.Equal(3L, await CountAsync(chat, database => new NpgsqlCommand("SELECT count(*) FROM messaging.attachments", database)));

        store.Away = false;
        await PassAsync(chat, DateTimeOffset.UtcNow.AddSeconds(3));
        Assert.Single(store.Objects);
        Assert.Equal(kept, Assert.Single(await IdsAsync(chat)));
    }

    [Fact]
    public async Task MSG_CONV_009_ReProtectionWritesANewObjectUnderTheWriteVersionThenTheOldOneIsDeleted()
    {
        await using ChatHost chat = await StartAsync();
        Guid conversation = await DirectAsync(chat);
        byte[] pdf = Pdf(150_000);
        Guid attachment = await UploadAsync(chat, conversation, "amal", pdf, "report.pdf", "application/pdf");
        await WriteAsync(chat, conversation, "amal", attachment);
        await PassAsync(chat, DateTimeOffset.UtcNow.AddSeconds(1));
        string oldKey = (string)(await chat.ScalarAsync(database => new NpgsqlCommand("SELECT store_key FROM messaging.attachments", database)))!;

        // A second process holding versions 1 and 2 of contentKeys, writing under the second.
        await using ChatHost rotated = await StartAsync(
            configuration => configuration["Hermes:Secrets:ContentKeys:WriteVersion"] = "2",
            shared: chat,
            services: services =>
            {
                FixedSecretSource secrets = (FixedSecretSource)services.Single(service => service.ServiceType == typeof(FixedSecretSource)).ImplementationInstance!;
                SecretVersion first = secrets.ReadAsync("contentKeys", default).AsTask().GetAwaiter().GetResult()!.Versions.Single();
                secrets.SetSecret("contentKeys", new SecretVersions { Versions = [first, new SecretVersion { Version = 2, Value = SHA256.HashData("second"u8) }] });
            });
        AttachmentProtection step = ActivatorUtilities.CreateInstance<AttachmentProtection>(rotated.Application.Services);
        Assert.True(await step.ProtectAgainAsync(2, () => Task.FromResult(true), TestContext.Current.CancellationToken));

        Assert.Equal(2L, await CountAsync(rotated, database => new NpgsqlCommand("SELECT content_key_version FROM messaging.attachments WHERE conversation_id IS NOT NULL", database)));
        string newKey = (string)(await rotated.ScalarAsync(database => new NpgsqlCommand("SELECT store_key FROM messaging.attachments WHERE conversation_id IS NOT NULL", database)))!;
        Assert.NotEqual(oldKey, newKey);
        Assert.Equal(2, store.Objects.Count);

        await PassAsync(rotated, DateTimeOffset.UtcNow.AddSeconds(2));
        Assert.Equal(newKey, Assert.Single(store.Objects.Keys));
        Assert.Equal(attachment, Assert.Single(await IdsAsync(rotated)));
        using HttpResponseMessage read = await rotated.SendAsync(HttpMethod.Get, $"/conversations/{conversation:D}/attachments/{attachment:D}", "omar");
        Assert.Equal(pdf, await read.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>A PNG's signature followed by <paramref name="size"/> bytes in all less eight of a pattern.</summary>
    internal static byte[] Png(int size)
    {
        byte[] bytes = Filler(size);
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(bytes, 0);
        return bytes;
    }

    private static byte[] Pdf(int size)
    {
        byte[] bytes = Filler(size);
        "%PDF-1.7"u8.CopyTo(bytes);
        return bytes;
    }

    private static byte[] Filler(int size)
    {
        byte[] bytes = new byte[size];
        for (int index = 0; index < size; index++)
        {
            bytes[index] = (byte)((index * 7) + (index / 251));
        }

        return bytes;
    }

    /// <summary>A multipart body of <paramref name="parts"/>, each a field and, for a file, its name, sent as <c>filename*</c>, its type and its bytes.</summary>
    internal static HttpContent Multipart(params (string Field, string? FileName, string Type, byte[] Bytes)[] parts)
    {
        const string boundary = "attachment-boundary-5d21";
        using MemoryStream body = new();
        foreach ((string field, string? fileName, string type, byte[] bytes) in parts)
        {
            string disposition = fileName is null
                ? $"form-data; name=\"{field}\""
                : $"form-data; name=\"{field}\"; filename*=UTF-8''{Uri.EscapeDataString(fileName)}";
            body.Write(Encoding.ASCII.GetBytes($"--{boundary}\r\nContent-Disposition: {disposition}\r\nContent-Type: {type}\r\n\r\n"));
            body.Write(bytes);
            body.Write("\r\n"u8);
        }

        body.Write(Encoding.ASCII.GetBytes($"--{boundary}--\r\n"));
        ByteArrayContent content = new(body.ToArray());
        content.Headers.ContentType = MediaTypeHeaderValue.Parse($"multipart/form-data; boundary={boundary}");
        return content;
    }

    /// <summary>A single-file multipart body.</summary>
    internal static HttpContent File(byte[] bytes, string name, string type)
    {
        return Multipart(("file", name, type, bytes));
    }

    private static async Task<Guid> DirectAsync(ChatHost chat)
    {
        return (await chat.CallAsync(chats => chats.StartDirectAsync("amal", ["omar"], default))).Value!.Conversation.Value;
    }

    private static async Task<Guid> UploadAsync(ChatHost chat, Guid conversation, string person, byte[] bytes, string name = "photo.png", string type = "image/png")
    {
        using MemoryStream content = new(bytes);
        Result<AttachmentRecord> uploaded = await chat.CallAsync(chats => chats.UploadAttachmentAsync(person, new ConversationId(conversation), name, type, content, default));
        Assert.True(uploaded.IsSuccess, uploaded.Failure?.Code);
        return uploaded.Value!.Attachment.Value;
    }

    private static async Task<MessageId> WriteAsync(ChatHost chat, Guid conversation, string person, params Guid[] attachments)
    {
        Result<MessageRecord> written = await chat.CallAsync(chats => chats.WriteAsync(person, new ConversationId(conversation), "Attached", MessageType.Text, null, null, [.. attachments.Select(each => new AttachmentId(each))], default));
        Assert.True(written.IsSuccess, written.Failure?.Code);
        return written.Value!.Message;
    }

    internal static async Task PassAsync(ChatHost chat, DateTimeOffset now)
    {
        AsyncServiceScope scope = chat.Application.Services.CreateAsyncScope();
        await using (scope)
        {
            await scope.ServiceProvider.GetRequiredService<AttachmentWork>().PassAsync(now, _ => Task.FromResult(true), TestContext.Current.CancellationToken);
        }
    }

    private static async Task<JsonElement[]> StaffMessagesAsync(ChatHost chat, Guid conversation, string person)
    {
        using HttpResponseMessage page = await chat.SendAsync(HttpMethod.Get, $"/conversations/{conversation:D}/messages", person);
        return [.. (await Browser.JsonAsync(page)).GetProperty("messages").EnumerateArray()];
    }

    private static async Task<string?> StatusAsync(ChatHost chat, Guid attachment)
    {
        return (string?)await chat.ScalarAsync(database => With(new NpgsqlCommand("SELECT status FROM messaging.attachments WHERE id = @id", database), attachment));
    }

    private static async Task<string> StoreKeyAsync(ChatHost chat, Guid attachment)
    {
        return (string)(await chat.ScalarAsync(database => With(new NpgsqlCommand("SELECT store_key FROM messaging.attachments WHERE id = @id", database), attachment)))!;
    }

    private static async Task<bool?> HoldingAsync(ChatHost chat)
    {
        return await chat.ScalarAsync(database => new NpgsqlCommand("SELECT holding FROM messaging.alerts WHERE condition = 'attachment-scan'", database)) as bool?;
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

    private static async Task<Guid[]> IdsAsync(ChatHost chat)
    {
        NpgsqlConnection database = new(chat.Connection);
        await using (database)
        {
            await database.OpenAsync(TestContext.Current.CancellationToken);
            await using NpgsqlCommand command = new("SELECT id FROM messaging.attachments ORDER BY id", database);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
            List<Guid> ids = [];
            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            {
                ids.Add(reader.GetGuid(0));
            }

            return [.. ids];
        }
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        return (await Browser.JsonAsync(response)).GetProperty("code").GetString();
    }

    private static async Task<string?> FieldAsync(HttpResponseMessage response)
    {
        return (await Browser.JsonAsync(response)).GetProperty("details").GetProperty("field").GetString();
    }

    /// <summary>A support host with attachments on in both groups, the store and scanner this test holds; on <paramref name="shared"/>'s database where given.</summary>
    private Task<ChatHost> StartAsync(Action<Dictionary<string, string?>>? configure = null, ChatHost? shared = null, Action<IServiceCollection>? services = null)
    {
        return SupportTests.StartSupportAsync(
            postgres,
            configuration =>
            {
                configuration["Hermes:Conversations:Attachments:Enabled"] = "true";
                configuration["Hermes:Conversations:Attachments:Public"] = "true";
                configure?.Invoke(configuration);
            },
            shared,
            more =>
            {
                more.AddSingleton<IAttachmentStore>(store);
                more.AddSingleton<IAttachmentScanner>(scanner);
                services?.Invoke(more);
            });
    }
}