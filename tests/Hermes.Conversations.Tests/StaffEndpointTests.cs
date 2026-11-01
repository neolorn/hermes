using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Hermes.Core;

using Xunit;

namespace Hermes.Conversations.Tests;

/// <summary>The staff conversations group, each route thin over its operation (<c>05</c> MSG-API-009).</summary>
[Trait("Category", "integration")]
public sealed class StaffEndpointTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task MSG_API_009_AC1_TheDirectConversationRoutesDoWhatTheirOperationsDo()
    {
        await using ChatHost chat = await ChatHost.StartAsync(postgres);

        using HttpResponseMessage anonymous = await chat.SendAsync(HttpMethod.Get, "/conversations", person: null);
        Assert.Equal((HttpStatusCode.Unauthorized, "messaging.request.unauthenticated"), (anonymous.StatusCode, await CodeAsync(anonymous)));

        using HttpResponseMessage started = await chat.SendAsync(HttpMethod.Post, "/conversations/direct", "amal", """{ "participants": ["omar"] }""");
        Assert.Equal(HttpStatusCode.Created, started.StatusCode);
        using JsonDocument conversation = await JsonAsync(started);
        string id = conversation.RootElement.GetProperty("id").GetString()!;
        Assert.Equal("direct", conversation.RootElement.GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.Null, conversation.RootElement.GetProperty("scope").ValueKind);
        Assert.Equal(["amal", "omar"], conversation.RootElement.GetProperty("participants").EnumerateArray().Select(each => each.GetProperty("person").GetString()).Order(StringComparer.Ordinal));
        Assert.Equal((0, false), (conversation.RootElement.GetProperty("unread").GetInt32(), conversation.RootElement.GetProperty("muted").GetBoolean()));
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$", conversation.RootElement.GetProperty("lastAt").GetString());

        using HttpResponseMessage invalid = await chat.SendAsync(HttpMethod.Post, "/conversations/direct", "amal", """{ "participants": ["amal"] }""");
        Assert.Equal((HttpStatusCode.BadRequest, "messaging.conversation.invalid"), (invalid.StatusCode, await CodeAsync(invalid)));

        using HttpResponseMessage written = await chat.SendAsync(HttpMethod.Post, $"/conversations/{id}/messages", "omar", """{ "text": "Hello", "mentions": ["amal"] }""");
        Assert.Equal(HttpStatusCode.Created, written.StatusCode);
        using JsonDocument message = await JsonAsync(written);
        string messageId = message.RootElement.GetProperty("id").GetString()!;
        Assert.Equal(("person", "omar", "text", "Hello", false), (message.RootElement.GetProperty("authorKind").GetString(), message.RootElement.GetProperty("author").GetString(), message.RootElement.GetProperty("type").GetString(), message.RootElement.GetProperty("text").GetString(), message.RootElement.GetProperty("erased").GetBoolean()));
        Assert.Equal(["amal"], message.RootElement.GetProperty("mentions").EnumerateArray().Select(each => each.GetString()));

        using HttpResponseMessage note = await chat.SendAsync(HttpMethod.Post, $"/conversations/{id}/messages", "omar", """{ "text": "Psst", "type": "note" }""");
        Assert.Equal(("messaging.message.invalid", "type"), await FieldAsync(note));
        using HttpResponseMessage untyped = await chat.SendAsync(HttpMethod.Post, $"/conversations/{id}/messages", "omar", """{ "type": "text" }""");
        Assert.Equal((HttpStatusCode.BadRequest, "messaging.request.malformed"), (untyped.StatusCode, await CodeAsync(untyped)));
        using HttpResponseMessage plain = await chat.SendAsync(HttpMethod.Post, $"/conversations/{id}/messages", "omar");
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, plain.StatusCode);

        using HttpResponseMessage list = await chat.SendAsync(HttpMethod.Get, "/conversations?size=1", "amal");
        using JsonDocument listed = await JsonAsync(list);
        Assert.Equal(1, listed.RootElement.GetProperty("conversations").EnumerateArray().Single().GetProperty("unread").GetInt32());
        Assert.Equal(JsonValueKind.Null, listed.RootElement.GetProperty("next").ValueKind);
        using HttpResponseMessage badSize = await chat.SendAsync(HttpMethod.Get, "/conversations?size=101", "amal");
        Assert.Equal(("messaging.request.malformed", "size"), await MemberAsync(badSize));

        using HttpResponseMessage read = await chat.SendAsync(HttpMethod.Post, $"/conversations/{id}/read", "amal", $$"""{ "messageId": "{{messageId}}" }""");
        Assert.Equal(HttpStatusCode.NoContent, read.StatusCode);
        using HttpResponseMessage readUnknown = await chat.SendAsync(HttpMethod.Post, $"/conversations/{id}/read", "amal", $$"""{ "messageId": "{{Guid.CreateVersion7():D}}" }""");
        Assert.Equal((HttpStatusCode.NotFound, "messaging.message.notfound"), (readUnknown.StatusCode, await CodeAsync(readUnknown)));
        using HttpResponseMessage muted = await chat.SendAsync(HttpMethod.Put, $"/conversations/{id}/mute", "amal", """{ "muted": true }""");
        Assert.Equal(HttpStatusCode.NoContent, muted.StatusCode);
        using HttpResponseMessage got = await chat.SendAsync(HttpMethod.Get, $"/conversations/{id}", "amal");
        using JsonDocument reread = await JsonAsync(got);
        Assert.Equal((0, true), (reread.RootElement.GetProperty("unread").GetInt32(), reread.RootElement.GetProperty("muted").GetBoolean()));

        using HttpResponseMessage notAuthor = await chat.SendAsync(HttpMethod.Put, $"/conversations/{id}/messages/{messageId}", "amal", """{ "text": "Changed" }""");
        Assert.Equal((HttpStatusCode.Forbidden, "messaging.message.notauthor"), (notAuthor.StatusCode, await CodeAsync(notAuthor)));
        using HttpResponseMessage edited = await chat.SendAsync(HttpMethod.Put, $"/conversations/{id}/messages/{messageId}", "omar", """{ "text": "Hello again" }""");
        Assert.Equal(HttpStatusCode.NoContent, edited.StatusCode);
        using HttpResponseMessage deleted = await chat.SendAsync(HttpMethod.Delete, $"/conversations/{id}/messages/{messageId}", "omar");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using HttpResponseMessage again = await chat.SendAsync(HttpMethod.Put, $"/conversations/{id}/messages/{messageId}", "omar", """{ "text": "Too late" }""");
        Assert.Equal((HttpStatusCode.Conflict, "messaging.message.deleted"), (again.StatusCode, await CodeAsync(again)));

        using HttpResponseMessage messages = await chat.SendAsync(HttpMethod.Get, $"/conversations/{id}/messages?size=10", "amal");
        using JsonDocument page = await JsonAsync(messages);
        JsonElement shown = page.RootElement.GetProperty("messages").EnumerateArray().Single();
        Assert.Equal((JsonValueKind.Null, false), (shown.GetProperty("text").ValueKind, shown.GetProperty("erased").GetBoolean()));
        Assert.NotEqual(JsonValueKind.Null, shown.GetProperty("deletedAt").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, shown.GetProperty("editedAt").ValueKind);
        using HttpResponseMessage badBefore = await chat.SendAsync(HttpMethod.Get, $"/conversations/{id}/messages?before=nope", "amal");
        Assert.Equal(("messaging.request.malformed", "before"), await MemberAsync(badBefore));

        using HttpResponseMessage added = await chat.SendAsync(HttpMethod.Post, $"/conversations/{id}/participants", "amal", """{ "participants": ["sara"] }""");
        Assert.Equal(HttpStatusCode.NoContent, added.StatusCode);
        using HttpResponseMessage crowd = await chat.SendAsync(HttpMethod.Post, $"/conversations/{id}/participants", "amal", $$"""{ "participants": [{{string.Join(", ", Enumerable.Range(1, 14).Select(each => $"\"p{each:D2}\""))}}] }""");
        Assert.Equal((HttpStatusCode.Conflict, "messaging.conversation.full"), (crowd.StatusCode, await CodeAsync(crowd)));
        using HttpResponseMessage left = await chat.SendAsync(HttpMethod.Post, $"/conversations/{id}/leave", "sara", "{ }");
        Assert.Equal(HttpStatusCode.NoContent, left.StatusCode);
        using HttpResponseMessage gone = await chat.SendAsync(HttpMethod.Get, $"/conversations/{id}", "sara");
        Assert.Equal((HttpStatusCode.NotFound, "messaging.conversation.notfound"), (gone.StatusCode, await CodeAsync(gone)));
        using HttpResponseMessage unread = await chat.SendAsync(HttpMethod.Get, "/conversations/not-an-id", "amal");
        Assert.Equal((HttpStatusCode.NotFound, "messaging.conversation.notfound"), (unread.StatusCode, await CodeAsync(unread)));
    }

    [Fact]
    public async Task MSG_API_009_AC1_TheGroupConversationRouteDoesWhatItsOperationDoes()
    {
        await using ChatHost chat = await ChatHost.StartAsync(postgres);
        chat.Membership.Add(MembershipFake.Organization("desk"), "amal");

        using HttpResponseMessage opened = await chat.SendAsync(HttpMethod.Get, "/conversations/group/organization/desk", "amal");
        using HttpResponseMessage again = await chat.SendAsync(HttpMethod.Get, "/conversations/group/organization/desk", "amal");
        using HttpResponseMessage outsider = await chat.SendAsync(HttpMethod.Get, "/conversations/group/organization/desk", "omar");
        using HttpResponseMessage kind = await chat.SendAsync(HttpMethod.Get, "/conversations/group/team/desk", "amal");

        Assert.Equal(HttpStatusCode.OK, opened.StatusCode);
        using JsonDocument first = await JsonAsync(opened);
        using JsonDocument second = await JsonAsync(again);
        Assert.Equal(first.RootElement.GetProperty("id").GetString(), second.RootElement.GetProperty("id").GetString());
        Assert.Equal(("group", "organization", "desk"), (first.RootElement.GetProperty("type").GetString(), first.RootElement.GetProperty("scope").GetProperty("kind").GetString(), first.RootElement.GetProperty("scope").GetProperty("reference").GetString()));
        Assert.Empty(first.RootElement.GetProperty("participants").EnumerateArray());
        Assert.Equal((HttpStatusCode.NotFound, "messaging.conversation.notfound"), (outsider.StatusCode, await CodeAsync(outsider)));
        Assert.Equal(("messaging.conversation.invalid", "kind"), await FieldAsync(kind));
        string id = first.RootElement.GetProperty("id").GetString()!;
        using HttpResponseMessage leave = await chat.SendAsync(HttpMethod.Post, $"/conversations/{id}/leave", "amal", "{ }");
        Assert.Equal(("messaging.conversation.invalid", "type"), await FieldAsync(leave));
    }

    [Fact]
    public async Task OpenGroup_AReferenceOutsideItsBound_Is400NamingIt()
    {
        await using ChatHost chat = await ChatHost.StartAsync(postgres);
        string longest = new('r', 128);
        chat.Membership.Add(MembershipFake.Organization(longest), "amal");

        using HttpResponseMessage within = await chat.SendAsync(HttpMethod.Get, "/conversations/group/organization/" + longest, "amal");
        using HttpResponseMessage over = await chat.SendAsync(HttpMethod.Get, "/conversations/group/organization/" + longest + "r", "amal");
        Result<ConversationRecord> called = await chat.CallAsync(chats => chats.OpenGroupAsync("amal", MembershipFake.Organization(longest + "r"), default));

        Assert.Equal(HttpStatusCode.OK, within.StatusCode);
        Assert.Equal(("messaging.request.malformed", "reference"), await MemberAsync(over));
        Assert.Equal("reference", called.Failure?.Details["member"]);
    }

    private static async Task<JsonDocument> JsonAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        using JsonDocument body = await JsonAsync(response);
        return body.RootElement.GetProperty("code").GetString();
    }

    private static async Task<(string?, string?)> FieldAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using JsonDocument body = await JsonAsync(response);
        return (body.RootElement.GetProperty("code").GetString(), body.RootElement.GetProperty("details").GetProperty("field").GetString());
    }

    private static async Task<(string?, string?)> MemberAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using JsonDocument body = await JsonAsync(response);
        return (body.RootElement.GetProperty("code").GetString(), body.RootElement.GetProperty("details").GetProperty("member").GetString());
    }
}