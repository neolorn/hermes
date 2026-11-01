using System;
using System.Collections.Generic;
using System.Linq;

using Hermes.Endpoints;

using Xunit;

namespace Hermes.Tests;

/// <summary>
/// Every group's mapped endpoints, the development inbox's in a host whose environment is
/// Development, their metadata and bodies against the whole of <c>05</c> (<c>07</c>
/// MSG-ENG-006, M-025).
/// </summary>
[Trait("Category", "contract")]
public sealed class EndpointContractTests
{
    private const string Removal = "| `DELETE /push/subscriptions` | `{ \"endpoint\" }` | **204**,";

    private const string Clear = "| `DELETE /messages` | **204**,";

    private const string Sms = "| `sms` | `{ \"sender\",";

    [Fact]
    public void MSG_ENG_006_AC1_TheMappedEndpointsAreExactlyTheRoutesMethodsStatusesAndBodiesOf05()
    {
        SortedSet<string> given = ApiContract.Read();

        Assert.Empty(ApiContract.Violations(given, MappedEndpoints.Lines()));
        Assert.Contains("GET /notifications/push/key 200 body publicKey", given);
        Assert.Contains("PUT /notifications/preferences/{category}/{channel} 401 body details", given);
        Assert.Contains("GET /notifications/preferences 200 body categories.channels.on", given);
        Assert.Contains("POST /notifications/push/subscriptions request keys.p256dh", given);
        Assert.Contains("GET /notifications/unsubscribe/{token} 503 body code", given);
        Assert.DoesNotContain("GET /notifications/push/key 401", given);
        Assert.DoesNotContain("POST /notifications/unsubscribe/{token} 415", given);
        Assert.Contains("GET /notifications/messages 200 body messages.summary", given);
        Assert.Contains("GET /notifications/messages/{id} 200 body email.replyTo.displayName", given);
        Assert.Contains("GET /notifications/messages/latest 200 body push.notification.badge", given);
        Assert.Contains("GET /notifications/messages/{id} 200 body email.headers.value", given);
        Assert.Contains("GET /notifications/assets/inbox.css 403 body code", given);
        Assert.Contains("GET /notifications/messages/latest 400 body details", given);
        Assert.DoesNotContain("GET /notifications/messages/{id} 400", given);
        Assert.DoesNotContain("GET /notifications/messages/{id} 200 body limit", given);
        Assert.DoesNotContain("GET /notifications/messages 401", given);
        Assert.Contains("GET /notifications/inapp 200 body entries.targetValues", given);
        Assert.Contains("GET /notifications/inapp/unread 503 body code", given);
        Assert.Contains("POST /notifications/inapp/read 401 body details", given);
    }

    [Fact]
    public void MSG_ENG_006_AC1_EveryBodyOfTheLibrarysSerializerContextsIsOneARouteReadsOrAnswers()
    {
        Assert.Contains(BodyMembers.Roots(), type => type.Name == "ListBody");
        Assert.Empty(MappedEndpoints.Unmapped());
    }

    [Fact]
    public void MSG_ENG_006_AC1_AStatusOfTheInbox05GivesThatTheEndpointDoesNotDeclareIsFound()
    {
        string text = ApiContract.Text();
        Assert.Contains(Clear, text, StringComparison.Ordinal);

        IReadOnlyList<string> violations = Compare(text.Replace(Clear, Clear.Replace("**204**", "**200**", StringComparison.Ordinal), StringComparison.Ordinal));

        Assert.Equal(
            [
                "05 gives DELETE /notifications/messages 200, which the mapped endpoints do not.",
                "The mapped endpoints give DELETE /notifications/messages 204, which 05 does not.",
            ],
            violations);
    }

    [Fact]
    public void MSG_ENG_006_AC1_AMemberOfAnInboxShape05GivesThatTheBodyDoesNotHoldIsFound()
    {
        string text = ApiContract.Text();
        Assert.Contains(Sms, text, StringComparison.Ordinal);

        IReadOnlyList<string> violations = Compare(text.Replace(Sms, "| `sms` | `{ \"country\", \"sender\",", StringComparison.Ordinal));

        Assert.Equal(
            [
                "05 gives GET /notifications/messages/latest 200 body sms.country, which the mapped endpoints do not.",
                "05 gives GET /notifications/messages/{id} 200 body sms.country, which the mapped endpoints do not.",
            ],
            violations);
    }

    [Fact]
    public void MSG_ENG_006_AC1_AStatus05GivesThatTheEndpointDoesNotDeclareIsFound()
    {
        string text = ApiContract.Text();
        Assert.Contains(Removal, text, StringComparison.Ordinal);

        IReadOnlyList<string> violations = Compare(text.Replace(Removal, Removal.Replace("**204**", "**200**", StringComparison.Ordinal), StringComparison.Ordinal));

        Assert.Equal(
            [
                "05 gives DELETE /notifications/push/subscriptions 200, which the mapped endpoints do not.",
                "The mapped endpoints give DELETE /notifications/push/subscriptions 204, which 05 does not.",
            ],
            violations);
    }

    [Fact]
    public void MSG_ENG_006_AC1_ARequestMember05GivesThatTheBodyDoesNotHoldIsFound()
    {
        IReadOnlyList<string> violations = Compare(ApiContract.Text().Replace(Removal, Removal.Replace("{ \"endpoint\" }", "{ \"endpoint\", \"reason\" }", StringComparison.Ordinal), StringComparison.Ordinal));

        Assert.Equal(["05 gives DELETE /notifications/push/subscriptions request reason, which the mapped endpoints do not."], violations);
    }

    [Fact]
    public void MSG_ENG_006_AC1_ARoute05DoesNotGiveIsFound()
    {
        string text = ApiContract.Text();
        int row = text.IndexOf("| `GET /push/key` |", StringComparison.Ordinal);
        Assert.True(row >= 0);

        IReadOnlyList<string> violations = Compare(text.Remove(row, text.IndexOf('\n', row) + 1 - row));

        Assert.Contains("The mapped endpoints give GET /notifications/push/key, which 05 does not.", violations);
        Assert.Contains("The mapped endpoints give GET /notifications/push/key 200 body publicKey, which 05 does not.", violations);
    }

    [Fact]
    public void MSG_API_009_AC2_TheContractTestFindsTheStaffHubsRouteAndMethods()
    {
        SortedSet<string> given = ApiContract.Read();
        SortedSet<string> mapped = MappedEndpoints.Lines();

        Assert.Empty(ApiContract.Violations(given, mapped));
        Assert.Contains("HUB /staff/live", mapped);
        Assert.Contains("HUB /staff/live server message conversationId,message", mapped);
        Assert.Contains("HUB /staff/live server presence participantId,online", mapped);
        Assert.Contains("HUB /staff/live client Typing conversationId", mapped);
        Assert.Equal(8, mapped.Count(line => line.StartsWith("HUB /staff/", StringComparison.Ordinal)));
    }

    [Fact]
    public void MSG_API_009_AC2_AHubMethod05GivesThatTheHubDoesNotHaveIsFound()
    {
        const string Typing = "| `Typing` | The client | `conversationId` |";
        string text = ApiContract.Text();
        Assert.Contains(Typing, text, StringComparison.Ordinal);

        // The staff hub's table comes first; the public hub's holds the same row.
        int row = text.IndexOf(Typing, StringComparison.Ordinal);
        IReadOnlyList<string> violations = Compare(text.Remove(row, Typing.Length).Insert(row, Typing.Replace("`conversationId`", "`conversationId`, `draft`", StringComparison.Ordinal)));

        Assert.Equal(
            [
                "05 gives HUB /staff/live client Typing conversationId,draft, which the mapped endpoints do not.",
                "The mapped endpoints give HUB /staff/live client Typing conversationId, which 05 does not.",
            ],
            violations);
    }

    [Fact]
    public void MSG_API_010_AC1_TheContractTestFindsExactlyThePublicRoutesAndHub()
    {
        SortedSet<string> given = ApiContract.Read();
        SortedSet<string> mapped = MappedEndpoints.Lines();

        Assert.Empty(ApiContract.Violations(given, mapped));
        Assert.Contains("POST /public/support/conversations 201 body offline", given);
        Assert.Contains("POST /public/support/conversations 429 body code", given);
        Assert.Contains("POST /public/support/offline request email", given);
        Assert.Contains("GET /public/support/topics 200 body topics.open", given);
        Assert.Contains("POST /public/support/conversations/{id}/close 415 body code", given);
        Assert.Contains("POST /staff/support/conversations/{id}/close 503 body code", given);
        Assert.DoesNotContain("POST /public/support/conversations 401", given);
        Assert.Contains("POST /public/support/conversations/{id}/close 503 body code", given);
        Assert.Contains("GET /staff/support/queue 503 body code", given);
        Assert.Contains("HUB /public/live server place position", mapped);
        Assert.Contains("HUB /public/live client Typing conversationId", mapped);
        Assert.Equal(8, mapped.Count(line => line.StartsWith("HUB /public/", StringComparison.Ordinal)));
    }

    [Fact]
    public void MSG_API_010_AC1_APublicRoute05DoesNotGiveIsFound()
    {
        string text = ApiContract.Text();
        int section = text.IndexOf("## 9. Public conversations", StringComparison.Ordinal);
        int row = text.IndexOf("| `POST /support/conversations/{id}/close` |", section, StringComparison.Ordinal);
        Assert.True(section >= 0 && row > section);

        IReadOnlyList<string> violations = Compare(text.Remove(row, text.IndexOf('\n', row) + 1 - row));

        Assert.Contains("The mapped endpoints give POST /public/support/conversations/{id}/close, which 05 does not.", violations);
        Assert.DoesNotContain(violations, violation => violation.Contains("/staff/", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("{ \"on\": true | false }", "on")]
    [InlineData("{ \"code\": \"<code>\", \"details\": { ... } }", "code,details")]
    [InlineData("{ \"endpoint\", \"expirationTime\", \"keys\": { \"p256dh\", \"auth\" }, \"applicationServerKey\" }", "endpoint,expirationTime,keys,keys.p256dh,keys.auth,applicationServerKey")]
    [InlineData("{ \"categories\": [ { \"name\", \"essential\", \"channels\": [ { \"channel\", \"on\" } ] } ] }", "categories,categories.name,categories.essential,categories.channels,categories.channels.channel,categories.channels.on")]
    public void OfSketch_ABodyAs05WritesIt_AnswersEachMemberPath(string sketch, string members)
    {
        ArgumentNullException.ThrowIfNull(members);

        Assert.Equal(members.Split(','), BodyMembers.OfSketch(sketch));
    }

    [Fact]
    public void Of_ABodyWithNestedBodies_AnswersEachMemberPathAsTheSerializerContextNamesIt()
    {
        Assert.Equal(
            ["categories", "categories.name", "categories.essential", "categories.channels", "categories.channels.channel", "categories.channels.on"],
            BodyMembers.Of(typeof(PreferencesBody)));
    }

    private static IReadOnlyList<string> Compare(string text)
    {
        return ApiContract.Violations(ApiContract.Read(text, Endpoints.Prefix), MappedEndpoints.Lines());
    }
}