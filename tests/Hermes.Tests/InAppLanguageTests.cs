using System.Collections.Generic;

using Hermes.Endpoints;

using Microsoft.Extensions.Primitives;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "unit")]
public sealed class InAppLanguageTests
{
    private static readonly IReadOnlyList<string> Declared = ["en", "ar"];

    [Theory]
    [InlineData("ar, en;q=0.5", "ar")]
    [InlineData("en;q=0.5, ar", "ar")]
    [InlineData("fr, ar-EG;q=0.8", "ar")]
    [InlineData("AR", "ar")]
    [InlineData("ar;q=0, en;q=0.1", "en")]
    [InlineData("*", null)]
    [InlineData("fr, de", null)]
    [InlineData("", null)]
    public void Language_AcceptLanguage_AnswersTheFirstDeclaredMatchByWeight(string header, string? expected)
    {
        Assert.Equal(expected, InAppEndpoints.Language(new StringValues(header), Declared));
    }
}