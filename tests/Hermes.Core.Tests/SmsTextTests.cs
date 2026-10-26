using System;

using Xunit;

namespace Hermes.Core.Tests;

[Trait("Category", "unit")]
public sealed class SmsTextTests
{
    private const string Arabic = "ب";

    [Theory]
    [InlineData(70, 1)]
    [InlineData(71, 2)]
    [InlineData(134, 2)]
    [InlineData(135, 3)]
    public void Measure_ArabicText_IsUcs2InSegmentsOf70ThenOf67(int length, int segments)
    {
        SmsMeasure measure = SmsText.Measure(Repeat(Arabic, length));

        Assert.Equal(SmsEncoding.Ucs2, measure.Encoding);
        Assert.Equal(segments, measure.Segments);
    }

    [Fact]
    public void MSG_SMS_001_AC2_ATextOf160CharactersOfTheDefaultAlphabetIsOneSegmentAnd159WithOneEuroAreTwo()
    {
        SmsMeasure whole = SmsText.Measure(Repeat("a", 160));
        SmsMeasure euro = SmsText.Measure(Repeat("a", 159) + "€");

        Assert.Equal(new SmsMeasure { Encoding = SmsEncoding.Gsm7, Segments = 1 }, whole);
        Assert.Equal(new SmsMeasure { Encoding = SmsEncoding.Gsm7, Segments = 2 }, euro);
    }

    [Fact]
    public void MSG_SMS_001_AC3_ATextWhose153rdSeptetWouldBeAnEscapeStartsItsSecondSegmentWithTheEscape()
    {
        // 152 septets, then an escape pair that does not fit the first segment's last septet,
        // then 152 more: 152, 2 + 151, 1. Splitting the pair would answer two segments.
        SmsMeasure measure = SmsText.Measure(Repeat("a", 152) + "€" + Repeat("a", 152));

        Assert.Equal(SmsEncoding.Gsm7, measure.Encoding);
        Assert.Equal(3, measure.Segments);
    }

    [Fact]
    public void Measure_SurrogatePairAtTheEndOfASegment_StartsTheNextSegment()
    {
        // 66 code units, then a pair that does not fit the first segment's last unit, then 66
        // more: 66, 2 + 65, 1.
        SmsMeasure measure = SmsText.Measure(Repeat(Arabic, 66) + "\U0001F4E8" + Repeat(Arabic, 66));

        Assert.Equal(SmsEncoding.Ucs2, measure.Encoding);
        Assert.Equal(3, measure.Segments);
    }

    [Theory]
    [InlineData("@£$¥èéùìòÇ\nØø\rÅå")]
    [InlineData("Δ_ΦΓΛΩΠΨΣΘΞÆæßÉ")]
    [InlineData(" !\"#¤%&'()*+,-./0123456789:;<=>?")]
    [InlineData("¡ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÑÜ§")]
    [InlineData("¿abcdefghijklmnopqrstuvwxyzäöñüà")]
    public void Measure_EachCharacterOfTheDefaultAlphabet_TakesOneSeptet(string characters)
    {
        ArgumentNullException.ThrowIfNull(characters);

        SmsMeasure measure = SmsText.Measure(Repeat(characters, 160 / characters.Length) + Repeat("a", 160 % characters.Length));

        Assert.Equal(new SmsMeasure { Encoding = SmsEncoding.Gsm7, Segments = 1 }, measure);
    }

    [Theory]
    [InlineData('\f')]
    [InlineData('^')]
    [InlineData('{')]
    [InlineData('}')]
    [InlineData('\\')]
    [InlineData('[')]
    [InlineData('~')]
    [InlineData(']')]
    [InlineData('|')]
    [InlineData('€')]
    public void Measure_EachCharacterOfTheExtensionTable_TakesTwoSeptets(char character)
    {
        SmsMeasure fits = SmsText.Measure(new string(character, 80));
        SmsMeasure over = SmsText.Measure(new string(character, 80) + "a");

        Assert.Equal(new SmsMeasure { Encoding = SmsEncoding.Gsm7, Segments = 1 }, fits);
        Assert.Equal(new SmsMeasure { Encoding = SmsEncoding.Gsm7, Segments = 2 }, over);
    }

    [Theory]
    [InlineData("ç")]
    [InlineData("`")]
    [InlineData("\u001B")]
    [InlineData(" ")]
    [InlineData("\U0001F4E8")]
    public void Measure_CharacterOutsideTheAlphabetAndItsExtension_MakesTheTextUcs2(string character)
    {
        Assert.Equal(SmsEncoding.Ucs2, SmsText.Measure("Your code is ready " + character).Encoding);
    }

    [Fact]
    public void Measure_EmptyText_IsOneSegment()
    {
        Assert.Equal(new SmsMeasure { Encoding = SmsEncoding.Gsm7, Segments = 1 }, SmsText.Measure(string.Empty));
    }

    [Fact]
    public void Measure_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => SmsText.Measure(null!));
    }

    private static string Repeat(string text, int count)
    {
        return string.Concat(System.Linq.Enumerable.Repeat(text, count));
    }
}