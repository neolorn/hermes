using System;

using Xunit;

namespace Hermes.Channels.Tests;

[Trait("Category", "unit")]
public sealed class SenderRuleTests
{
    [Theory]
    [InlineData("A")]
    [InlineData("Unison")]
    [InlineData("Unison News")]
    [InlineData("ABCDEFGHIJK")]
    [InlineData("abc 123")]
    [InlineData("1")]
    [InlineData("201000000000000")]
    public void IsSender_NameOrNumber_IsASender(string sender)
    {
        Assert.True(SenderRule.IsSender(sender));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ABCDEFGHIJKL")]
    [InlineData("2010000000000000")]
    [InlineData("Unison-News")]
    [InlineData("Unison_1")]
    [InlineData("+2010000000")]
    [InlineData("يونيسون")]
    [InlineData("Café")]
    public void IsSender_OutsideTheBaseRule_IsNoSender(string sender)
    {
        Assert.False(SenderRule.IsSender(sender));
    }

    [Theory]
    [InlineData("+")]
    [InlineData("+2")]
    [InlineData("+20")]
    [InlineData("+123456")]
    public void IsPrefix_PlusAndUpToSixDigits_IsAPrefix(string prefix)
    {
        Assert.True(SenderRule.IsPrefix(prefix));
    }

    [Theory]
    [InlineData("")]
    [InlineData("20")]
    [InlineData("+1234567")]
    [InlineData("+20 1")]
    [InlineData("++20")]
    [InlineData("+2a")]
    [InlineData("+٢٠")]
    public void IsPrefix_OtherText_IsNoPrefix(string prefix)
    {
        Assert.False(SenderRule.IsPrefix(prefix));
    }

    [Theory]
    [InlineData("+201000000000", true)]
    [InlineData("+441000000000", false)]
    [InlineData("+2", false)]
    public void Allows_Number_IsAllowedWhereItStartsWithAPrefix(string number, bool allowed)
    {
        Assert.Equal(allowed, SenderRule.Allows(["+20", "+9665"], number));
    }

    [Fact]
    public void Allows_PlusAlone_AllowsEveryNumber()
    {
        Assert.True(SenderRule.Allows(["+"], "+441000000000"));
    }

    [Fact]
    public void Allows_NoPrefix_AllowsNoNumber()
    {
        Assert.False(SenderRule.Allows([], "+201000000000"));
    }
}