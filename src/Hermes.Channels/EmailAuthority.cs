using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Hermes.Channels;

/// <summary>The rule of <c>Email:TrustedAuthority</c>: one PEM certificate (<c>08</c> section 4).</summary>
internal static class EmailAuthority
{
    /// <summary>Whether <paramref name="text"/> holds one PEM certificate and nothing else PEM.</summary>
    internal static bool IsAuthority(string text)
    {
        using X509Certificate2? authority = Read(text);
        return authority is not null;
    }

    /// <summary>The certificate <paramref name="text"/> holds, or none where it holds no PEM certificate, more than one PEM block, or one that does not read.</summary>
    internal static X509Certificate2? Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!PemEncoding.TryFind(text, out PemFields fields) || !text.AsSpan()[fields.Label].SequenceEqual("CERTIFICATE"))
        {
            return null;
        }

        if (PemEncoding.TryFind(text.AsSpan()[fields.Location.End..], out _))
        {
            return null;
        }

        try
        {
            byte[] der = Convert.FromBase64String(text[fields.Base64Data]);
            return X509CertificateLoader.LoadCertificate(der);
        }
        catch (Exception failure) when (failure is CryptographicException or FormatException)
        {
            return null;
        }
    }
}