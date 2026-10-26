using System;
using System.Text;

namespace Hermes.Sending;

/// <summary>
/// The envelope a stored value is kept in (<c>01</c> MSG-SEND-018; <c>08</c> section 8): the
/// format byte <c>0x01</c>, the protector's <c>Id</c> as its length in one byte and its ASCII
/// bytes, then the protector's output.
/// </summary>
internal static class ContentEnvelope
{
    private const byte Format = 0x01;

    /// <summary>The first and last format bytes kept for later releases.</summary>
    private const byte FirstLater = 0x02;

    private const byte LastLater = 0x0F;

    internal static byte[] Wrap(string id, ReadOnlySpan<byte> output)
    {
        byte[] envelope = new byte[2 + id.Length + output.Length];
        envelope[0] = Format;
        envelope[1] = (byte)id.Length;
        Encoding.ASCII.GetBytes(id, envelope.AsSpan(2));
        output.CopyTo(envelope.AsSpan(2 + id.Length));
        return envelope;
    }

    /// <summary>
    /// Reads <paramref name="stored"/>: the protector it names and its output, or the format
    /// kept for a later release it is in, or that it is in no format.
    /// </summary>
    internal static Envelope Read(ReadOnlyMemory<byte> stored)
    {
        ReadOnlySpan<byte> bytes = stored.Span;
        if (bytes.IsEmpty)
        {
            return Envelope.None;
        }

        if (bytes[0] is >= FirstLater and <= LastLater)
        {
            return Envelope.Later;
        }

        if (bytes[0] != Format || bytes.Length < 2)
        {
            return Envelope.None;
        }

        int length = bytes[1];
        if (length is < 1 or > 32 || bytes.Length < 2 + length)
        {
            return Envelope.None;
        }

        ReadOnlySpan<byte> id = bytes.Slice(2, length);
        foreach (byte character in id)
        {
            if (character is not ((>= (byte)'a' and <= (byte)'z') or (>= (byte)'0' and <= (byte)'9')))
            {
                return Envelope.None;
            }
        }

        return new Envelope(EnvelopeForm.Value, Encoding.ASCII.GetString(id), stored[(2 + length)..]);
    }

    internal enum EnvelopeForm
    {
        /// <summary>In format <c>0x01</c>.</summary>
        Value,

        /// <summary>In a format kept for a later release.</summary>
        Later,

        /// <summary>In no format.</summary>
        None,
    }

    /// <summary>A stored value read.</summary>
    /// <param name="Form">Its format.</param>
    /// <param name="Id">The protector it names, in format <c>0x01</c>.</param>
    /// <param name="Output">The protector's output, in format <c>0x01</c>.</param>
    internal readonly record struct Envelope(EnvelopeForm Form, string Id, ReadOnlyMemory<byte> Output)
    {
        internal static Envelope Later => new(EnvelopeForm.Later, string.Empty, ReadOnlyMemory<byte>.Empty);

        internal static Envelope None => new(EnvelopeForm.None, string.Empty, ReadOnlyMemory<byte>.Empty);
    }
}