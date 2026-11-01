using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "unit")]
public sealed class ChannelCheckTests
{
    [Theory]
    [InlineData("orders")]
    [InlineData("orders@")]
    [InlineData("orders@example.com>")]
    [InlineData("Orders <orders@example.com>")]
    [InlineData("")]
    public async Task MSG_EMAIL_002_AC2_AKindWhoseFromIsNotAnAddressStopsTheStart(string from)
    {
        using IHost host = Build(declare => Newsletter(declare).Email(from: new EmailAddress { Address = from }));

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.declarationinvalid", failure.Code);
        Assert.Equal("news.issued", failure.Details["declaration"]);
        Assert.Equal("from", failure.Details["field"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("A display name longer than one hundred and twenty-eight characters, which is more than the rule for the deployment's own sender allows")]
    public async Task MSG_EMAIL_002_AC2_AKindWhoseFromHasADisplayNameOutsideItsRuleStopsTheStart(string name)
    {
        using IHost host = Build(declare => Newsletter(declare).Email(from: new EmailAddress { Address = "news@example.com", DisplayName = name }));

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.declarationinvalid", failure.Code);
        Assert.Equal("from", failure.Details["field"]);
    }

    [Fact]
    public async Task MSG_EMAIL_002_AC2_AKindWhoseReplyToIsNotAnAddressStopsTheStart()
    {
        using IHost host = Build(declare => Newsletter(declare).Email(from: new EmailAddress { Address = "news@example.com", DisplayName = "News" }, replyTo: new EmailAddress { Address = "replies" }));

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.declarationinvalid", failure.Code);
        Assert.Equal("replyTo", failure.Details["field"]);
    }

    [Fact]
    public async Task MSG_EMAIL_002_AC2_AKindWhoseFromAndReplyToAreAddressesStarts()
    {
        using IHost host = Build(declare => Newsletter(declare).Email(from: new EmailAddress { Address = "news@example.com", DisplayName = "News" }, replyTo: new EmailAddress { Address = "\"news desk\"@[192.0.2.1]" }));

        await StartStage.AssertPastTheSeams(host);
    }

    [Theory]
    [InlineData("Order Notices")]
    [InlineData("1234567890123456")]
    [InlineData("Orders[1]")]
    [InlineData("")]
    public async Task StartAsync_KindSenderOutsideTheBaseRule_StopsTheStart(string sender)
    {
        using IHost host = Build(declare => SignIn(declare).Sms(sender: sender));

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.declarationinvalid", failure.Code);
        Assert.Equal("sign.in", failure.Details["declaration"]);
        Assert.Equal("sender", failure.Details["field"]);
    }

    [Fact]
    public async Task StartAsync_KindSenderTheSmsProviderRefuses_StopsTheStart()
    {
        Dictionary<string, string?> configuration = TestHost.Languages();
        configuration["Hermes:Sms:Sender"] = "20100";
        using IHost host = Build(declare => SignIn(declare).Sms(sender: "Orders"), configuration, after: services => services.AddSingleton<ISmsProvider, SmsProviderFake>());

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.declarationinvalid", failure.Code);
        Assert.Equal("sender", failure.Details["field"]);
    }

    [Fact]
    public async Task StartAsync_DefaultSenderTheSmsProviderRefuses_StopsTheStartNamingTheKey()
    {
        using IHost host = Build(declare => SignIn(declare).Sms(), after: services => services.AddSingleton<ISmsProvider, SmsProviderFake>());

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.configurationinvalid", failure.Code);
        Assert.Equal("Sms:Sender", failure.Details["key"]);
    }

    [Fact]
    public async Task StartAsync_SendersTheSmsProviderAccepts_Starts()
    {
        Dictionary<string, string?> configuration = TestHost.Languages();
        configuration["Hermes:Sms:Sender"] = "20100";
        using IHost host = Build(declare => SignIn(declare).Sms(sender: "20200"), configuration, after: services => services.AddSingleton<ISmsProvider, SmsProviderFake>());

        await StartStage.AssertPastTheSeams(host);
    }

    [Theory]
    [InlineData("Email:Rate", "0", "Email:Rate")]
    [InlineData("Email:From:Address", "notices", "Email:From:Address")]
    [InlineData("Sms:AllowedPrefixes:0", "20", "Sms:AllowedPrefixes")]
    [InlineData("Push:Rate", "1001", "Push:Rate")]
    [InlineData("Push:AllowedHosts:0", "*.com", "Push:AllowedHosts")]
    public async Task MSG_LIB_003_AC2_AChannelValueOutsideItsRuleStopsTheStartNamingTheKey(string key, string value, string named)
    {
        Dictionary<string, string?> configuration = TestHost.Languages();
        configuration["Hermes:" + key] = value;
        using IHost host = Build(Every, configuration);

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.configurationinvalid", failure.Code);
        Assert.Equal(named, failure.Details["key"]);
    }

    [Theory]
    [InlineData("Email:From:Address")]
    [InlineData("Sms:Sender")]
    [InlineData("Push:Subject")]
    public async Task MSG_LIB_003_AC2_ARequiredChannelKeyAbsentWhereAKindUsesTheChannelStopsTheStart(string key)
    {
        Dictionary<string, string?> configuration = TestHost.Languages();
        configuration.Remove("Hermes:" + key);
        using IHost host = Build(Every, configuration);

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.configurationinvalid", failure.Code);
        Assert.Equal(key, failure.Details["key"]);
    }

    [Fact]
    public async Task StartAsync_ChannelKeysOfAChannelNoKindUses_AreNeitherReadNorRequired()
    {
        Dictionary<string, string?> configuration = TestHost.Languages();
        configuration.Remove("Hermes:Sms:Sender");
        configuration.Remove("Hermes:Sms:AllowedPrefixes:0");
        configuration.Remove("Hermes:Push:Subject");
        configuration["Hermes:Push:Rate"] = "0";
        configuration["Hermes:Sms:Rate"] = "0";
        using IHost host = Build(declare => Newsletter(declare).Email(), configuration);

        await StartStage.AssertPastTheSeams(host);
    }

    [Fact]
    public async Task StartAsync_ApplicationThatDoesNotDispatch_StartsWithNoPushSubject()
    {
        Dictionary<string, string?> configuration = TestHost.Languages();
        configuration.Remove("Hermes:Push:Subject");
        configuration["Hermes:Dispatch:Enabled"] = "false";
        using IHost host = Build(declare => SignIn(declare).Push(), configuration);

        await StartStage.AssertPastTheSeams(host);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MSG_CHAN_001_AC1_AHostPushProviderReplacesTheShippedOneUnderTheEngine(bool registeredBefore)
    {
        PushProviderFake own = new();
        Dictionary<string, string?> configuration = TestHost.Languages();
        configuration.Remove("Hermes:Push:Subject");
        Action<IServiceCollection> register = services => services.AddSingleton<IPushProvider>(own);
        using IHost host = TestHost.Build(
            configuration,
            declare => SignIn(declare).Push(),
            services =>
            {
                services.AddSingleton<ISecretSource>(Secrets.Held());
                if (registeredBefore)
                {
                    register(services);
                }
            },
            registeredBefore ? null : register);

        await StartStage.AssertPastTheSeams(host);

        Assert.Same(own, host.Services.GetRequiredService<IPushProvider>());
    }

    private static void Every(DeclarationBuilder declare)
    {
        Newsletter(declare).Email();
        SignIn(declare).Sms().Push();
    }

    private static KindBuilder Newsletter(DeclarationBuilder declare)
    {
        declare.Category("news").Label("en", "News");
        return declare.Kind("news.issued", "news");
    }

    private static KindBuilder SignIn(DeclarationBuilder declare)
    {
        declare.Category("security").Essential();
        return declare.Kind("sign.in", "security");
    }

    private static IHost Build(Action<DeclarationBuilder> declare, Dictionary<string, string?>? configuration = null, Action<IServiceCollection>? after = null)
    {
        Dictionary<string, string?> values = configuration ?? TestHost.Languages();
        values.TryAdd("Hermes:PublicBaseAddress", "https://example.test/");
        return TestHost.Build(values, declare, services => services.AddSingleton<ISecretSource>(Secrets.Held()), after);
    }
}