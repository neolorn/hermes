using System;

using Xunit;

namespace Hermes.Channels.Tests;

[Trait("Category", "unit")]
public sealed class HostRuleTests
{
    private static readonly string[] Defaults =
    [
        "fcm.googleapis.com",
        "*.push.services.mozilla.com",
        "*.push.apple.com",
        "*.notify.windows.com",
    ];

    [Theory]
    [InlineData("smtp.example.com")]
    [InlineData("localhost")]
    [InlineData("a-b.example")]
    [InlineData("3com.example")]
    [InlineData("SMTP.Example.COM")]
    public void IsHostName_DnsName_IsAHostName(string text)
    {
        Assert.True(HostRule.IsHostName(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("192.0.2.1")]
    [InlineData("[2001:db8::1]")]
    [InlineData("2001:db8::1")]
    [InlineData("example.com.")]
    [InlineData(".example.com")]
    [InlineData("a..example")]
    [InlineData("-a.example")]
    [InlineData("a-.example")]
    [InlineData("a_b.example")]
    [InlineData("smtp.example.com:587")]
    [InlineData("bücher.example")]
    [InlineData("*.example.com")]
    public void IsHostName_OtherText_IsNoHostName(string text)
    {
        Assert.False(HostRule.IsHostName(text));
    }

    [Fact]
    public void IsHostName_Length_IsAtMost253CharactersAnd63ALabel()
    {
        string label = new('a', 63);
        string longest = string.Join('.', label, label, label, new string('a', 61));

        Assert.True(HostRule.IsHostName(longest));
        Assert.False(HostRule.IsHostName(longest + "a"));
        Assert.False(HostRule.IsHostName(new string('a', 64) + ".example"));
    }

    [Theory]
    [InlineData("fcm.googleapis.com")]
    [InlineData("*.push.apple.com")]
    [InlineData("*.push.services.mozilla.com")]
    [InlineData("push.example")]
    public void IsAllowedHostEntry_NameOrWildcardOverThreeLabels_IsAnEntry(string entry)
    {
        Assert.True(HostRule.IsAllowedHostEntry(entry));
    }

    [Theory]
    [InlineData("*.com")]
    [InlineData("*.example.com")]
    [InlineData("*")]
    [InlineData("*.")]
    [InlineData("**.push.apple.com")]
    [InlineData("a.*.push.apple.com")]
    [InlineData("*push.apple.com")]
    [InlineData("192.0.2.1")]
    public void IsAllowedHostEntry_OtherText_IsNoEntry(string entry)
    {
        Assert.False(HostRule.IsAllowedHostEntry(entry));
    }

    [Theory]
    [InlineData("fcm.googleapis.com", true)]
    [InlineData("FCM.googleapis.com", true)]
    [InlineData("web.push.apple.com", true)]
    [InlineData("a.b.push.apple.com", true)]
    [InlineData("updates.push.services.mozilla.com", true)]
    [InlineData("wns2-db5p.notify.windows.com", true)]
    [InlineData("push.apple.com", false)]
    [InlineData("evilpush.apple.com", false)]
    [InlineData("web.push.apple.com.example", false)]
    [InlineData("x.fcm.googleapis.com", false)]
    [InlineData("googleapis.com", false)]
    [InlineData("192.0.2.1", false)]
    public void Allows_Host_IsAllowedByAnExactEntryOrBySubdomainOfAWildcard(string host, bool allowed)
    {
        Assert.Equal(allowed, HostRule.Allows(Defaults, host));
    }

    [Fact]
    public void Allows_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => HostRule.Allows(Defaults, null!));
    }
}