using System;
using System.Buffers.Text;
using System.Security.Cryptography;

namespace Hermes.Channels;

/// <summary>
/// One version of the secret <c>pushVapidKeys</c>: a P-256 private key as its 32-byte
/// scalar, whose public key, an uncompressed point of 65 bytes, is derived from it, and the
/// version's VAPID tokens (<c>02</c> MSG-PUSH-007). The client takes both keys as base64url
/// text, so each is held once, for the process's life, in that form.
/// </summary>
internal sealed class VapidKey
{
    /// <summary>The order of P-256's base point, above which no scalar is (SEC 2, section 2.4.2).</summary>
    private static readonly byte[] P256Order = Convert.FromHexString("FFFFFFFF00000000FFFFFFFFFFFFFFFFBCE6FAADA7179E84F3B9CAC2FC632551");

    private VapidKey(string publicKey, string privateKey, VapidTokenCache tokens)
    {
        PublicKey = publicKey;
        PrivateKey = privateKey;
        Tokens = tokens;
    }

    /// <summary>The public key, base64url without padding.</summary>
    internal string PublicKey { get; }

    /// <summary>The private key's scalar, base64url without padding.</summary>
    internal string PrivateKey { get; }

    /// <summary>The version's VAPID tokens.</summary>
    internal VapidTokenCache Tokens { get; }

    /// <summary>Whether <paramref name="scalar"/> is 32 bytes, big-endian, from 1 to the order of P-256 less one.</summary>
    internal static bool IsScalar(ReadOnlySpan<byte> scalar)
    {
        return scalar.Length == 32 && scalar.ContainsAnyExcept((byte)0) && scalar.SequenceCompareTo(P256Order) < 0;
    }

    /// <summary>The public key of <paramref name="scalar"/>, an uncompressed point of 65 bytes.</summary>
    internal static byte[] PublicPoint(byte[] scalar)
    {
        using ECDsa pair = ECDsa.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, D = scalar });
        ECPoint point = pair.ExportParameters(includePrivateParameters: false).Q;
        return [0x04, .. point.X!, .. point.Y!];
    }

    /// <summary>The key of <paramref name="scalar"/>, which <see cref="IsScalar"/> holds of.</summary>
    internal static VapidKey FromScalar(byte[] scalar, TimeProvider time)
    {
        return new(Base64Url.EncodeToString(PublicPoint(scalar)), Base64Url.EncodeToString(scalar), new VapidTokenCache(time));
    }

    /// <summary>Prints nothing of the keys.</summary>
    public override string ToString()
    {
        return nameof(VapidKey);
    }
}