using System;

using Xunit;

namespace Hermes.Channels.Tests;

[Trait("Category", "unit")]
public sealed class AddressRuleTests
{
    [Theory]
    [InlineData("news@example.com")]
    [InlineData("first.last@example.com")]
    [InlineData("a@b")]
    [InlineData("!#$%&'*+-/=?^_`{|}~@example.com")]
    [InlineData("\"quoted local\"@example.com")]
    [InlineData("\"a@b\"@example.com")]
    [InlineData("\"with \\\" pair\"@example.com")]
    [InlineData("\"\"@example.com")]
    [InlineData("news@[192.0.2.1]")]
    [InlineData("news@[IPv6:2001:db8::1]")]
    public void IsAddress_AddrSpec_IsAnAddress(string text)
    {
        Assert.True(AddressRule.IsAddress(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("news")]
    [InlineData("@example.com")]
    [InlineData("news@")]
    [InlineData(".news@example.com")]
    [InlineData("news.@example.com")]
    [InlineData("first..last@example.com")]
    [InlineData("news@example..com")]
    [InlineData("news@.example.com")]
    [InlineData("two@signs@example.com")]
    [InlineData("with space@example.com")]
    [InlineData("News <news@example.com>")]
    [InlineData("news(comment)@example.com")]
    [InlineData("news@example.com (comment)")]
    [InlineData(" news@example.com")]
    [InlineData("news@example.com\r\nBcc: x@example.com")]
    [InlineData("\"unclosed@example.com")]
    [InlineData("\"bad\\\"@example.com")]
    [InlineData("\"line\r\nbreak\"@example.com")]
    [InlineData("news@[unclosed")]
    [InlineData("news@[a[b]")]
    [InlineData("nouvelles@exemple.fré")]
    [InlineData("néws@example.com")]
    public void IsAddress_NoAddrSpecOrAnObsoleteOrCommentedForm_IsNoAddress(string text)
    {
        Assert.False(AddressRule.IsAddress(text));
    }

    [Theory]
    [InlineData(254, true)]
    [InlineData(255, false)]
    public void IsAddress_Length_IsAtMost254Characters(int length, bool within)
    {
        string text = new string('a', 64) + "@" + new string('b', length - 65);

        Assert.Equal(within, AddressRule.IsAddress(text));
    }

    [Fact]
    public void IsAddress_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => AddressRule.IsAddress(null!));
    }
}