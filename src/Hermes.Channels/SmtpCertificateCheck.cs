using System;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Hermes.Channels;

/// <summary>
/// The library's own check of the submission server's certificate (<c>02</c> MSG-EMAIL-001):
/// its name is <c>Email:Host</c>; its chain builds, for server authentication, to the system's
/// authorities or to <c>Email:TrustedAuthority</c> alone; every certificate below the
/// authority that names a revocation source has its status read there, online, and good; one
/// that names none is accepted only as a short-lived leaf, or below a trusted authority that
/// publishes no revocation. No setting turns the check off.
/// </summary>
internal sealed class SmtpCertificateCheck(string host, X509Certificate2? authority, EmailRevocation revocation, TimeSpan shortLived, TimeSpan retrieval, TimeProvider time)
{
    private const string ServerAuthentication = "1.3.6.1.5.5.7.3.1";

    private const string CrlDistributionPoints = "2.5.29.31";

    private const string AuthorityInformationAccess = "1.3.6.1.5.5.7.1.1";

    /// <summary>Whether <paramref name="certificate"/>, presented with <paramref name="presented"/>, passes the check.</summary>
    internal bool IsValid(X509Certificate? certificate, X509Chain? presented)
    {
        if (certificate is null)
        {
            return false;
        }

        using X509Certificate2 leaf = X509CertificateLoader.LoadCertificate(certificate.GetRawCertData());
        X509Certificate2Collection sent = [];
        if (presented is not null)
        {
            sent.AddRange(presented.ChainPolicy.ExtraStore);
            foreach (X509ChainElement element in presented.ChainElements.Skip(1))
            {
                sent.Add(element.Certificate);
            }
        }

        return IsValid(leaf, sent);
    }

    /// <summary>Whether <paramref name="leaf"/>, sent with the certificates <paramref name="sent"/>, passes the check.</summary>
    internal bool IsValid(X509Certificate2 leaf, X509Certificate2Collection sent)
    {
        ArgumentNullException.ThrowIfNull(leaf);
        if (!leaf.MatchesHostname(host))
        {
            return false;
        }

        using X509Chain chain = new();
        X509ChainPolicy policy = chain.ChainPolicy;
        policy.RevocationMode = X509RevocationMode.Online;
        policy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
        policy.UrlRetrievalTimeout = retrieval;
        policy.VerificationTime = time.GetUtcNow().UtcDateTime;
        policy.ApplicationPolicy.Add(new Oid(ServerAuthentication));
        if (authority is not null)
        {
            policy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            policy.CustomTrustStore.Add(authority);
        }

        policy.ExtraStore.AddRange(sent);
        try
        {
            chain.Build(leaf);
            return Holds(chain);
        }
        finally
        {
            foreach (X509ChainElement element in chain.ChainElements)
            {
                element.Certificate.Dispose();
            }
        }
    }

    /// <summary>Whether a certificate names a CRL distribution point or an OCSP responder.</summary>
    internal static bool NamesRevocationSource(X509Certificate2 certificate)
    {
        if (certificate.Extensions[CrlDistributionPoints] is not null)
        {
            return true;
        }

        return certificate.Extensions[AuthorityInformationAccess] is { } access
            && new X509AuthorityInformationAccessExtension(access.RawData, access.Critical).EnumerateOcspUris().Any();
    }

    private static bool IsRevocationUnknown(X509ChainStatusFlags status)
    {
        return status is X509ChainStatusFlags.RevocationStatusUnknown or X509ChainStatusFlags.OfflineRevocation;
    }

    private bool Holds(X509Chain chain)
    {
        X509ChainElementCollection elements = chain.ChainElements;
        if (elements.Count == 0)
        {
            return false;
        }

        if (authority is not null && !elements[^1].Certificate.RawDataMemory.Span.SequenceEqual(authority.RawDataMemory.Span))
        {
            return false;
        }

        for (int index = 0; index < elements.Count; index++)
        {
            X509ChainElement element = elements[index];
            bool excused = index < elements.Count - 1 && !NamesRevocationSource(element.Certificate) && Excused(index, element.Certificate);
            if (element.ChainElementStatus.Any(status => status.Status != X509ChainStatusFlags.NoError && !(excused && IsRevocationUnknown(status.Status))))
            {
                return false;
            }
        }

        // The chain's own statuses are its elements' and its own, such as a partial chain.
        return chain.ChainStatus.All(status => status.Status == X509ChainStatusFlags.NoError || IsRevocationUnknown(status.Status));
    }

    /// <summary>Whether a certificate below the authority that names no revocation source is accepted.</summary>
    private bool Excused(int index, X509Certificate2 certificate)
    {
        bool shortLivedLeaf = index == 0 && certificate.NotAfter - certificate.NotBefore <= shortLived;
        bool unpublished = authority is not null && revocation == EmailRevocation.None;
        return shortLivedLeaf || unpublished;
    }
}