using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Security.Cryptography;

using Hermes.Core;
using Hermes.Sending;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "unit")]
public sealed class PushOutcomeTests
{
    private static readonly Guid First = new("00000000-0000-7000-8000-000000000001");

    private static readonly Guid Second = new("00000000-0000-7000-8000-000000000002");

    private static readonly Guid Third = new("00000000-0000-7000-8000-000000000003");

    [Fact]
    public void Targets_AFirstAttempt_GoesToEveryLiveSubscriptionAndNoneEndsNoSubscription()
    {
        Assert.Equal([First, Second], PushOutcomes.Targets([], 0, [First, Second]).Targets);
        Assert.Equal("nosubscription", PushOutcomes.Targets([], 0, []).Ends);
    }

    [Fact]
    public void Targets_AfterATransientAttempt_GoToThoseThatAnsweredTransientAndAreStillLive()
    {
        List<PushCall> calls = [new(1, First, AttemptClass.Transient, AttemptReason.Temporary), new(1, null, AttemptClass.Gone, AttemptReason.Gone), new(1, Third, AttemptClass.Transient, AttemptReason.Temporary)];

        Assert.Equal([First], PushOutcomes.Targets(calls, 1, [First, Second]).Targets);
        Assert.Equal(("nosubscription", (AttemptReason?)null), Ends(PushOutcomes.Targets(calls, 1, [Second])));
    }

    [Fact]
    public void Targets_AfterAnAttemptThatMadeNoCall_GoToEveryLiveSubscriptionNotRefusedAndNoneLeftEndsFailedWithTheLastPermanentReason()
    {
        List<PushCall> calls = [new(1, First, AttemptClass.Permanent, AttemptReason.MessageRefused), new(1, Second, AttemptClass.Transient, AttemptReason.Temporary)];

        Assert.Equal([Second, Third], PushOutcomes.Targets(calls, 2, [First, Second, Third]).Targets);
        Assert.Equal(("failed", (AttemptReason?)AttemptReason.MessageRefused), Ends(PushOutcomes.Targets(calls, 2, [First])));
        Assert.Equal(("nosubscription", (AttemptReason?)null), Ends(PushOutcomes.Targets([], 1, [])));
    }

    [Fact]
    public void Targets_AfterAnAttemptLostBetweenItsCalls_GoToEveryLiveSubscriptionNotRefusedThoughSomeWereCalled()
    {
        List<PushCall> transient = [new(1, First, AttemptClass.Transient, AttemptReason.Temporary)];
        List<PushCall> refused = [new(1, First, AttemptClass.Permanent, AttemptReason.BadRequest)];

        Assert.Equal([First, Second], PushOutcomes.Targets(transient, 1, [First, Second], lost: true).Targets);
        Assert.Equal([Second], PushOutcomes.Targets(refused, 1, [First, Second], lost: true).Targets);
    }

    [Fact]
    public void Of_OneAcceptedAmongOthers_IsAccepted()
    {
        Assert.Equal(AttemptClass.Accepted, PushOutcomes.Of([new Attempt.Accepted(null), new Attempt.Transient(AttemptReason.Temporary, 503, null)]).Class);
    }

    [Fact]
    public void Of_NoneAcceptedAndOneTransient_IsTransientWithTheLongestRetryAfter()
    {
        (AttemptClass answered, AttemptReason? reason, TimeSpan? after) = PushOutcomes.Of(
        [
            new Attempt.Transient(AttemptReason.RateLimited, 429, TimeSpan.FromSeconds(90)),
            new Attempt.Gone(AttemptReason.Gone, 410),
            new Attempt.Transient(AttemptReason.Temporary, 503, TimeSpan.FromSeconds(30)),
        ]);

        Assert.Equal((AttemptClass.Transient, AttemptReason.Temporary, TimeSpan.FromSeconds(90)), (answered, reason!.Value, after!.Value));
    }

    [Fact]
    public void Of_EveryOneGoneOrSkipped_IsGone()
    {
        Assert.Equal(AttemptClass.Gone, PushOutcomes.Of([new Attempt.Gone(AttemptReason.Gone, 404), null]).Class);
    }

    [Fact]
    public void Of_PermanentAndGone_IsPermanentWithTheLastPermanentReason()
    {
        (AttemptClass answered, AttemptReason? reason, _) = PushOutcomes.Of([new Attempt.Permanent(AttemptReason.BadRequest, 400), new Attempt.Gone(AttemptReason.Gone, 410), new Attempt.Permanent(AttemptReason.MessageRefused, 413)]);

        Assert.Equal((AttemptClass.Permanent, AttemptReason.MessageRefused), (answered, reason!.Value));
    }

    [Theory]
    [InlineData("https://push.example.com/send/abc", true)]
    [InlineData("https://push.example.com:443/send/abc", true)]
    [InlineData("https://other.example.com/send/abc", false)]
    [InlineData("https://127.0.0.1/send/abc", false)]
    [InlineData("https://push.example.com:8443/send/abc", false)]
    [InlineData("http://push.example.com/send/abc", false)]
    [InlineData("https://user@push.example.com/send/abc", false)]
    [InlineData("/send/abc", false)]
    public void Allows_OnlyAnHttpsEndpointOnPort443WithNoUserInformationAtAnAllowedDnsName(string endpoint, bool allowed)
    {
        Assert.Equal(allowed, SubscriptionRule.Allows(endpoint, ["push.example.com"]));
    }

    [Fact]
    public void Allows_AnEndpointOverTheMostCharacters_IsRefused()
    {
        string endpoint = "https://push.example.com/" + new string('a', 2_048);

        Assert.False(SubscriptionRule.Allows(endpoint, ["push.example.com"]));
    }

    [Fact]
    public void Keys_AP256PointAndA16ByteSecret_ReadAndAnythingElseDoesNot()
    {
        (string point, string auth) = Subscriber();

        Assert.Equal(81, SubscriptionRule.Keys(point, auth)!.Length);
        Assert.Null(SubscriptionRule.Keys(point, Base64Url.EncodeToString(new byte[15])));
        Assert.Null(SubscriptionRule.Keys(Base64Url.EncodeToString([0x04, .. new byte[64]]), auth));
        Assert.Null(SubscriptionRule.Keys("not base64url!", auth));
        Assert.Null(SubscriptionRule.Keys(null, auth));
    }

    /// <summary>A browser's subscription keys: a fresh P-256 public key and a 16-byte secret, base64url.</summary>
    internal static (string P256dh, string Auth) Subscriber()
    {
        using ECDiffieHellman key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        ECParameters parameters = key.ExportParameters(includePrivateParameters: false);
        return (Base64Url.EncodeToString([0x04, .. parameters.Q.X!, .. parameters.Q.Y!]), Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16)));
    }

    private static (string? Ends, AttemptReason? Reason) Ends((IReadOnlyList<Guid> Targets, string? Ends, AttemptReason? Reason) targets)
    {
        Assert.Empty(targets.Targets);
        return (targets.Ends, targets.Reason);
    }
}