using Hermes.Content;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "unit")]
public sealed class SendLanguageTests
{
    private static readonly string[] Declared = ["en", "ar"];

    [Fact]
    public void MSG_TPL_003_AC1_DirectionIsRtlForArAndLtrForEn()
    {
        Assert.Equal("rtl", SendLanguage.Direction("ar"));
        Assert.Equal("ltr", SendLanguage.Direction("en"));
    }

    [Theory]
    [InlineData("ar", "ar")]
    [InlineData("AR", "ar")]
    [InlineData("ar-EG", "ar")]
    [InlineData("En-gb", "en")]
    [InlineData("fr", "en")]
    [InlineData("arabic", "en")]
    [InlineData(null, "en")]
    public void Resolve_ATag_IsTheDeclaredLanguageItMatchesOrTheDefault(string? tag, string resolved)
    {
        Assert.Equal(resolved, SendLanguage.Resolve(tag, Declared, "en"));
    }

    [Theory]
    [InlineData("Africa/Cairo", "Africa/Cairo")]
    [InlineData("Nowhere/Else", "UTC")]
    [InlineData(null, "UTC")]
    public void Zone_ATimeZoneThatDoesNotResolve_IsTheDeployments(string? zone, string resolved)
    {
        Assert.Equal(resolved, SendLanguage.Zone(zone, "UTC"));
    }
}