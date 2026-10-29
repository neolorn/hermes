using Xunit;

namespace Hermes.Development.Tests;

/// <summary>The links a record holds (<c>02</c> MSG-DEV-003).</summary>
[Trait("Category", "unit")]
public sealed class InboxLinksTests
{
    [Fact]
    public void MSG_DEV_003_AC2_EachAbsoluteLinkOfTheHtmlAndTheTextIsHeldOnceInTheOrderMet()
    {
        const string html = """
            <a href="https://bakery.example/orders/7?a=1&amp;b=2">Order</a>
            <a href='/relative'>Relative</a>
            <a href="mailto:help@bakery.example">Mail</a>
            <img src="https://bakery.example/logo.png">
            <a href="HTTP://BAKERY.EXAMPLE/UPPER">Upper</a>
            <a href="https://bakery.example/orders/7?a=1&amp;b=2">Again</a>
            """;
        const string text = "See https://bakery.example/orders/7?a=1&b=2, then (https://bakery.example/help). Or ftp://bakery.example/file and https://bakery.example/end.";

        Assert.Equal(
            ["https://bakery.example/orders/7?a=1&b=2", "HTTP://BAKERY.EXAMPLE/UPPER", "https://bakery.example/help", "https://bakery.example/end"],
            InboxLinks.Of(html, text, null));
    }

    [Fact]
    public void MSG_DEV_003_AC2_AnSmssLinksAreThoseOfItsTextAndAPushsItsAbsoluteUrl()
    {
        Assert.Equal(["https://bakery.example/track/7"], InboxLinks.Of(null, "Track it: https://bakery.example/track/7", null));
        Assert.Equal(["https://bakery.example/orders/7"], InboxLinks.Of(null, null, "https://bakery.example/orders/7"));
        Assert.Empty(InboxLinks.Of(null, null, "/orders/7"));
    }
}