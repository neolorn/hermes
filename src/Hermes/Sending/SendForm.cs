using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

using Hermes.Core;

namespace Hermes.Sending;

/// <summary>
/// The canonical form of a send (<c>01</c> MSG-SEND-009), whose keyed hash is an idempotency
/// row's digest: each member as the host gave it, before any default is applied.
/// </summary>
internal static class SendForm
{
    private const byte Format = 0x01;

    private const byte Absent = 0x00;

    private const byte Text = 0x01;

    private const byte Person = 0x02;

    private const byte Anonymous = 0x03;

    private const byte Whole = 0x04;

    private const byte Decimal = 0x05;

    private const byte Boolean = 0x06;

    private const byte Instant = 0x07;

    private const byte List = 0x08;

    private const byte Map = 0x09;

    private const byte Channels = 0x0A;

    /// <summary>The bytes of <paramref name="send"/>'s canonical form.</summary>
    internal static byte[] Of(Send send)
    {
        ArgumentNullException.ThrowIfNull(send);
        using MemoryStream form = new();
        form.WriteByte(Format);
        String(form, send.Kind);
        if (send.Recipient is Recipient.Person person)
        {
            form.WriteByte(Person);
            Written(form, person.Reference);
        }
        else
        {
            form.WriteByte(Anonymous);
        }

        String(form, send.Email);
        String(form, send.Phone);
        String(form, send.Language);
        String(form, send.TimeZone);
        form.WriteByte(Map);
        Entries(form, send.Values);
        if (send.Channels is null)
        {
            form.WriteByte(Absent);
        }
        else
        {
            form.WriteByte(Channels);
            Count(form, send.Channels.Count);
            foreach (string channel in send.Channels.Select(Spellings.Of).Order(StringComparer.Ordinal))
            {
                Written(form, channel);
            }
        }

        if (send.NotBefore is { } notBefore)
        {
            form.WriteByte(Instant);
            Long(form, notBefore.UtcTicks);
        }
        else
        {
            form.WriteByte(Absent);
        }

        String(form, send.PushTopic);
        return form.ToArray();
    }

    /// <summary>A decimal number's invariant form, its fraction's trailing zeros and a trailing point removed.</summary>
    internal static string DecimalText(decimal value)
    {
        string text = value.ToString(CultureInfo.InvariantCulture);
        return text.Contains('.', StringComparison.Ordinal) ? text.TrimEnd('0').TrimEnd('.') : text;
    }

    private static void String(MemoryStream form, string? value)
    {
        if (value is null)
        {
            form.WriteByte(Absent);
            return;
        }

        form.WriteByte(Text);
        Written(form, value);
    }

    private static void Entries(MemoryStream form, IReadOnlyDictionary<string, SendValue> values)
    {
        Count(form, values.Count);
        foreach (KeyValuePair<string, SendValue> entry in values.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            Written(form, entry.Key);
            Value(form, entry.Value);
        }
    }

    private static void Value(MemoryStream form, SendValue value)
    {
        switch (value)
        {
            case SendValue.PlainText text:
                form.WriteByte(Text);
                Written(form, text.Value);
                break;
            case SendValue.WholeNumber whole:
                form.WriteByte(Whole);
                Long(form, whole.Value);
                break;
            case SendValue.DecimalNumber number:
                form.WriteByte(Decimal);
                Written(form, DecimalText(number.Value));
                break;
            case SendValue.Flag flag:
                form.WriteByte(Boolean);
                form.WriteByte(flag.Value ? (byte)0x01 : (byte)0x00);
                break;
            case SendValue.Instant instant:
                form.WriteByte(Instant);
                Long(form, instant.Value.UtcTicks);
                break;
            case SendValue.List list:
                form.WriteByte(List);
                Count(form, list.Items.Count);
                foreach (IReadOnlyDictionary<string, SendValue> item in list.Items)
                {
                    Entries(form, item);
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(value), value.GetType().Name, "A send value of no known form.");
        }
    }

    private static void Written(MemoryStream form, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        Count(form, bytes.Length);
        form.Write(bytes);
    }

    private static void Count(MemoryStream form, int count)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(bytes, count);
        form.Write(bytes);
    }

    private static void Long(MemoryStream form, long value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        form.Write(bytes);
    }
}