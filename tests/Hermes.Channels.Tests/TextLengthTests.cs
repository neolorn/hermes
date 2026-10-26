using Xunit;

namespace Hermes.Channels.Tests;

[Trait("Category", "unit")]
public sealed class TextLengthTests
{
    [Theory]
    [InlineData("", 0)]
    [InlineData("News", 4)]
    [InlineData("الأخبار", 7)]
    [InlineData("\U0001F4E8\U0001F4E8", 2)]
    [InlineData("é", 2)]
    public void ScalarValues_Text_CountsEachCodePointOtherThanASurrogateOnce(string text, int length)
    {
        Assert.Equal(length, TextLength.ScalarValues(text));
    }

    [Fact]
    public void ScalarValues_SurrogateNotPartOfAPair_IsNoText()
    {
        // Built here, as theory data would carry a lone surrogate through UTF-8 as U+FFFD.
        const char high = '\uD83D';
        const char low = '\uDCE8';
        string[] texts = [$"{high}", $"{low}", $"{low}{high}", $"News {high}!", $"{high}{low}{high}"];

        Assert.All(texts, text => Assert.Null(TextLength.ScalarValues(text)));
    }

    [Theory]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public void AtMost_ScalarValuesOutsideTheBasicMultilingualPlane_CountOneEach(int count, bool within)
    {
        Assert.Equal(within, TextLength.AtMost(string.Concat(System.Linq.Enumerable.Repeat("\U0001F4E8", count)), 128));
    }
}