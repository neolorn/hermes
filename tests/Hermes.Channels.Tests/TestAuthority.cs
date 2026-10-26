using System;
using System.Collections.Generic;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Hermes.Channels.Tests;

/// <summary>
/// A certificate authority made at run time (<c>07</c>): its P-256 key exists only in the
/// test process, and its name is new each time so no revocation list cached for another
/// test's authority is ever read for it.
/// </summary>
internal sealed class TestAuthority : IDisposable
{
    private const string ServerAuthentication = "1.3.6.1.5.5.7.3.1";

    private const string ClientAuthentication = "1.3.6.1.5.5.7.3.2";

    private readonly List<X509Certificate2> issued = [];

    private BigInteger crlNumber = BigInteger.One;

    private TestAuthority(X509Certificate2 certificate)
    {
        Certificate = certificate;
    }

    /// <summary>The authority's certificate, with its private key.</summary>
    internal X509Certificate2 Certificate { get; }

    /// <summary>The authority's certificate as PEM, as <c>Email:TrustedAuthority</c> takes it.</summary>
    internal string Pem => Certificate.ExportCertificatePem();

    /// <summary>A self-signed authority.</summary>
    internal static TestAuthority Create()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        CertificateRequest request = new(Name("Hermes Test Authority"), key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new TestAuthority(request.CreateSelfSigned(now.AddDays(-1), now.AddYears(1)));
    }

    /// <summary>An intermediate authority this one issues, naming <paramref name="crl"/> as its revocation source where given.</summary>
    internal TestAuthority Intermediate(Uri? crl)
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        CertificateRequest request = new(Name("Hermes Test Intermediate"), key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        AddIssuer(request, crl);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        using X509Certificate2 bare = request.Create(Certificate, now.AddHours(-1), now.AddDays(90), Serial());
        X509Certificate2 intermediate = bare.CopyWithPrivateKey(key);
        issued.Add(intermediate);
        return new TestAuthority(intermediate);
    }

    /// <summary>
    /// A server certificate for <paramref name="host"/>, valid for <paramref name="validity"/>
    /// from a minute ago, naming <paramref name="crl"/> as its revocation source where given,
    /// with its private key in a form any platform's TLS takes.
    /// </summary>
    internal X509Certificate2 Issue(string host, TimeSpan validity, Uri? crl = null, bool serverAuthentication = true)
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        CertificateRequest request = new(new X500DistinguishedName("CN=" + host), key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid(serverAuthentication ? ServerAuthentication : ClientAuthentication)], false));
        SubjectAlternativeNameBuilder names = new();
        names.AddDnsName(host);
        request.CertificateExtensions.Add(names.Build());
        AddIssuer(request, crl);
        DateTimeOffset notBefore = DateTimeOffset.UtcNow.AddMinutes(-1);
        using X509Certificate2 bare = request.Create(Certificate, notBefore, notBefore + validity, Serial());
        using X509Certificate2 withKey = bare.CopyWithPrivateKey(key);
        X509Certificate2 leaf = X509CertificateLoader.LoadPkcs12(withKey.Export(X509ContentType.Pkcs12), null);
        issued.Add(leaf);
        return leaf;
    }

    /// <summary>A revocation list of this authority's, valid for a day, listing <paramref name="revoked"/>.</summary>
    internal byte[] Crl(params X509Certificate2[] revoked)
    {
        CertificateRevocationListBuilder builder = new();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        foreach (X509Certificate2 certificate in revoked)
        {
            builder.AddEntry(certificate, now.AddMinutes(-1));
        }

        byte[] crl = builder.Build(Certificate, crlNumber, now.AddDays(1), HashAlgorithmName.SHA256, thisUpdate: now.AddMinutes(-1));
        crlNumber++;
        return crl;
    }

    public void Dispose()
    {
        foreach (X509Certificate2 certificate in issued)
        {
            certificate.Dispose();
        }

        Certificate.Dispose();
    }

    private static X500DistinguishedName Name(string name)
    {
        return new X500DistinguishedName("CN=" + name + " " + Guid.NewGuid().ToString("N"));
    }

    private static byte[] Serial()
    {
        byte[] serial = RandomNumberGenerator.GetBytes(16);
        serial[0] &= 0x7F;
        return serial;
    }

    private void AddIssuer(CertificateRequest request, Uri? crl)
    {
        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(Certificate, true, false));
        if (crl is not null)
        {
            request.CertificateExtensions.Add(CertificateRevocationListBuilder.BuildCrlDistributionPointExtension([crl.AbsoluteUri]));
        }
    }
}