using System;
using System.Buffers.Binary;
using System.Buffers.Text;

namespace Hermes.Sending;

/// <summary>
/// The cursor of a page of the in-app list (<c>02</c> MSG-INAPP-003), opaque to the host: the
/// instant written, in microseconds since the Unix epoch, and the identifier of the last entry
/// of the page before, 24 bytes written base64url without padding.
/// </summary>
internal static class InAppCursor
{
    private const int Length = 24;

    private const long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1_000;

    /// <summary>The cursor that reads the entries after the one written at <paramref name="writtenAt"/> with the identifier <paramref name="entry"/>.</summary>
    internal static string Write(DateTimeOffset writtenAt, Guid entry)
    {
        Span<byte> bytes = stackalloc byte[Length];
        BinaryPrimitives.WriteInt64BigEndian(bytes, (writtenAt.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) / TicksPerMicrosecond);
        entry.TryWriteBytes(bytes[sizeof(long)..], bigEndian: true, out _);
        return Base64Url.EncodeToString(bytes);
    }

    /// <summary>The instant and identifier <paramref name="cursor"/> holds, or none where it does not read.</summary>
    internal static (DateTimeOffset WrittenAt, Guid Entry)? Read(string cursor)
    {
        Span<byte> bytes = stackalloc byte[Length + 3];
        if (cursor.Length != Base64Url.GetEncodedLength(Length)
            || !Base64Url.TryDecodeFromChars(cursor, bytes, out int written)
            || written != Length
            || !string.Equals(Base64Url.EncodeToString(bytes[..Length]), cursor, StringComparison.Ordinal))
        {
            return null;
        }

        long microseconds = BinaryPrimitives.ReadInt64BigEndian(bytes);
        if (microseconds < 0 || microseconds > (DateTimeOffset.MaxValue.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) / TicksPerMicrosecond)
        {
            return null;
        }

        long ticks = DateTimeOffset.UnixEpoch.UtcTicks + (microseconds * TicksPerMicrosecond);
        return (new DateTimeOffset(ticks, TimeSpan.Zero), new Guid(bytes[sizeof(long)..Length], bigEndian: true));
    }
}