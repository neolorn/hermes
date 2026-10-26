using System;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Hermes.Channels.Tests;

/// <summary>
/// The receiver's side of RFC 8291 section 3, written from the RFC and not from the client,
/// so a message the client encrypts is checked against the standard: the keys of section
/// 3.4 over the content coding of RFC 8188, a single record, its padding ending in the
/// delimiter 0x02.
/// </summary>
internal static class Rfc8291
{
    private const int TagLength = 16;

    /// <summary>The plaintext of <paramref name="message"/>, an <c>aes128gcm</c> body of one record.</summary>
    internal static byte[] Decrypt(byte[] message, byte[] receiverPrivateKey, byte[] receiverPublicKey, byte[] authenticationSecret)
    {
        byte[] salt = message[..16];
        uint recordSize = BinaryPrimitives.ReadUInt32BigEndian(message.AsSpan(16, 4));
        int keyIdLength = message[20];
        byte[] senderPublicKey = message[21..(21 + keyIdLength)];
        byte[] record = message[(21 + keyIdLength)..];
        if (record.Length > recordSize)
        {
            throw new CryptographicException("The message holds more than one record.");
        }

        Derivation keys = Derive(salt, receiverPrivateKey, receiverPublicKey, senderPublicKey, authenticationSecret);
        byte[] padded = new byte[record.Length - TagLength];
        using AesGcm cipher = new(keys.Cek, TagLength);
        cipher.Decrypt(keys.Nonce, record.AsSpan(0, padded.Length), record.AsSpan(padded.Length), padded);

        int delimiter = Array.FindLastIndex(padded, octet => octet != 0);
        if (delimiter < 0 || padded[delimiter] != 0x02)
        {
            throw new CryptographicException("The last record does not end in the delimiter 0x02.");
        }

        return padded[..delimiter];
    }

    /// <summary>The values of section 3.4 and RFC 8188 section 2.2 for one message.</summary>
    internal static Derivation Derive(byte[] salt, byte[] receiverPrivateKey, byte[] receiverPublicKey, byte[] senderPublicKey, byte[] authenticationSecret)
    {
        byte[] ecdhSecret = Agree(receiverPrivateKey, receiverPublicKey, senderPublicKey);

        // PRK_key = HMAC-SHA-256(auth_secret, ecdh_secret); IKM = HMAC-SHA-256(PRK_key, key_info || 0x01).
        byte[] prkKey = HMACSHA256.HashData(authenticationSecret, ecdhSecret);
        byte[] keyInfo = [.. "WebPush: info\0"u8, .. receiverPublicKey, .. senderPublicKey];
        byte[] ikm = HMACSHA256.HashData(prkKey, (byte[])[.. keyInfo, 0x01]);

        // PRK = HMAC-SHA-256(salt, IKM); CEK and NONCE the first 16 and 12 octets of their expansions.
        byte[] prk = HMACSHA256.HashData(salt, ikm);
        byte[] cek = HMACSHA256.HashData(prk, (byte[])[.. Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0"), 0x01])[..16];
        byte[] nonce = HMACSHA256.HashData(prk, (byte[])[.. Encoding.ASCII.GetBytes("Content-Encoding: nonce\0"), 0x01])[..12];
        return new Derivation(ecdhSecret, prkKey, ikm, prk, cek, nonce);
    }

    private static byte[] Agree(byte[] receiverPrivateKey, byte[] receiverPublicKey, byte[] senderPublicKey)
    {
        using ECDiffieHellman receiver = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = receiverPrivateKey,
            Q = Point(receiverPublicKey),
        });
        using ECDiffieHellman sender = ECDiffieHellman.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, Q = Point(senderPublicKey) });
        return receiver.DeriveRawSecretAgreement(sender.PublicKey);
    }

    /// <summary>The point of an uncompressed key of 65 octets, 0x04 and its two coordinates.</summary>
    private static ECPoint Point(byte[] key)
    {
        if (key.Length != 65 || key[0] != 0x04)
        {
            throw new CryptographicException("The key is not an uncompressed P-256 point.");
        }

        return new ECPoint { X = key[1..33], Y = key[33..] };
    }

    /// <summary>The intermediate values appendix A prints.</summary>
    internal sealed record Derivation(byte[] EcdhSecret, byte[] PrkKey, byte[] Ikm, byte[] Prk, byte[] Cek, byte[] Nonce);
}