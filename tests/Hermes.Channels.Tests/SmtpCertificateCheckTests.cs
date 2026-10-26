using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

using Xunit;

namespace Hermes.Channels.Tests;

[Trait("Category", "unit")]
public sealed class SmtpCertificateCheckTests
{
    private static readonly TimeSpan ShortLived = TimeSpan.FromDays(7);

    [Fact]
    public void MSG_EMAIL_001_AC2_CertificateOfTheTrustedAuthorityValidatesAndOneOfAnotherDoesNot()
    {
        using TestAuthority trusted = TestAuthority.Create();
        using TestAuthority other = TestAuthority.Create();
        X509Certificate2 issued = trusted.Issue(SmtpHarness.Host, TimeSpan.FromDays(6));
        X509Certificate2 foreign = other.Issue(SmtpHarness.Host, TimeSpan.FromDays(6));

        Assert.True(Check(trusted, EmailRevocation.Published).IsValid(issued, []));
        Assert.False(Check(trusted, EmailRevocation.Published).IsValid(foreign, []));
    }

    [Fact]
    public void MSG_EMAIL_001_AC2_TrustedAuthorityIsTheOnlyAnchorEvenWhenTheChainSentReachesAnother()
    {
        using TestAuthority trusted = TestAuthority.Create();
        using TestAuthority other = TestAuthority.Create();
        using TestAuthority intermediate = other.Intermediate(null);
        X509Certificate2 leaf = intermediate.Issue(SmtpHarness.Host, TimeSpan.FromDays(6));

        Assert.False(Check(trusted, EmailRevocation.None).IsValid(leaf, [intermediate.Certificate, other.Certificate]));
        Assert.True(Check(other, EmailRevocation.None).IsValid(leaf, [intermediate.Certificate]));
    }

    [Fact]
    public void MSG_EMAIL_001_AC3_SixDayLeafNamingNoRevocationSourceValidatesAndANinetyDayOneDoesNot()
    {
        using TestAuthority authority = TestAuthority.Create();
        X509Certificate2 sixDays = authority.Issue(SmtpHarness.Host, TimeSpan.FromDays(6));
        X509Certificate2 ninetyDays = authority.Issue(SmtpHarness.Host, TimeSpan.FromDays(90));

        Assert.True(Check(authority, EmailRevocation.Published).IsValid(sixDays, []));
        Assert.False(Check(authority, EmailRevocation.Published).IsValid(ninetyDays, []));
    }

    [Fact]
    public void IsValid_LeafLongerThanShortLivedMaxValidity_DoesNotValidate()
    {
        using TestAuthority authority = TestAuthority.Create();
        X509Certificate2 threeDays = authority.Issue(SmtpHarness.Host, TimeSpan.FromDays(3));

        Assert.False(new SmtpCertificateCheck(SmtpHarness.Host, authority.Certificate, EmailRevocation.Published, TimeSpan.FromDays(2), TimeSpan.FromSeconds(10), TimeProvider.System).IsValid(threeDays, []));
    }

    [Fact]
    public void IsValid_TrustedAuthorityPublishingNoRevocation_AcceptsALeafNamingNoSource()
    {
        using TestAuthority authority = TestAuthority.Create();
        X509Certificate2 ninetyDays = authority.Issue(SmtpHarness.Host, TimeSpan.FromDays(90));

        Assert.True(Check(authority, EmailRevocation.None).IsValid(ninetyDays, []));
    }

    [Fact]
    public void IsValid_IntermediateNamingNoSourceUnderAnAuthorityThatPublishes_DoesNotValidate()
    {
        using TestAuthority authority = TestAuthority.Create();
        using TestAuthority intermediate = authority.Intermediate(null);
        X509Certificate2 leaf = intermediate.Issue(SmtpHarness.Host, TimeSpan.FromDays(6));

        Assert.False(Check(authority, EmailRevocation.Published).IsValid(leaf, [intermediate.Certificate]));
        Assert.True(Check(authority, EmailRevocation.None).IsValid(leaf, [intermediate.Certificate]));
    }

    [Fact]
    public void IsValid_CertificateForAnotherHost_DoesNotValidate()
    {
        using TestAuthority authority = TestAuthority.Create();
        X509Certificate2 other = authority.Issue("smtp.example.net", TimeSpan.FromDays(6));

        Assert.False(Check(authority, EmailRevocation.None).IsValid(other, []));
    }

    [Fact]
    public void IsValid_CertificateNotForServerAuthentication_DoesNotValidate()
    {
        using TestAuthority authority = TestAuthority.Create();
        X509Certificate2 client = authority.Issue(SmtpHarness.Host, TimeSpan.FromDays(6), serverAuthentication: false);

        Assert.False(Check(authority, EmailRevocation.None).IsValid(client, []));
    }

    [Fact]
    public void IsValid_NoTrustedAuthorityAndATestAuthoritysChain_DoesNotValidate()
    {
        using TestAuthority authority = TestAuthority.Create();
        X509Certificate2 leaf = authority.Issue(SmtpHarness.Host, TimeSpan.FromDays(6));

        Assert.False(new SmtpCertificateCheck(SmtpHarness.Host, null, EmailRevocation.Published, ShortLived, TimeSpan.FromSeconds(10), TimeProvider.System).IsValid(leaf, [authority.Certificate]));
    }

    [Fact]
    public void IsValid_CertificateExpiredAtTheClocksInstant_DoesNotValidate()
    {
        using TestAuthority authority = TestAuthority.Create();
        X509Certificate2 leaf = authority.Issue(SmtpHarness.Host, TimeSpan.FromDays(6));
        ManualClock later = new(DateTimeOffset.UtcNow.AddDays(7));

        Assert.False(new SmtpCertificateCheck(SmtpHarness.Host, authority.Certificate, EmailRevocation.None, ShortLived, TimeSpan.FromSeconds(10), later).IsValid(leaf, []));
    }

    [Fact]
    public async Task IsValid_RevocationSourceAnsweringAGoodStatus_Validates()
    {
        await using CrlServerFake crls = await CrlServerFake.StartAsync();
        using TestAuthority authority = TestAuthority.Create();
        Uri crl = crls.Reserve();
        X509Certificate2 leaf = authority.Issue(SmtpHarness.Host, TimeSpan.FromDays(90), crl);
        crls.Publish(crl, authority.Crl());

        Assert.True(Check(authority, EmailRevocation.Published).IsValid(leaf, []));
        Assert.True(crls.Requests > 0);
    }

    [Fact]
    public async Task IsValid_RevocationSourceListingTheCertificate_DoesNotValidate()
    {
        await using CrlServerFake crls = await CrlServerFake.StartAsync();
        using TestAuthority authority = TestAuthority.Create();
        Uri crl = crls.Reserve();
        X509Certificate2 leaf = authority.Issue(SmtpHarness.Host, TimeSpan.FromDays(90), crl);
        crls.Publish(crl, authority.Crl(leaf));

        Assert.False(Check(authority, EmailRevocation.Published).IsValid(leaf, []));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task IsValid_RevocationSourceThatCannotBeRead_DoesNotValidate(bool authorityPublishes)
    {
        await using CrlServerFake crls = await CrlServerFake.StartAsync();
        using TestAuthority authority = TestAuthority.Create();
        X509Certificate2 shortLived = authority.Issue(SmtpHarness.Host, TimeSpan.FromDays(6), crls.Reserve());

        Assert.False(Check(authority, authorityPublishes ? EmailRevocation.Published : EmailRevocation.None).IsValid(shortLived, []));
    }

    [Fact]
    public void IsValid_NoCertificate_DoesNotValidate()
    {
        using TestAuthority authority = TestAuthority.Create();

        Assert.False(Check(authority, EmailRevocation.None).IsValid((X509Certificate?)null, null));
    }

    private static SmtpCertificateCheck Check(TestAuthority authority, EmailRevocation revocation)
    {
        return new SmtpCertificateCheck(SmtpHarness.Host, authority.Certificate, revocation, ShortLived, TimeSpan.FromSeconds(10), TimeProvider.System);
    }
}