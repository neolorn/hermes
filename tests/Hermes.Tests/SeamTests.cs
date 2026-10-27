using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Operations;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "unit")]
public sealed class SeamTests
{
    [Fact]
    public async Task MSG_LIB_005_AC1_SecretSourceMissingStopsTheStartNamingIt()
    {
        using IHost host = TestHost.Build(TestHost.Languages(), Essential);

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.declarationmissing", failure.Code);
        Assert.Equal("secretSource", failure.Details["key"]);
    }

    [Fact]
    public async Task MSG_LIB_005_AC1_ConsentSourceMissingWhereACategoryRequiresConsentStopsTheStartNamingIt()
    {
        using IHost host = TestHost.Build(
            TestHost.Languages(),
            declare =>
            {
                declare.Category("offers").RequiresConsent("marketing");
                declare.Kind("offer.made", "offers").Sms();
            },
            services => services.AddSingleton<ISecretSource>(new SecretSourceFake()));

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.declarationmissing", failure.Code);
        Assert.Equal("consentSource", failure.Details["key"]);
    }

    [Fact]
    public async Task MSG_LIB_005_AC1_ConsentSourceIsNotRequiredWhereNoCategoryRequiresConsent()
    {
        using IHost host = TestHost.Build(TestHost.Languages(), Essential, services => services.AddSingleton<ISecretSource>(new SecretSourceFake()));

        await StartStage.AssertPastTheSeams(host);
    }

    [Fact]
    public async Task MSG_LIB_005_AC1_DeliveryObserverMissingWhereObserverAndDispatchAreOnStopsTheStartNamingIt()
    {
        Dictionary<string, string?> configuration = TestHost.Languages();
        configuration["Hermes:Observer:Enabled"] = "true";
        using IHost host = TestHost.Build(configuration, Essential, services => services.AddSingleton<ISecretSource>(new SecretSourceFake()));

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.declarationmissing", failure.Code);
        Assert.Equal("deliveryObserver", failure.Details["key"]);
    }

    [Fact]
    public async Task MSG_LIB_005_AC1_RecipientIdentityMissingWhereThePreferencesGroupIsMappedStopsTheStartNamingIt()
    {
        using IHost host = TestHost.Build(
            TestHost.Languages(),
            Essential,
            services => services.AddSingleton<ISecretSource>(new SecretSourceFake()),
            map: application => application.MapHermesPreferences(Endpoints.Prefix));

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.declarationmissing", failure.Code);
        Assert.Equal("recipientIdentity", failure.Details["key"]);
    }

    [Fact]
    public async Task MSG_LIB_005_AC1_RecipientIdentityMissingWhereThePushGroupIsMappedStopsTheStartNamingIt()
    {
        using IHost host = TestHost.Build(
            TestHost.Languages(),
            Essential,
            services => services.AddSingleton<ISecretSource>(new SecretSourceFake()),
            map: application => application.MapHermesPushSubscriptions(Endpoints.Prefix));

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.declarationmissing", failure.Code);
        Assert.Equal("recipientIdentity", failure.Details["key"]);
    }

    [Fact]
    public async Task MSG_LIB_005_AC1_RecipientIdentityRegisteredWhereThePreferencesGroupIsMappedIsFound()
    {
        using IHost host = TestHost.Build(
            TestHost.Languages(),
            Essential,
            services =>
            {
                services.AddSingleton<ISecretSource>(new SecretSourceFake());
                Endpoints.Identity(services);
            },
            map: application => application.MapHermesPreferences(Endpoints.Prefix));

        await StartStage.AssertPastTheSeams(host);
    }

    [Fact]
    public async Task MSG_LIB_005_AC1_RecipientIdentityIsNotRequiredWhereOnlyTheUnsubscribeGroupIsMapped()
    {
        using IHost host = TestHost.Build(
            TestHost.Languages(),
            Essential,
            services => services.AddSingleton<ISecretSource>(new SecretSourceFake()),
            map: application => application.MapHermesUnsubscribe(Endpoints.Prefix));

        await StartStage.AssertPastTheSeams(host);
    }

    [Fact]
    public void Map_AfterTheHostStarted_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => TestHost.Build(
            TestHost.Languages(),
            Essential,
            map: application =>
            {
                application.Services.GetRequiredService<MessagingReady>().Mark();
                application.MapHermesPreferences(Endpoints.Prefix);
            }));
    }

    [Fact]
    public async Task MSG_LIB_005_AC1_DeliveryObserverIsNotRequiredWhereDispatchIsOff()
    {
        Dictionary<string, string?> configuration = TestHost.Languages();
        configuration["Hermes:Observer:Enabled"] = "true";
        configuration["Hermes:Dispatch:Enabled"] = "false";
        using IHost host = TestHost.Build(configuration, Essential, services => services.AddSingleton<ISecretSource>(new SecretSourceFake()));

        await StartStage.AssertPastTheSeams(host);
    }

    [Fact]
    public async Task MSG_LIB_005_AC1_SeamRegisteredAfterTheCallIsFound()
    {
        Dictionary<string, string?> configuration = TestHost.Languages();
        configuration["Hermes:Observer:Enabled"] = "true";
        using IHost host = TestHost.Build(
            configuration,
            Essential,
            after: services => services.AddSingleton<ISecretSource>(new SecretSourceFake()).AddSingleton<IDeliveryObserver>(new DeliveryObserverFake()));

        await StartStage.AssertPastTheSeams(host);
    }

    [Fact]
    public async Task MSG_LIB_005_AC2_TwoHostProtectorsWithOneIdStopTheStart()
    {
        using IHost host = TestHost.Build(
            TestHost.Languages(),
            Essential,
            services => services
                .AddSingleton<ISecretSource>(new SecretSourceFake())
                .AddSingleton<IContentProtector>(new ContentProtectorFake("vault"))
                .AddSingleton<IContentProtector>(new ContentProtectorFake("vault")));

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.declarationinvalid", failure.Code);
        Assert.Equal("vault", failure.Details["declaration"]);
        Assert.Equal("Id", failure.Details["field"]);
    }

    [Theory]
    [InlineData("messaging")]
    [InlineData("")]
    [InlineData("Vault")]
    [InlineData("my-vault")]
    [InlineData("vaultvaultvaultvaultvaultvaultvau")]
    public async Task Start_HostProtectorIdOutsideTheRule_StopsTheStart(string id)
    {
        using IHost host = TestHost.Build(
            TestHost.Languages(),
            Essential,
            services => services.AddSingleton<ISecretSource>(new SecretSourceFake()).AddSingleton<IContentProtector>(new ContentProtectorFake(id)));

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.declarationinvalid", failure.Code);
        Assert.Equal(id, failure.Details["declaration"]);
        Assert.Equal("Id", failure.Details["field"]);
    }

    [Fact]
    public async Task Start_HostProtectorIdOfTheRule_IsAccepted()
    {
        using IHost host = TestHost.Build(
            TestHost.Languages(),
            Essential,
            services => services.AddSingleton<ISecretSource>(new SecretSourceFake()).AddSingleton<IContentProtector>(new ContentProtectorFake("vault2" + new string('v', 26))));

        await StartStage.AssertPastTheSeams(host);
    }

    [Fact]
    public async Task MSG_LIB_005_LibrarysOwnAlertSinkLogsEachConditionAtTheErrorLevel()
    {
        using LogCapture logs = new();
        using IHost host = TestHost.Build(TestHost.Languages(), Essential, logs: logs);
        IAlertSink sink = host.Services.GetRequiredService<IAlertSink>();

        await sink.RaiseAsync("configuration-invalid", "Dispatch:Interval", new Dictionary<string, string>(StringComparer.Ordinal) { ["key"] = "Dispatch:Interval" }, TestContext.Current.CancellationToken);
        await sink.ClearAsync("configuration-invalid", "Dispatch:Interval", TestContext.Current.CancellationToken);

        Assert.Collection(
            logs.Entries,
            raised =>
            {
                Assert.Equal(LogLevel.Error, raised.Level);
                Assert.Contains("configuration-invalid", raised.Message, StringComparison.Ordinal);
                Assert.Contains("key Dispatch:Interval", raised.Message, StringComparison.Ordinal);
            },
            cleared =>
            {
                Assert.Equal(LogLevel.Error, cleared.Level);
                Assert.Contains("configuration-invalid", cleared.Message, StringComparison.Ordinal);
            });
    }

    [Fact]
    public void AddHermes_HostSinkRegisteredBefore_IsKept()
    {
        AlertSinkFake sink = new();
        using IHost host = TestHost.Build(TestHost.Languages(), Essential, services => services.AddSingleton<IAlertSink>(sink));

        Assert.Same(sink, host.Services.GetRequiredService<IAlertSink>());
    }

    private static void Essential(DeclarationBuilder declare)
    {
        declare.Category("security").Essential();
        declare.Kind("sign.in", "security").Sms();
    }
}