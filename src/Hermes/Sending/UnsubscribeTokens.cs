using System;
using System.Buffers.Binary;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

using Hermes.Operations;

namespace Hermes.Sending;

/// <summary>
/// The unsubscribe token of <c>05</c> MSG-API-005: the format byte, the <c>tokenKeys</c> and
/// <c>hashKeys</c> versions, the category, the language, the day it is dated from and the
/// destination's keyed hash, signed by HMAC-SHA-256 under that <c>tokenKeys</c> version and
/// written as base64url without padding; a layout two releases share and never change.
/// </summary>
internal sealed class UnsubscribeTokens(HeldKeys keys)
{
    /// <summary>The format this release makes and reads.</summary>
    internal const byte Format = 0x01;

    /// <summary>The last format byte kept for a later release.</summary>
    internal const byte LastLaterFormat = 0x0F;

    private const int HashBytes = 32;

    private const int MostNameBytes = 255;

    /// <summary>
    /// Makes the token of <paramref name="emailHash"/>, made under <paramref name="hashVersion"/>,
    /// for <paramref name="category"/> in <paramref name="language"/>, dated from
    /// <paramref name="from"/>, signed under the <c>tokenKeys</c> write version.
    /// </summary>
    internal string Make(int hashVersion, string category, string language, DateTimeOffset from, byte[] emailHash)
    {
        HeldKey held = Held;
        byte[] categoryBytes = Encoding.UTF8.GetBytes(category);
        byte[] languageBytes = Encoding.UTF8.GetBytes(language);
        if (categoryBytes.Length > MostNameBytes || languageBytes.Length > MostNameBytes || emailHash.Length != HashBytes)
        {
            throw new ArgumentException("A token's category and language are at most 255 bytes, and its hash 32.");
        }

        int signed = 1 + 4 + 4 + 1 + categoryBytes.Length + 1 + languageBytes.Length + 4 + HashBytes;
        byte[] token = new byte[signed + HashBytes];
        Span<byte> span = token;
        span[0] = Format;
        BinaryPrimitives.WriteInt32BigEndian(span[1..], held.WriteVersion);
        BinaryPrimitives.WriteInt32BigEndian(span[5..], hashVersion);
        int at = 9;
        at = Write(span, at, categoryBytes);
        at = Write(span, at, languageBytes);
        BinaryPrimitives.WriteInt32BigEndian(span[at..], Day(from));
        emailHash.CopyTo(span[(at + 4)..]);
        HMACSHA256.HashData(held.Write, token.AsSpan(0, signed), span[signed..]);
        return Base64Url.EncodeToString(token);
    }

    /// <summary>
    /// Reads <paramref name="text"/> as a token at <paramref name="now"/>: one that verifies, one
    /// naming a version this process does not hold, one of a format kept for a later release,
    /// or one that does not verify.
    /// </summary>
    internal TokenReading Read(string text, DateTimeOffset now, TimeSpan lifetime, Func<string, bool> isOptionalCategory)
    {
        byte[] bytes;
        try
        {
            bytes = Base64Url.DecodeFromChars(text);
        }
        catch (FormatException)
        {
            return TokenReading.Invalid;
        }

        if (bytes.Length == 0 || !string.Equals(Base64Url.EncodeToString(bytes), text, StringComparison.Ordinal))
        {
            return TokenReading.Invalid;
        }

        if (bytes[0] is > Format and <= LastLaterFormat)
        {
            return TokenReading.Later;
        }

        if (bytes[0] != Format || !Parse(bytes, out Fields fields))
        {
            return TokenReading.Invalid;
        }

        if (!Held.Versions.TryGetValue(fields.TokenVersion, out byte[]? key))
        {
            return TokenReading.NotHeld(KeyNeeds.TokenKeys, fields.TokenVersion);
        }

        if (keys.Find(KeyNeeds.HashKeys) is not { } hashKeys || !hashKeys.Versions.ContainsKey(fields.HashVersion))
        {
            return TokenReading.NotHeld(KeyNeeds.HashKeys, fields.HashVersion);
        }

        Span<byte> mac = stackalloc byte[HashBytes];
        HMACSHA256.HashData(key, bytes.AsSpan(0, bytes.Length - HashBytes), mac);
        if (!CryptographicOperations.FixedTimeEquals(mac, bytes.AsSpan(bytes.Length - HashBytes)))
        {
            return TokenReading.Invalid;
        }

        if ((long)Day(now) - fields.Day > (long)Math.Floor(lifetime.TotalDays) || !isOptionalCategory(fields.Category))
        {
            return TokenReading.Invalid;
        }

        return TokenReading.Valid(fields.TokenVersion, fields.HashVersion, fields.Category, fields.Language, fields.Hash);
    }

    /// <summary>
    /// The <c>tokenKeys</c> and <c>hashKeys</c> versions the token at the end of the unsubscribe
    /// address <paramref name="address"/> names, or none where it is not of this release's format.
    /// </summary>
    internal static (int TokenVersion, int HashVersion)? Versions(string address)
    {
        byte[] bytes;
        try
        {
            bytes = Base64Url.DecodeFromChars(address.AsSpan(address.LastIndexOf('/') + 1));
        }
        catch (FormatException)
        {
            return null;
        }

        if (bytes.Length < 9 || bytes[0] != Format)
        {
            return null;
        }

        return (BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(1)), BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(5)));
    }

    private HeldKey Held => keys.Find(KeyNeeds.TokenKeys) ?? throw new InvalidOperationException("An unsubscribe token is used before the startup checks read tokenKeys.");

    /// <summary>The days since 1970-01-01 UTC of <paramref name="instant"/>.</summary>
    private static int Day(DateTimeOffset instant)
    {
        return (int)(instant.UtcTicks / TimeSpan.TicksPerDay - DateTimeOffset.UnixEpoch.UtcTicks / TimeSpan.TicksPerDay);
    }

    private static int Write(Span<byte> span, int at, byte[] bytes)
    {
        span[at] = (byte)bytes.Length;
        bytes.CopyTo(span[(at + 1)..]);
        return at + 1 + bytes.Length;
    }

    private static bool Parse(byte[] bytes, out Fields fields)
    {
        fields = default;
        ReadOnlySpan<byte> span = bytes;
        int at = 9;
        if (span.Length < at + 1)
        {
            return false;
        }

        int tokenVersion = BinaryPrimitives.ReadInt32BigEndian(span[1..]);
        int hashVersion = BinaryPrimitives.ReadInt32BigEndian(span[5..]);
        if (!ReadName(span, ref at, out string? category) || !ReadName(span, ref at, out string? language))
        {
            return false;
        }

        if (span.Length != at + 4 + HashBytes + HashBytes)
        {
            return false;
        }

        int day = BinaryPrimitives.ReadInt32BigEndian(span[at..]);
        fields = new Fields(tokenVersion, hashVersion, category!, language!, day, span.Slice(at + 4, HashBytes).ToArray());
        return true;
    }

    private static bool ReadName(ReadOnlySpan<byte> span, ref int at, out string? name)
    {
        name = null;
        if (span.Length <= at || span.Length < at + 1 + span[at])
        {
            return false;
        }

        int length = span[at];
        try
        {
            name = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(span.Slice(at + 1, length));
        }
        catch (ArgumentException)
        {
            return false;
        }

        at += 1 + length;
        return true;
    }

    private readonly record struct Fields(int TokenVersion, int HashVersion, string Category, string Language, int Day, byte[] Hash);
}