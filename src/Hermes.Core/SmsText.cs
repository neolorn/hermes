using System;
using System.Buffers;

namespace Hermes.Core;

/// <summary>
/// Counts an SMS text's segments as the network counts them (<c>02</c> MSG-SMS-001), so a host
/// that builds an <see cref="SmsMessage"/> itself fills its encoding and segments as the engine does.
/// </summary>
public static class SmsText
{
    private const int Gsm7Single = 160;

    private const int Gsm7Concatenated = 153;

    private const int Ucs2Single = 70;

    private const int Ucs2Concatenated = 67;

    /// <summary>The GSM 7-bit default alphabet, its escape (0x1B) aside (3GPP TS 23.038 section 6.2.1).</summary>
    private static readonly SearchValues<char> DefaultAlphabet = SearchValues.Create(
        "@£$¥èéùìòÇ\nØø\rÅå"
        + "Δ_ΦΓΛΩΠΨΣΘΞÆæßÉ"
        + " !\"#¤%&'()*+,-./0123456789:;<=>?"
        + "¡ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÑÜ§"
        + "¿abcdefghijklmnopqrstuvwxyzäöñüà");

    /// <summary>The characters of the extension table, each sent as the escape and itself (3GPP TS 23.038 section 6.2.1.1).</summary>
    private static readonly SearchValues<char> Extension = SearchValues.Create("\f^{}\\[~]|€");

    /// <summary>
    /// Measures <paramref name="text"/>: GSM 7-bit where every character is of the default
    /// alphabet or its extension table, an extension character taking two septets, one segment
    /// holding 160 septets and each segment of a longer text 153; otherwise UCS-2, one segment
    /// holding 70 UTF-16 code units and each segment of a longer text 67. Segments are counted
    /// by packing the text in order, never splitting an escape pair or a surrogate pair.
    /// </summary>
    /// <param name="text">The text, as it is sent.</param>
    /// <returns>The text's encoding and segments; an empty text takes one segment.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public static SmsMeasure Measure(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        ReadOnlySpan<char> characters = text;
        bool gsm7 = true;
        foreach (char character in characters)
        {
            if (!DefaultAlphabet.Contains(character) && !Extension.Contains(character))
            {
                gsm7 = false;
                break;
            }
        }

        return gsm7
            ? new SmsMeasure { Encoding = SmsEncoding.Gsm7, Segments = Count(characters, Septets, Gsm7Single, Gsm7Concatenated) }
            : new SmsMeasure { Encoding = SmsEncoding.Ucs2, Segments = Count(characters, CodeUnits, Ucs2Single, Ucs2Concatenated) };
    }

    /// <summary>The units the character at <paramref name="index"/> takes, and the characters it spans.</summary>
    private delegate (int Units, int Length) Cost(ReadOnlySpan<char> text, int index);

    private static int Count(ReadOnlySpan<char> text, Cost cost, int single, int concatenated)
    {
        int total = 0;
        for (int index = 0; index < text.Length;)
        {
            (int units, int length) = cost(text, index);
            total += units;
            index += length;
        }

        if (total <= single)
        {
            return 1;
        }

        int segments = 1;
        int used = 0;
        for (int index = 0; index < text.Length;)
        {
            (int units, int length) = cost(text, index);
            if (used + units > concatenated)
            {
                segments++;
                used = 0;
            }

            used += units;
            index += length;
        }

        return segments;
    }

    private static (int Units, int Length) Septets(ReadOnlySpan<char> text, int index)
    {
        return (Extension.Contains(text[index]) ? 2 : 1, 1);
    }

    private static (int Units, int Length) CodeUnits(ReadOnlySpan<char> text, int index)
    {
        bool pair = char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]);
        return pair ? (2, 2) : (1, 1);
    }
}