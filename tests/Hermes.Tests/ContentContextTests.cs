using System;

using Hermes.Sending;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "unit")]
public sealed class ContentContextTests
{
    [Fact]
    public void Of_ARow_IsItsTableItsIdInLowerCaseHyphenatedHexAndItsField()
    {
        string context = ContentContext.Of(ContentContext.Subscriptions, new Guid("0F8FAD5B-D9CB-469F-A165-70867728950E"), "p256dh");

        Assert.Equal("subscriptions:0f8fad5b-d9cb-469f-a165-70867728950e:p256dh", context);
    }

    [Fact]
    public void Tables_AreTheNamesTheContextGivesThem()
    {
        Assert.Equal(("deliveries", "subscriptions"), (ContentContext.Deliveries, ContentContext.Subscriptions));
    }
}