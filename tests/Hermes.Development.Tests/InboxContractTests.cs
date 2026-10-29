using System;
using System.Collections.Generic;

using Xunit;

namespace Hermes.Development.Tests;

/// <summary>The inbox group's metadata and bodies against <c>05</c> section 6 (<c>07</c> MSG-ENG-006; <c>05</c> MSG-API-007).</summary>
[Trait("Category", "contract")]
public sealed class InboxContractTests
{
    private const string Clear = "| `DELETE /messages` | **204**,";

    [Fact]
    public void MSG_ENG_006_AC1_TheInboxsMappedEndpointsAreExactlyTheRoutesMethodsStatusesAndBodiesOf05()
    {
        SortedSet<string> given = InboxContract.Read(InboxContract.Text(), InboxContract.Prefix);

        Assert.Empty(InboxContract.Violations(given, InboxContract.Mapped()));
        Assert.Contains("GET /dev/inbox/messages 200 body messages.summary", given);
        Assert.Contains("GET /dev/inbox/messages/{id} 200 body email.replyTo.displayName", given);
        Assert.Contains("GET /dev/inbox/messages/latest 200 body push.notification.badge", given);
        Assert.Contains("GET /dev/inbox/messages/{id} 200 body email.headers.value", given);
        Assert.Contains("GET /dev/inbox/assets/inbox.css 403 body code", given);
        Assert.Contains("GET /dev/inbox/messages/latest 400 body details", given);
        Assert.DoesNotContain("GET /dev/inbox/messages/{id} 400", given);
        Assert.DoesNotContain("GET /dev/inbox/messages/{id} 200 body limit", given);
    }

    [Fact]
    public void MSG_ENG_006_AC1_EveryBodyOfTheInboxsSerializerContextIsOneARouteAnswers()
    {
        Assert.Empty(InboxContract.Unmapped());
    }

    [Fact]
    public void MSG_ENG_006_AC1_AStatusOfTheInboxThatTheEndpointDoesNotDeclareIsFound()
    {
        string text = InboxContract.Text();
        Assert.Contains(Clear, text, StringComparison.Ordinal);

        SortedSet<string> given = InboxContract.Read(text.Replace(Clear, Clear.Replace("**204**", "**200**", StringComparison.Ordinal), StringComparison.Ordinal), InboxContract.Prefix);

        Assert.Equal(
            [
                "05 gives DELETE /dev/inbox/messages 200, which the mapped endpoints do not.",
                "The mapped endpoints give DELETE /dev/inbox/messages 204, which 05 does not.",
            ],
            InboxContract.Violations(given, InboxContract.Mapped()));
    }

    [Fact]
    public void MSG_ENG_006_AC1_AMemberOfAShapeThatTheBodyDoesNotHoldIsFound()
    {
        string text = InboxContract.Text();
        const string Sms = "| `sms` | `{ \"sender\",";
        Assert.Contains(Sms, text, StringComparison.Ordinal);

        SortedSet<string> given = InboxContract.Read(text.Replace(Sms, "| `sms` | `{ \"country\", \"sender\",", StringComparison.Ordinal), InboxContract.Prefix);

        Assert.Equal(
            [
                "05 gives GET /dev/inbox/messages/latest 200 body sms.country, which the mapped endpoints do not.",
                "05 gives GET /dev/inbox/messages/{id} 200 body sms.country, which the mapped endpoints do not.",
            ],
            InboxContract.Violations(given, InboxContract.Mapped()));
    }
}