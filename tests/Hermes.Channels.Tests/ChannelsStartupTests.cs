using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Xunit;

namespace Hermes.Channels.Tests;

[Trait("Category", "unit")]
public sealed class ChannelsStartupTests
{
    public static TheoryData<int, byte[]> NotScalars { get; } = new()
    {
        { 2, new byte[32] },
        { 2, new byte[31] },
        { 2, new byte[33] },
        { 2, Convert.FromHexString("FFFFFFFF00000000FFFFFFFFFFFFFFFFBCE6FAADA7179E84F3B9CAC2FC632551") },
        { 2, Convert.FromHexString("FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF") },
        { 0, PushHarness.Scalar() },
        { 1, PushHarness.Scalar() },
    };

    [Fact]
    public async Task MSG_OPS_001_AC1_EveryChannelKeyAbsentHasItsDefault()
    {
        using IHost host = ChannelsHost.Build(ChannelsHost.Used());
        await host.StartAsync(TestContext.Current.CancellationToken);

        ChannelsOptions options = host.Services.GetRequiredService<IOptions<ChannelsOptions>>().Value;

        Assert.Equal(TimeSpan.FromSeconds(20), options.Dispatch.Timeout);
        Assert.Null(options.Email.From.DisplayName);
        Assert.Equal(10, options.Email.Rate);
        Assert.Equal(1024 * 1024, options.Email.MaxBytes);
        Assert.Equal(5, options.Sms.Rate);
        Assert.Null(options.Sms.ReportBaseAddress);
        Assert.Equal(60, options.Sms.WrongKeyRate);
        Assert.Equal(20, options.Sms.ReportRejectThreshold);
        Assert.Equal(TimeSpan.FromHours(1), options.Sms.ReportRejectWindow);
        Assert.Equal(TimeSpan.FromMinutes(10), options.Sms.ReportHold);
        Assert.Equal(1_000, options.Sms.ReportHoldMax);
        Assert.Equal(TimeSpan.FromMinutes(15), options.Sms.BalancePoll);
        Assert.Equal(0m, options.Sms.BalanceFloor);
        Assert.Equal(TimeSpan.FromDays(1), options.Sms.FloorWarning);
        Assert.Equal(TimeSpan.FromHours(1), options.Sms.DrainWindow);
        Assert.Equal(3d, options.Sms.DrainFactor);
        Assert.Equal(TimeSpan.FromDays(7), options.Sms.DrainBaseline);
        Assert.Equal(TimeSpan.FromDays(1), options.Sms.DrainMinHistory);
        Assert.Equal(10m, options.Sms.DrainMinSpend);
        Assert.Equal(TimeSpan.FromHours(1), options.Sms.BalanceUnreadableAfter);
        Assert.Equal(["fcm.googleapis.com", "*.push.services.mozilla.com", "*.push.apple.com", "*.notify.windows.com"], options.Push.Hosts);
        Assert.Equal(10, options.Push.MaxSubscriptions);
        Assert.Equal(50, options.Push.Rate);
        Assert.Equal(TimeSpan.FromDays(180), options.Push.IdleRetention);
        Assert.Equal(20, options.Push.AuthFailThreshold);
        Assert.Equal(TimeSpan.FromHours(1), options.Push.AuthFailWindow);
    }

    [Fact]
    public async Task MSG_OPS_001_AC1_ChannelKeyPresentIsBound()
    {
        Dictionary<string, string?> configuration = ChannelsHost.Used();
        configuration["Hermes:Dispatch:Timeout"] = "00:00:05";
        configuration["Hermes:Email:From:DisplayName"] = "Order notices";
        configuration["Hermes:Email:MaxBytes"] = "2097152";
        configuration["Hermes:Sms:AllowedPrefixes:1"] = "+966";
        configuration["Hermes:Sms:ReportBaseAddress"] = "https://messages.example.com/sms";
        configuration["Hermes:Sms:BalanceFloor"] = "12.5";
        configuration["Hermes:Sms:DrainFactor"] = "1.5";
        configuration["Hermes:Push:Rate"] = "100";
        using IHost host = ChannelsHost.Build(configuration);
        await host.StartAsync(TestContext.Current.CancellationToken);

        ChannelsOptions options = host.Services.GetRequiredService<IOptions<ChannelsOptions>>().Value;

        Assert.Equal(TimeSpan.FromSeconds(5), options.Dispatch.Timeout);
        Assert.Equal(TimeSpan.FromSeconds(5), host.Services.GetRequiredService<CallTimeout>().Current);
        Assert.Equal(ChannelsHost.FromAddress, options.Email.From.Address);
        Assert.Equal("Order notices", options.Email.From.DisplayName);
        Assert.Equal(2 * 1024 * 1024, options.Email.MaxBytes);
        Assert.Equal(ChannelsHost.Sender, options.Sms.Sender);
        Assert.Equal(["+20", "+966"], options.Sms.AllowedPrefixes);
        Assert.Equal(new Uri("https://messages.example.com/sms"), options.Sms.ReportBaseAddress);
        Assert.Equal(12.5m, options.Sms.BalanceFloor);
        Assert.Equal(1.5, options.Sms.DrainFactor);
        Assert.Equal(ChannelsHost.Subject, options.Push.Subject);
        Assert.Equal(100, options.Push.Rate);
    }

    [Theory]
    [InlineData("Dispatch:Timeout", "00:00:00.999")]
    [InlineData("Dispatch:Timeout", "00:02:01")]
    [InlineData("Email:Host", "smtp_example.com")]
    [InlineData("Email:Host", "192.0.2.1")]
    [InlineData("Email:Host", "")]
    [InlineData("Email:Port", "0")]
    [InlineData("Email:Port", "65536")]
    [InlineData("Email:Port", "submission")]
    [InlineData("Email:Security", "implicittls")]
    [InlineData("Email:Security", "Tls")]
    [InlineData("Email:TrustedAuthority", "authority")]
    [InlineData("Email:TrustedAuthorityRevocation", "None")]
    [InlineData("Email:TrustedAuthorityRevocation", "online")]
    [InlineData("Email:ShortLivedMaxValidity", "23:59:59")]
    [InlineData("Email:ShortLivedMaxValidity", "10.00:00:01")]
    [InlineData("Email:From:Address", "notices")]
    [InlineData("Email:From:Address", "notices@example.com>")]
    [InlineData("Email:From:Address", "")]
    [InlineData("Email:From:DisplayName", "")]
    [InlineData("Email:Rate", "0")]
    [InlineData("Email:Rate", "1001")]
    [InlineData("Email:MaxBytes", "10239")]
    [InlineData("Email:MaxBytes", "26214401")]
    [InlineData("Sms:Sender", "Hermes Notices")]
    [InlineData("Sms:Sender", "1234567890123456")]
    [InlineData("Sms:Sender", "")]
    [InlineData("Sms:Rate", "0")]
    [InlineData("Sms:Rate", "1001")]
    [InlineData("Sms:ReportBaseAddress", "ftp://messages.example.com/sms")]
    [InlineData("Sms:ReportBaseAddress", "/sms")]
    [InlineData("Sms:WrongKeyRate", "0")]
    [InlineData("Sms:WrongKeyRate", "10001")]
    [InlineData("Sms:ReportRejectThreshold", "0")]
    [InlineData("Sms:ReportRejectThreshold", "10001")]
    [InlineData("Sms:ReportRejectWindow", "00:00:59")]
    [InlineData("Sms:ReportRejectWindow", "1.00:00:01")]
    [InlineData("Sms:ReportHold", "00:00:59")]
    [InlineData("Sms:ReportHold", "1.00:00:01")]
    [InlineData("Sms:ReportHoldMax", "9")]
    [InlineData("Sms:ReportHoldMax", "1000001")]
    [InlineData("Sms:BalancePoll", "00:00:59")]
    [InlineData("Sms:BalancePoll", "1.00:00:01")]
    [InlineData("Sms:BalanceFloor", "-0.01")]
    [InlineData("Sms:BalanceFloor", "ten")]
    [InlineData("Sms:FloorWarning", "00:59:59")]
    [InlineData("Sms:FloorWarning", "30.00:00:01")]
    [InlineData("Sms:DrainWindow", "00:04:59")]
    [InlineData("Sms:DrainWindow", "1.00:00:01")]
    [InlineData("Sms:DrainFactor", "1.49")]
    [InlineData("Sms:DrainFactor", "100.01")]
    [InlineData("Sms:DrainBaseline", "90.00:00:01")]
    [InlineData("Sms:DrainMinHistory", "90.00:00:01")]
    [InlineData("Sms:DrainMinSpend", "-1")]
    [InlineData("Sms:BalanceUnreadableAfter", "00:04:59")]
    [InlineData("Sms:BalanceUnreadableAfter", "1.00:00:01")]
    [InlineData("Push:Subject", "operations@example.com")]
    [InlineData("Push:Subject", "http://example.com/operations")]
    [InlineData("Push:Subject", "")]
    [InlineData("Push:MaxSubscriptions", "0")]
    [InlineData("Push:MaxSubscriptions", "101")]
    [InlineData("Push:Rate", "0")]
    [InlineData("Push:Rate", "1001")]
    [InlineData("Push:IdleRetention", "6.23:59:59")]
    [InlineData("Push:IdleRetention", "1095.00:00:01")]
    [InlineData("Push:AuthFailThreshold", "0")]
    [InlineData("Push:AuthFailThreshold", "10001")]
    [InlineData("Push:AuthFailWindow", "00:00:59")]
    [InlineData("Push:AuthFailWindow", "1.00:00:01")]
    public async Task MSG_OPS_001_AC1_ChannelKeyOutsideItsRuleStopsTheStart(string key, string value)
    {
        Dictionary<string, string?> configuration = ChannelsHost.Used();
        configuration["Hermes:" + key] = value;
        using IHost host = ChannelsHost.Build(configuration);

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.configurationinvalid", failure.Code);
        Assert.Equal(key, failure.Details["key"]);
    }

    [Theory]
    [InlineData("Sms:AllowedPrefixes", "+966", "20")]
    [InlineData("Sms:AllowedPrefixes", "+966", "+1234567")]
    [InlineData("Push:AllowedHosts", "push.example.net", "push.example.net.")]
    [InlineData("Push:AllowedHosts", "push.example.net", "192.0.2.1")]
    public async Task MSG_OPS_001_AC1_ListItemOutsideItsRuleStopsTheStartNamingTheList(string key, string valid, string value)
    {
        Dictionary<string, string?> configuration = ChannelsHost.Used();
        configuration["Hermes:" + key + ":0"] = valid;
        configuration["Hermes:" + key + ":1"] = value;
        using IHost host = ChannelsHost.Build(configuration);

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.configurationinvalid", failure.Code);
        Assert.Equal(key, failure.Details["key"]);
    }

    [Theory]
    [InlineData("Dispatch:Timeout", "00:00:01")]
    [InlineData("Dispatch:Timeout", "00:02:00")]
    [InlineData("Email:Port", "1")]
    [InlineData("Email:Port", "65535")]
    [InlineData("Email:Security", "StartTls")]
    [InlineData("Email:TrustedAuthorityRevocation", "none")]
    [InlineData("Email:ShortLivedMaxValidity", "1.00:00:00")]
    [InlineData("Email:ShortLivedMaxValidity", "10.00:00:00")]
    [InlineData("Email:From:Address", "\"order notices\"@[192.0.2.1]")]
    [InlineData("Sms:Sender", "123456789012345")]
    [InlineData("Sms:Sender", "Hermes Note")]
    [InlineData("Sms:AllowedPrefixes:0", "+")]
    [InlineData("Sms:AllowedPrefixes:0", "+123456")]
    [InlineData("Sms:ReportBaseAddress", "http://messages.example.com/sms")]
    [InlineData("Sms:DrainFactor", "100")]
    [InlineData("Sms:DrainMinSpend", "0")]
    [InlineData("Push:Subject", "https://example.com/operations")]
    [InlineData("Push:AllowedHosts:0", "*.push.example.net")]
    [InlineData("Push:IdleRetention", "1095.00:00:00")]
    public async Task MSG_OPS_001_AC1_ChannelKeyAtTheEdgeOfItsRuleIsWithinIt(string key, string value)
    {
        Dictionary<string, string?> configuration = ChannelsHost.Used();
        configuration["Hermes:" + key] = value;
        using IHost host = ChannelsHost.Build(configuration);

        await host.StartAsync(TestContext.Current.CancellationToken);

        Assert.Empty(host.Services.GetRequiredService<ChannelsConfiguration>().InvalidKeys);
    }

    [Theory]
    [InlineData("Email", "Email:From:Address")]
    [InlineData("Email", "Email:Host")]
    [InlineData("Sms", "Sms:Sender")]
    [InlineData("Sms", "Sms:AllowedPrefixes")]
    [InlineData("Push", "Push:Subject")]
    public async Task MSG_OPS_001_AC1_RequiredChannelKeyAbsentStopsTheStart(string channel, string key)
    {
        Dictionary<string, string?> configuration = ChannelsHost.Used();
        configuration.Remove("Hermes:" + key);
        configuration.Remove("Hermes:" + key + ":0");
        configuration["Hermes:" + channel + ":Rate"] = "1";
        using IHost host = ChannelsHost.Build(configuration);

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.configurationinvalid", failure.Code);
        Assert.Equal(key, failure.Details["key"]);
    }

    [Theory]
    [InlineData("Sms:DrainBaseline", "01:59:59", "Sms:DrainBaseline")]
    [InlineData("Sms:DrainMinHistory", "00:59:59", "Sms:DrainMinHistory")]
    [InlineData("Sms:DrainMinHistory", "7.00:00:01", "Sms:DrainMinHistory")]
    [InlineData("Sms:DrainBaseline", "12:00:00", "Sms:DrainMinHistory")]
    public async Task MSG_OPS_001_AC1_ValueThatPutsAnotherChannelKeyOutsideItsRuleStopsTheStartNamingThatKey(string key, string value, string named)
    {
        Dictionary<string, string?> configuration = ChannelsHost.Used();
        configuration["Hermes:" + key] = value;
        using IHost host = ChannelsHost.Build(configuration);

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.configurationinvalid", failure.Code);
        Assert.Equal(named, failure.Details["key"]);
    }

    [Fact]
    public async Task MSG_OPS_001_AC1_KeyOfAPartNotUsedIsNeitherReadNorRequired()
    {
        Dictionary<string, string?> configuration = new(StringComparer.Ordinal)
        {
            ["Hermes:Email:From:Address"] = ChannelsHost.FromAddress,
            ["Hermes:Email:Host"] = ChannelsHost.SubmissionHost,
            ["Hermes:Retry:Factor"] = "many",
        };
        using IHost host = ChannelsHost.Build(configuration);

        await host.StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new ChannelUse(ProvidersAlone: true, Email: true, Sms: false, Push: false, ShippedEmail: true, ShippedPush: false), host.Services.GetRequiredService<ChannelsConfiguration>().Use);
    }

    [Fact]
    public async Task MSG_OPS_001_AC3_OneAllowedHostLeavesThatHostAloneAllowed()
    {
        Dictionary<string, string?> configuration = ChannelsHost.Used();
        configuration["Hermes:Push:AllowedHosts:0"] = "push.example.net";
        using IHost host = ChannelsHost.Build(configuration);
        await host.StartAsync(TestContext.Current.CancellationToken);

        IReadOnlyList<string> hosts = host.Services.GetRequiredService<IOptions<ChannelsOptions>>().Value.Push.Hosts;

        Assert.Equal(["push.example.net"], hosts);
        Assert.True(HostRule.Allows(hosts, "push.example.net"));
        Assert.False(HostRule.Allows(hosts, "fcm.googleapis.com"));
        Assert.False(HostRule.Allows(hosts, "updates.push.services.mozilla.com"));
    }

    [Theory]
    [InlineData("*.com")]
    [InlineData("*.co.uk")]
    public async Task MSG_PUSH_004_AC4_AllowedHostEntryCoveringAWholeDomainStopsTheStart(string entry)
    {
        Dictionary<string, string?> configuration = ChannelsHost.Used();
        configuration["Hermes:Push:AllowedHosts:0"] = "push.example.net";
        configuration["Hermes:Push:AllowedHosts:1"] = entry;
        using IHost host = ChannelsHost.Build(configuration);

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.configurationinvalid", failure.Code);
        Assert.Equal("Push:AllowedHosts", failure.Details["key"]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MSG_CHAN_001_AC1_HostPushProviderReplacesTheShippedOneWithNoOtherChange(bool registeredBefore)
    {
        PushProviderFake own = new();
        Dictionary<string, string?> configuration = new(StringComparer.Ordinal) { ["Hermes:Push:Rate"] = "20" };
        Action<IServiceCollection> register = services => services.AddSingleton<IPushProvider>(own);
        using IHost host = ChannelsHost.Build(ChannelsHost.Root(configuration), registeredBefore ? register : null, registeredBefore ? null : register);

        await host.StartAsync(TestContext.Current.CancellationToken);

        Assert.Same(own, host.Services.GetRequiredService<IPushProvider>());
        Assert.False(host.Services.GetRequiredService<ChannelsConfiguration>().Use!.ShippedPush);
    }

    [Fact]
    public async Task StartAsync_SenderTheSmsProviderRefuses_StopsTheStart()
    {
        using IHost host = ChannelsHost.Build(ChannelsHost.Used(), after: services => services.AddSingleton<ISmsProvider, SmsProviderFake>());

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.configurationinvalid", failure.Code);
        Assert.Equal("Sms:Sender", failure.Details["key"]);
    }

    [Fact]
    public async Task StartAsync_SenderTheSmsProviderAccepts_Starts()
    {
        Dictionary<string, string?> configuration = ChannelsHost.Used();
        configuration["Hermes:Sms:Sender"] = "20100";
        using IHost host = ChannelsHost.Build(configuration, after: services => services.AddSingleton<ISmsProvider, SmsProviderFake>());

        await host.StartAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(host.Services.GetRequiredService<ChannelsConfiguration>().Use);
    }

    [Fact]
    public async Task StartAsync_ShippedPushWithNoSecretSource_StopsTheStartAsADeclarationMissing()
    {
        using IHost host = ChannelsHost.Build(ChannelsHost.Root(ChannelsHost.Used()));

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.declarationmissing", failure.Code);
        Assert.Equal("secretSource", failure.Details["key"]);
    }

    [Fact]
    public async Task StartAsync_ShippedPushWithNoVapidKey_StopsTheStartAsASecretUnavailable()
    {
        using IHost host = ChannelsHost.Build(ChannelsHost.Used(), services => services.AddSingleton<ISecretSource>(new SecretSourceFake().WithVersions("emailSubmission", (1, ChannelsHost.Account(ChannelsHost.User, ChannelsHost.Password)))));

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.secretunavailable", failure.Code);
        Assert.Equal("pushVapidKeys", failure.Details["key"]);
    }

    [Theory]
    [MemberData(nameof(NotScalars))]
    public async Task MSG_PUSH_007_AC3_VersionThatIsNotAValidScalarStopsTheStart(int version, byte[] value)
    {
        SecretSourceFake secrets = ChannelsHost.Secrets().WithVersions("pushVapidKeys", (1, PushHarness.Scalar()), (version, value));
        using IHost host = ChannelsHost.Build(ChannelsHost.Used(), services => services.AddSingleton<ISecretSource>(secrets));

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.secretinvalid", failure.Code);
        Assert.Equal("pushVapidKeys", failure.Details["key"]);
    }

    [Fact]
    public async Task SendAsync_BeforeTheStart_Throws()
    {
        using IHost host = ChannelsHost.Build(ChannelsHost.Used());

        IPushProvider provider = host.Services.GetRequiredService<IPushProvider>();

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await provider.SendAsync(PushHarness.Request(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SendAsync_AfterTheStart_SignsWithTheHeldVersion()
    {
        await using PushServiceFake service = await PushServiceFake.StartAsync();
        using IHost host = ChannelsHost.Build(ChannelsHost.Used(), after: services => services.AddHttpClient(PushClient.Name).ConfigurePrimaryHttpMessageHandler(service.CreateHandler));
        await host.StartAsync(TestContext.Current.CancellationToken);

        Attempt attempt = await host.Services.GetRequiredService<IPushProvider>().SendAsync(PushHarness.Request(), TestContext.Current.CancellationToken);

        Assert.Equal(new Attempt.Accepted(null), attempt);
        Assert.StartsWith("vapid t=", Assert.Single(service.Requests).Headers["Authorization"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_VersionNotHeld_AnswersKeyUnavailable()
    {
        using IHost host = ChannelsHost.Build(ChannelsHost.Used());
        await host.StartAsync(TestContext.Current.CancellationToken);

        Attempt attempt = await host.Services.GetRequiredService<IPushProvider>().SendAsync(PushHarness.Request(keyVersion: 2), TestContext.Current.CancellationToken);

        Assert.Equal(new Attempt.Transient(AttemptReason.KeyUnavailable, null, null), attempt);
    }

    [Fact]
    public async Task StartingAsync_UnderTheEngine_LeavesTheChecksToIt()
    {
        Dictionary<string, string?> configuration = ChannelsHost.Used();
        configuration["Hermes:Push:Rate"] = "0";
        using IHost host = ChannelsHost.Build(configuration, after: services => services.AddSingleton<ChannelsUnderEngine>());

        await host.StartAsync(TestContext.Current.CancellationToken);

        Assert.Null(host.Services.GetRequiredService<ChannelsConfiguration>().Use);
    }

    [Fact]
    public async Task StartAsync_TrustedAuthorityHoldingOneCertificate_Starts()
    {
        using TestAuthority authority = TestAuthority.Create();
        Dictionary<string, string?> configuration = ChannelsHost.Used();
        configuration["Hermes:Email:TrustedAuthority"] = authority.Pem;
        using IHost host = ChannelsHost.Build(configuration);

        await host.StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal(authority.Pem, host.Services.GetRequiredService<IOptions<ChannelsOptions>>().Value.Email.TrustedAuthority);
    }

    [Fact]
    public async Task StartAsync_TrustedAuthorityHoldingOtherThanOneCertificate_StopsTheStart()
    {
        using TestAuthority authority = TestAuthority.Create();
        using TestAuthority other = TestAuthority.Create();
        using ECDsa key = authority.Certificate.GetECDsaPublicKey()!;
        string[] values = [authority.Pem + other.Pem, authority.Pem + key.ExportSubjectPublicKeyInfoPem(), key.ExportSubjectPublicKeyInfoPem()];
        foreach (string value in values)
        {
            Dictionary<string, string?> configuration = ChannelsHost.Used();
            configuration["Hermes:Email:TrustedAuthority"] = value;
            using IHost host = ChannelsHost.Build(configuration);

            MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

            Assert.Equal("messaging.startup.configurationinvalid", failure.Code);
            Assert.Equal("Email:TrustedAuthority", failure.Details["key"]);
        }
    }

    [Fact]
    public async Task MSG_LIB_005_AC3_TheProvidersAloneWithTheSmsSectionPresentAndNoSmsProviderDoNotStartNamingIt()
    {
        Dictionary<string, string?> configuration = new(StringComparer.Ordinal)
        {
            ["Hermes:Sms:Sender"] = ChannelsHost.Sender,
            ["Hermes:Sms:AllowedPrefixes:0"] = "+20",
        };
        using IHost host = ChannelsHost.Build(configuration, services => services.AddSingleton<ISecretSource>(new SecretSourceFake()), services => services.RemoveAll<ISmsProvider>());

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.declarationmissing", failure.Code);
        Assert.Equal("smsProvider", failure.Details["key"]);
    }

    [Fact]
    public async Task MSG_LIB_005_AC3_TheProvidersAloneWithNoSmsSectionStartWithNoSmsProvider()
    {
        Dictionary<string, string?> configuration = new(StringComparer.Ordinal)
        {
            ["Hermes:Push:Subject"] = ChannelsHost.Subject,
        };
        using IHost host = ChannelsHost.Build(configuration, after: services => services.RemoveAll<ISmsProvider>());

        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task MSG_LIB_003_AC4_TheProvidersAloneWithTheEmailSectionPresentDoNotStartWithoutEmailSubmission()
    {
        Dictionary<string, string?> configuration = new(StringComparer.Ordinal)
        {
            ["Hermes:Email:From:Address"] = ChannelsHost.FromAddress,
            ["Hermes:Email:Host"] = ChannelsHost.SubmissionHost,
        };
        using IHost host = ChannelsHost.Build(configuration, services => services.AddSingleton<ISecretSource>(new SecretSourceFake()));

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.secretunavailable", failure.Code);
        Assert.Equal("emailSubmission", failure.Details["key"]);
    }

    [Fact]
    public async Task MSG_OPS_008_AC1_EmailSubmissionMissingWhereEmailIsSentStopsTheStartNamingIt()
    {
        SecretSourceFake secrets = new SecretSourceFake().WithVersions("pushVapidKeys", (1, PushHarness.Scalar()));
        using IHost host = ChannelsHost.Build(ChannelsHost.Used(), services => services.AddSingleton<ISecretSource>(secrets));

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.secretunavailable", failure.Code);
        Assert.Equal("emailSubmission", failure.Details["key"]);
    }

    [Theory]
    [InlineData("notices")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"userName\":\"notices\"}")]
    [InlineData("{\"userName\":\"\",\"password\":\"p\"}")]
    [InlineData("{\"userName\":\"notices\",\"password\":\"\"}")]
    [InlineData("{\"userName\":7,\"password\":\"p\"}")]
    [InlineData("{\"userName\":\"notices\",\"password\":\"p\"} {}")]
    [InlineData("{\"userName\":\"notices\",\"password\":\"p\"")]
    public async Task StartAsync_EmailSubmissionThatDoesNotReadAsTheAccount_StopsTheStartAsASecretInvalid(string json)
    {
        SecretSourceFake secrets = ChannelsHost.Secrets().WithVersions("emailSubmission", (1, Encoding.UTF8.GetBytes(json)));
        using IHost host = ChannelsHost.Build(ChannelsHost.Used(), services => services.AddSingleton<ISecretSource>(secrets));

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.secretinvalid", failure.Code);
        Assert.Equal("emailSubmission", failure.Details["key"]);
    }

    [Fact]
    public async Task StartAsync_EmailSubmissionWithMembersBesideTheAccount_Starts()
    {
        byte[] account = Encoding.UTF8.GetBytes("{\"server\":{\"name\":[1,2]},\"userName\":\"" + ChannelsHost.User + "\",\"password\":\"" + ChannelsHost.Password + "\"}");
        SecretSourceFake secrets = ChannelsHost.Secrets().WithVersions("emailSubmission", (1, account));
        using IHost host = ChannelsHost.Build(ChannelsHost.Used(), services => services.AddSingleton<ISecretSource>(secrets));

        await host.StartAsync(TestContext.Current.CancellationToken);

        Assert.True(host.Services.GetRequiredService<ChannelsConfiguration>().Use!.ShippedEmail);
    }

    [Fact]
    public async Task StartAsync_ShippedEmailWithNoSecretSource_StopsTheStartAsADeclarationMissing()
    {
        Dictionary<string, string?> configuration = new(StringComparer.Ordinal)
        {
            ["Hermes:Email:From:Address"] = ChannelsHost.FromAddress,
            ["Hermes:Email:Host"] = ChannelsHost.SubmissionHost,
        };
        using IHost host = ChannelsHost.Build(ChannelsHost.Root(configuration));

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.declarationmissing", failure.Code);
        Assert.Equal("secretSource", failure.Details["key"]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MSG_CHAN_001_AC1_HostEmailProviderReplacesTheShippedOneWithNoOtherChange(bool registeredBefore)
    {
        EmailProviderFake own = new();
        Dictionary<string, string?> configuration = new(StringComparer.Ordinal)
        {
            ["Hermes:Email:From:Address"] = ChannelsHost.FromAddress,
            ["Hermes:Email:Port"] = "0",
        };
        Action<IServiceCollection> register = services => services.AddSingleton<IEmailProvider>(own);
        using IHost host = ChannelsHost.Build(ChannelsHost.Root(configuration), registeredBefore ? register : null, registeredBefore ? null : register);

        await host.StartAsync(TestContext.Current.CancellationToken);

        Assert.Same(own, host.Services.GetRequiredService<IEmailProvider>());
        Assert.False(host.Services.GetRequiredService<ChannelsConfiguration>().Use!.ShippedEmail);
    }
}