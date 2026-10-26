using System;
using System.Buffers;
using System.Text;

namespace Hermes.Channels;

/// <summary>
/// A length in characters as <c>00</c> section 1.5 counts it: Unicode scalar values, so a
/// character outside the Basic Multilingual Plane counts once, and a string holding a
/// surrogate that is not part of a pair is no text.
/// </summary>
internal static class TextLength
{
    /// <summary>The scalar values of <paramref name="text"/>; none where it holds a surrogate that is not part of a pair.</summary>
    internal static int? ScalarValues(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        int count = 0;
        ReadOnlySpan<char> rest = text;
        while (!rest.IsEmpty)
        {
            if (Rune.DecodeFromUtf16(rest, out _, out int used) != OperationStatus.Done)
            {
                return null;
            }

            rest = rest[used..];
            count++;
        }

        return count;
    }

    /// <summary>Whether <paramref name="text"/> is text of at most <paramref name="most"/> scalar values.</summary>
    internal static bool AtMost(string text, int most)
    {
        return ScalarValues(text) is { } length && length <= most;
    }
}