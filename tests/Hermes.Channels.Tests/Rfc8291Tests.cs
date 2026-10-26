using System.Buffers.Text;
using System.Text;

using Xunit;

namespace Hermes.Channels.Tests;

[Trait("Category", "unit")]
public sealed class Rfc8291Tests
{
    [Fact]
    public void Derive_Section5Example_ReproducesAppendixA()
    {
        Rfc8291.Derivation derived = Rfc8291.Derive(
            Bytes(Vectors.Salt),
            Bytes(Vectors.ReceiverPrivateKey),
            Bytes(Vectors.ReceiverPublicKey),
            Bytes(Vectors.SenderPublicKey),
            Bytes(Vectors.AuthenticationSecret));

        Assert.Equal(Vectors.Ikm, Base64Url.EncodeToString(derived.Ikm));
        Assert.Equal(Vectors.Prk, Base64Url.EncodeToString(derived.Prk));
        Assert.Equal(Vectors.Cek, Base64Url.EncodeToString(derived.Cek));
        Assert.Equal(Vectors.Nonce, Base64Url.EncodeToString(derived.Nonce));
    }

    [Fact]
    public void Decrypt_Section5Message_IsItsPlaintext()
    {
        byte[] plaintext = Rfc8291.Decrypt(Bytes(Vectors.Message), Bytes(Vectors.ReceiverPrivateKey), Bytes(Vectors.ReceiverPublicKey), Bytes(Vectors.AuthenticationSecret));

        Assert.Equal(Vectors.Plaintext, Encoding.ASCII.GetString(plaintext));
    }

    private static byte[] Bytes(string base64Url)
    {
        return Base64Url.DecodeFromChars(base64Url);
    }
}