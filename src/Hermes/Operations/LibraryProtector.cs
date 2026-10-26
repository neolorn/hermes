using System;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Sending;

namespace Hermes.Operations;

/// <summary>
/// The library's own protector, <c>Id</c> <c>messaging</c> (<c>01</c> MSG-SEND-018): the
/// <c>contentKeys</c> version (4 bytes, big-endian), a random 12-byte nonce, the AES-256-GCM
/// ciphertext and its 16-byte tag, its associated data the context's UTF-8 bytes, a zero byte
/// and the person reference's, so a value opens only in its row and for its person.
/// </summary>
internal sealed class LibraryProtector(HeldKeys keys, ContentKeyReservations reservations)
{
    internal const string LibraryId = "messaging";

    private const int VersionLength = 4;

    private const int NonceLength = 12;

    private const int TagLength = 16;

    /// <summary>The protector's output for <paramref name="plaintext"/>, or the <c>contentKeys</c> version that is unavailable.</summary>
    internal async ValueTask<ContentProtecting> ProtectAsync(string person, ReadOnlyMemory<byte> plaintext, string context, CancellationToken cancellationToken)
    {
        if (keys.Find(KeyNeeds.ContentKeys) is not { } key)
        {
            return ContentProtecting.Unavailable(KeyNeeds.ContentKeys, null);
        }

        if (!await reservations.TakeAsync(key.WriteVersion, cancellationToken).ConfigureAwait(false))
        {
            return ContentProtecting.Unavailable(KeyNeeds.ContentKeys, key.WriteVersion);
        }

        byte[] output = new byte[VersionLength + NonceLength + plaintext.Length + TagLength];
        Span<byte> span = output;
        BinaryPrimitives.WriteInt32BigEndian(span[..VersionLength], key.WriteVersion);
        Span<byte> nonce = span.Slice(VersionLength, NonceLength);
        RandomNumberGenerator.Fill(nonce);
        using AesGcm aes = new(key.Write, TagLength);
        aes.Encrypt(nonce, plaintext.Span, span.Slice(VersionLength + NonceLength, plaintext.Length), span[^TagLength..], Associated(person, context));
        return new ContentProtecting(output, null, null);
    }

    /// <summary>Opens a value <see cref="ProtectAsync"/> made, under the same person and context.</summary>
    internal ValueTask<UnprotectResult> UnprotectAsync(string person, ReadOnlyMemory<byte> protectedValue, string context)
    {
        ReadOnlySpan<byte> value = protectedValue.Span;
        if (value.Length < VersionLength + NonceLength + TagLength)
        {
            return ValueTask.FromResult<UnprotectResult>(new UnprotectResult.Rejected());
        }

        int version = BinaryPrimitives.ReadInt32BigEndian(value[..VersionLength]);
        if (keys.Find(KeyNeeds.ContentKeys)?.Versions.TryGetValue(version, out byte[]? bytes) is not true || bytes is null)
        {
            return ValueTask.FromResult<UnprotectResult>(new UnprotectResult.KeyUnavailable());
        }

        int length = value.Length - VersionLength - NonceLength - TagLength;
        byte[] plaintext = new byte[length];
        using AesGcm aes = new(bytes, TagLength);
        try
        {
            aes.Decrypt(value.Slice(VersionLength, NonceLength), value.Slice(VersionLength + NonceLength, length), value[^TagLength..], plaintext, Associated(person, context));
        }
        catch (AuthenticationTagMismatchException)
        {
            return ValueTask.FromResult<UnprotectResult>(new UnprotectResult.Rejected());
        }

        return ValueTask.FromResult<UnprotectResult>(new UnprotectResult.Opened(plaintext));
    }

    private static byte[] Associated(string person, string context)
    {
        byte[] associated = new byte[Encoding.UTF8.GetByteCount(context) + 1 + Encoding.UTF8.GetByteCount(person)];
        int written = Encoding.UTF8.GetBytes(context, associated);
        Encoding.UTF8.GetBytes(person, associated.AsSpan(written + 1));
        return associated;
    }
}