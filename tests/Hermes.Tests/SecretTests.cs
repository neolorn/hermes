using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "unit")]
public sealed class SecretTests
{
    [Fact]
    public async Task MSG_OPS_008_AC1_ConnectionMissingStopsTheStartNamingIt()
    {
        MessagingStartupException failure = await StartFailure(Secrets.Held().Without("connection"));

        AssertFailure(failure, "messaging.startup.secretunavailable", "connection");
    }

    [Fact]
    public async Task Start_ConnectionWithNoVersion_StopsTheStartAsUnavailable()
    {
        MessagingStartupException failure = await StartFailure(Secrets.Held().With("connection"));

        AssertFailure(failure, "messaging.startup.secretunavailable", "connection");
    }

    [Theory]
    [InlineData(new byte[] { 0xC3, 0x28 })]
    [InlineData(new byte[] { (byte)'=', (byte)'x' })]
    [InlineData(new byte[] { (byte)'N', (byte)'o', (byte)'=', (byte)'1' })]
    public async Task Start_ConnectionThatDoesNotRead_StopsTheStartAsInvalid(byte[] value)
    {
        MessagingStartupException failure = await StartFailure(Secrets.Held().With("connection", value));

        AssertFailure(failure, "messaging.startup.secretinvalid", "connection");
    }

    [Fact]
    public async Task Start_ConnectionOfSeveralVersions_UsesTheHighest()
    {
        MessagingStartupException failure = await StartFailure(Secrets.Held().With("connection", Encoding.UTF8.GetBytes(Secrets.Unreachable), [0xC3, 0x28]));

        AssertFailure(failure, "messaging.startup.secretinvalid", "connection");
    }

    [Fact]
    public async Task MSG_OPS_008_AC1_HashKeysMissingStopsTheStartNamingIt()
    {
        MessagingStartupException failure = await StartFailure(Secrets.Held().Without("hashKeys"));

        AssertFailure(failure, "messaging.startup.secretunavailable", "hashKeys");
    }

    [Fact]
    public async Task MSG_OPS_008_AC1_ContentKeysMissingWhereSendsAreRecordedStopsTheStartNamingIt()
    {
        MessagingStartupException failure = await StartFailure(Secrets.Held().Without("contentKeys"), Configuration(("Dispatch:Enabled", "false"), ("Jobs:Enabled", "false")));

        AssertFailure(failure, "messaging.startup.secretunavailable", "contentKeys");
    }

    [Fact]
    public async Task MSG_OPS_008_AC1_ContentKeysMissingWhereTheApplicationDispatchesStopsTheStartNamingIt()
    {
        MessagingStartupException failure = await StartFailure(Secrets.Held().Without("contentKeys"), Configuration(("Messages:Enabled", "false"), ("Jobs:Enabled", "false")));

        AssertFailure(failure, "messaging.startup.secretunavailable", "contentKeys");
    }

    [Fact]
    public async Task MSG_OPS_008_AC1_ContentKeysMissingWhereJobsRunStopsTheStartNamingIt()
    {
        MessagingStartupException failure = await StartFailure(Secrets.Held().Without("contentKeys"), Configuration(("Messages:Enabled", "false"), ("Dispatch:Enabled", "false")));

        AssertFailure(failure, "messaging.startup.secretunavailable", "contentKeys");
    }

    [Fact]
    public async Task Start_ContentKeysWhereNoPartNeedsThem_IsNotRead()
    {
        SecretSourceFake secrets = Secrets.Held().Without("contentKeys");

        await AssertPastTheSecrets(secrets, Configuration(("Messages:Enabled", "false"), ("Dispatch:Enabled", "false"), ("Jobs:Enabled", "false")));

        Assert.DoesNotContain("contentKeys", secrets.Read);
    }

    [Fact]
    public async Task MSG_OPS_008_AC1_TokenKeysMissingWhereAnOptionalCategoryHasAnEmailKindStopsTheStartNamingIt()
    {
        MessagingStartupException failure = await StartFailure(Secrets.Held().Without("tokenKeys"), Addressed(TestHost.Languages()), Newsletter);

        AssertFailure(failure, "messaging.startup.secretunavailable", "tokenKeys");
    }

    [Fact]
    public async Task Start_TokenKeysWhereNoSendsAreRecorded_IsNotRead()
    {
        SecretSourceFake secrets = Secrets.Held().Without("tokenKeys");
        Dictionary<string, string?> configuration = Addressed(TestHost.Languages());
        configuration["Hermes:Messages:Enabled"] = "false";

        await AssertPastTheSecrets(secrets, configuration, Newsletter);

        Assert.DoesNotContain("tokenKeys", secrets.Read);
    }

    [Fact]
    public async Task Start_TokenKeysWhereEveryEmailKindIsEssential_IsNotRead()
    {
        SecretSourceFake secrets = Secrets.Held().Without("tokenKeys");

        await AssertPastTheSecrets(secrets, TestHost.Languages(), declare =>
        {
            declare.Category("security").Essential();
            declare.Kind("sign.in", "security").Email();
        });

        Assert.DoesNotContain("tokenKeys", secrets.Read);
    }

    [Fact]
    public async Task MSG_OPS_008_AC11_PushUsedWithTheHostedDispatcherOffDoesNotStartWithoutPushVapidKeys()
    {
        MessagingStartupException failure = await StartFailure(Secrets.Held().Without("pushVapidKeys"), Configuration(("Dispatch:Hosted", "false")), Pushed);

        AssertFailure(failure, "messaging.startup.secretunavailable", "pushVapidKeys");
    }

    [Fact]
    public async Task Start_PushVapidKeysWherePushIsNotDispatched_IsNotRead()
    {
        SecretSourceFake secrets = Secrets.Held().Without("pushVapidKeys");

        await AssertPastTheSecrets(secrets, Configuration(("Dispatch:Enabled", "false")), Pushed);

        Assert.DoesNotContain("pushVapidKeys", secrets.Read);
    }

    [Fact]
    public async Task MSG_OPS_008_AC1_EmailSubmissionMissingWhereEmailIsDispatchedStopsTheStartNamingIt()
    {
        MessagingStartupException failure = await StartFailure(Secrets.Held().Without("emailSubmission"), Addressed(TestHost.Languages()), Newsletter);

        AssertFailure(failure, "messaging.startup.secretunavailable", "emailSubmission");
    }

    [Fact]
    public async Task Start_EmailSubmissionThatDoesNotReadAsTheAccount_StopsTheStartAsInvalid()
    {
        MessagingStartupException failure = await StartFailure(Secrets.Held().With("emailSubmission", Encoding.UTF8.GetBytes("{\"userName\":\"notices\"}")), Addressed(TestHost.Languages()), Newsletter);

        AssertFailure(failure, "messaging.startup.secretinvalid", "emailSubmission");
    }

    [Fact]
    public async Task Start_EmailWhereTheApplicationDoesNotDispatch_ReadsNeitherEmailHostNorEmailSubmission()
    {
        SecretSourceFake secrets = Secrets.Held().Without("emailSubmission");
        Dictionary<string, string?> configuration = Addressed(Configuration(("Dispatch:Enabled", "false")));
        configuration.Remove("Hermes:Email:Host");

        await AssertPastTheSecrets(secrets, configuration, Newsletter);

        Assert.DoesNotContain("emailSubmission", secrets.Read);
    }

    [Fact]
    public async Task Start_HostEmailProvider_ReadsNeitherEmailHostNorEmailSubmission()
    {
        SecretSourceFake secrets = Secrets.Held().Without("emailSubmission");
        Dictionary<string, string?> configuration = Addressed(TestHost.Languages());
        configuration["Hermes:Email:Host"] = "smtp_example";
        using IHost host = TestHost.Build(configuration, Newsletter, services => services.AddSingleton<ISecretSource>(secrets).AddSingleton<IEmailProvider, EmailProviderFake>());
        Exception? failure = await Record.ExceptionAsync(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.False(failure is MessagingStartupException { Code: "messaging.startup.configurationinvalid" or "messaging.startup.secretunavailable" }, $"The start stopped at the email provider's keys: {failure?.Message}");
        Assert.DoesNotContain("emailSubmission", secrets.Read);
    }

    [Theory]
    [InlineData("contentKeys", 31)]
    [InlineData("contentKeys", 33)]
    [InlineData("hashKeys", 31)]
    [InlineData("tokenKeys", 31)]
    [InlineData("pushVapidKeys", 31)]
    [InlineData("pushVapidKeys", 33)]
    public async Task MSG_OPS_008_AC1_KeyVersionOutsideItsFormStopsTheStartNamingIt(string name, int length)
    {
        MessagingStartupException failure = await StartFailure(Secrets.Held().With(name, Secrets.Bytes(length, 7)), Addressed(TestHost.Languages()), Everything);

        AssertFailure(failure, "messaging.startup.secretinvalid", name);
    }

    [Theory]
    [InlineData("0000000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("FFFFFFFF00000000FFFFFFFFFFFFFFFFBCE6FAADA7179E84F3B9CAC2FC632551")]
    [InlineData("FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF")]
    public async Task Start_PushVapidKeyThatIsNoScalarOfP256_StopsTheStartAsInvalid(string scalar)
    {
        MessagingStartupException failure = await StartFailure(Secrets.Held().With("pushVapidKeys", Convert.FromHexString(scalar)), Configuration(), Pushed);

        AssertFailure(failure, "messaging.startup.secretinvalid", "pushVapidKeys");
    }

    [Fact]
    public async Task Start_KeyVersionsOfTheirForms_PassTheSecrets()
    {
        await AssertPastTheSecrets(Secrets.Held(), Addressed(TestHost.Languages()), Everything);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Start_KeyVersionNumberedBelowOne_StopsTheStartAsInvalid(int version)
    {
        SecretSourceFake secrets = Secrets.Held().WithVersions("hashKeys", (version, Secrets.Bytes(32, 1)));

        MessagingStartupException failure = await StartFailure(secrets);

        AssertFailure(failure, "messaging.startup.secretinvalid", "hashKeys");
    }

    [Fact]
    public async Task Start_KeyVersionNumberedTwice_StopsTheStartAsInvalid()
    {
        SecretSourceFake secrets = Secrets.Held().WithVersions("hashKeys", (1, Secrets.Bytes(32, 1)), (1, Secrets.Bytes(32, 2)));

        MessagingStartupException failure = await StartFailure(secrets);

        AssertFailure(failure, "messaging.startup.secretinvalid", "hashKeys");
    }

    [Fact]
    public async Task MSG_OPS_008_AC4_TwoContentKeyVersionsWithNoWriteVersionStopTheStart()
    {
        SecretSourceFake secrets = Secrets.Held().With("contentKeys", Secrets.Bytes(32, 1), Secrets.Bytes(32, 2));

        MessagingStartupException failure = await StartFailure(secrets);

        AssertFailure(failure, "messaging.startup.configurationinvalid", "Secrets:ContentKeys:WriteVersion");
    }

    [Theory]
    [InlineData("3")]
    [InlineData("0")]
    [InlineData("the second")]
    public async Task Start_WriteVersionNotAVersionHeld_StopsTheStartNamingIt(string writeVersion)
    {
        SecretSourceFake secrets = Secrets.Held().With("hashKeys", Secrets.Bytes(32, 1), Secrets.Bytes(32, 2));

        MessagingStartupException failure = await StartFailure(secrets, Configuration(("Secrets:HashKeys:WriteVersion", writeVersion)));

        AssertFailure(failure, "messaging.startup.configurationinvalid", "Secrets:HashKeys:WriteVersion");
    }

    [Fact]
    public async Task Start_WriteVersionOfAVersionHeld_PassesTheSecrets()
    {
        SecretSourceFake secrets = Secrets.Held().With("hashKeys", Secrets.Bytes(32, 1), Secrets.Bytes(32, 2));

        await AssertPastTheSecrets(secrets, Configuration(("Secrets:HashKeys:WriteVersion", "2")));
    }

    [Fact]
    public async Task Start_WriteVersionOfAKeyNoPartNeeds_IsNotChecked()
    {
        await AssertPastTheSecrets(Secrets.Held(), Configuration(("Secrets:TokenKeys:WriteVersion", "the second"), ("Secrets:PushVapidKeys:WriteVersion", "9")));
    }

    private static Dictionary<string, string?> Configuration(params (string Key, string Value)[] values)
    {
        Dictionary<string, string?> configuration = TestHost.Languages();
        foreach ((string key, string value) in values)
        {
            configuration["Hermes:" + key] = value;
        }

        return configuration;
    }

    private static Dictionary<string, string?> Addressed(Dictionary<string, string?> configuration)
    {
        configuration["Hermes:PublicBaseAddress"] = "https://example.test/";
        return configuration;
    }

    private static void Essential(DeclarationBuilder declare)
    {
        declare.Category("security").Essential();
        declare.Kind("sign.in", "security").Sms();
    }

    private static void Newsletter(DeclarationBuilder declare)
    {
        declare.Category("news").Label("en", "News");
        declare.Kind("news.issued", "news").Email();
    }

    private static void Pushed(DeclarationBuilder declare)
    {
        declare.Category("security").Essential();
        declare.Kind("sign.in", "security").Push();
    }

    private static void Everything(DeclarationBuilder declare)
    {
        Newsletter(declare);
        Pushed(declare);
    }

    private static async Task<MessagingStartupException> StartFailure(SecretSourceFake secrets, Dictionary<string, string?>? configuration = null, Action<DeclarationBuilder>? declare = null)
    {
        using IHost host = TestHost.Build(configuration ?? TestHost.Languages(), declare ?? Essential, services => services.AddSingleton<ISecretSource>(secrets));
        return await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));
    }

    private static async Task AssertPastTheSecrets(SecretSourceFake secrets, Dictionary<string, string?> configuration, Action<DeclarationBuilder>? declare = null)
    {
        using IHost host = TestHost.Build(configuration, declare ?? Essential, services => services.AddSingleton<ISecretSource>(secrets));
        Exception? failure = await Record.ExceptionAsync(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.False(
            failure is MessagingStartupException { Code: "messaging.startup.configurationinvalid" or "messaging.startup.declarationinvalid" or "messaging.startup.declarationmissing" or "messaging.startup.secretunavailable" or "messaging.startup.secretinvalid" },
            $"The start stopped at the secrets: {failure?.Message}");
    }

    private static void AssertFailure(MessagingStartupException failure, string code, string key)
    {
        Assert.Equal(code, failure.Code);
        Assert.Equal(key, failure.Details["key"]);
        Assert.Equal(["key"], failure.Details.Keys.ToArray());
    }
}