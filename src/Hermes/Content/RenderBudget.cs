using System;
using System.Text;

namespace Hermes.Content;

/// <summary>
/// A channel's limit on what a render writes and builds (<c>03</c> MSG-TPL-007): for email,
/// <c>Hermes:Email:MaxBytes</c> in UTF-8 over its subject, text and HTML together (<c>02</c>
/// MSG-EMAIL-003); for SMS, ten segments' worth of UTF-16 code units, the most a text of ten
/// 153-septet segments can hold, the segments themselves counted after the render (<c>02</c>
/// MSG-SMS-001); for push, the 3,993 bytes of the payload in UTF-8 (<c>02</c> MSG-PUSH-002). A
/// budget counts what the buffers sharing it hold.
/// </summary>
internal sealed class RenderBudget
{
    /// <summary>The most UTF-16 code units ten SMS segments hold (ten of 153 septets, a septet's character a code unit at least).</summary>
    internal const int SmsUnits = 1_530;

    /// <summary>The most bytes a push payload carries (<c>02</c> MSG-PUSH-002).</summary>
    internal const int PushBytes = 3_993;

    private RenderBudget(long limit, bool inBytes)
    {
        Limit = limit;
        InBytes = inBytes;
    }

    /// <summary>The limit, in UTF-8 bytes or in UTF-16 code units.</summary>
    internal long Limit { get; }

    /// <summary>Whether the limit counts UTF-8 bytes rather than UTF-16 code units.</summary>
    internal bool InBytes { get; }

    /// <summary>What the buffers sharing the budget hold.</summary>
    internal long Used { get; private set; }

    internal static RenderBudget Bytes(long limit)
    {
        return new RenderBudget(limit, inBytes: true);
    }

    internal static RenderBudget Units(long limit)
    {
        return new RenderBudget(limit, inBytes: false);
    }

    /// <summary>A budget of the same limit for a buffer of its own, a capture's.</summary>
    internal RenderBudget Fresh()
    {
        return new RenderBudget(Limit, InBytes);
    }

    /// <summary>How much of the limit <paramref name="text"/> takes.</summary>
    internal long Measure(string text)
    {
        return InBytes ? Encoding.UTF8.GetByteCount(text) : text.Length;
    }

    /// <summary>
    /// Takes what <paramref name="text"/> adds to the buffers, answering whether they still hold
    /// no more than the limit. In UTF-8, a high surrogate counts the three bytes of a
    /// lone one, and the low surrogate completing it the one byte more of the pair's four, so a
    /// pair written in two parts counts as it encodes.
    /// </summary>
    internal bool Take(ReadOnlySpan<char> text, ref bool afterHigh)
    {
        long added = 0;
        if (!InBytes)
        {
            added = text.Length;
        }
        else
        {
            foreach (char character in text)
            {
                if (char.IsLowSurrogate(character) && afterHigh)
                {
                    added += 1;
                }
                else
                {
                    added += character < 0x80 ? 1 : character < 0x800 ? 2 : 3;
                }

                afterHigh = char.IsHighSurrogate(character);
            }
        }

        Used += added;
        return Used <= Limit;
    }
}