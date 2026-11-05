using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Xunit;

namespace Hermes.Sms.Cequens.Tests;

[Trait("Category", "unit")]
public sealed class CequensStartupTests
{
    /// <summary>Highest versions outside the token's form: empty, over 8,192 bytes, a space, a control character, a byte outside ASCII.</summary>
    public static TheoryData<byte[]> OutsideTheForm { get; } = new()
    {
        Array.Empty<byte>(),
        Enumerable.Repeat((byte)'a', 8193).ToArray(),
        Encoding.ASCII.GetBytes("header payload.signature"),
        Encoding.ASCII.GetBytes("header.payload\n"),
        new byte[] { (byte)'a', 0xC3, 0xA9 },
    };

    [Fact]
    public async Task MSG_SMS_007_AC6_StartWithSmsCequensUnansweredFailsWithSecretUnavailable()
    {
        using IHost host = Build(services => services.AddSingleton<ISecretSource>(new SecretSourceFake().WithVersions("emailSubmission", (1, CequensHarness.Token()))));

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.secretunavailable", failure.Code);
        Assert.Equal(new Dictionary<string, string> { ["key"] = "smsCequens" }, failure.Details);
    }

    [Fact]
    public async Task MSG_SMS_007_AC6_NewHighestVersionIsUsedByTheNextSendWithNoRestart()
    {
        await using CequensHarness harness = await CequensHarness.StartAsync();
        byte[] first = CequensHarness.Token();
        byte[] second = CequensHarness.Token();
        harness.Secrets.WithVersions(CequensToken.Name, (1, first));
        await harness.Provider.SendAsync(CequensHarness.Message(), TestContext.Current.CancellationToken);

        harness.Secrets.WithVersions(CequensToken.Name, (1, first), (2, second));
        await harness.Provider.SendAsync(CequensHarness.Message(), TestContext.Current.CancellationToken);

        Assert.Equal(
            ["Bearer " + Encoding.ASCII.GetString(first), "Bearer " + Encoding.ASCII.GetString(second)],
            harness.Gateway.Requests.Select(request => request.Headers["Authorization"]));
    }

    [Fact]
    public async Task StartingAsync_NoSecretSourceRegistered_FailsWithSecretUnavailable()
    {
        using IHost host = Build(_ => { });

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.secretunavailable", failure.Code);
        Assert.Equal("smsCequens", failure.Details["key"]);
    }

    [Fact]
    public async Task StartingAsync_SecretAnsweredWithNoVersion_FailsWithSecretUnavailable()
    {
        using IHost host = Build(services => services.AddSingleton<ISecretSource>(new SecretSourceFake().WithVersions(CequensToken.Name)));

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.secretunavailable", failure.Code);
    }

    [Theory]
    [MemberData(nameof(OutsideTheForm))]
    public async Task StartingAsync_HighestVersionOutsideTheForm_FailsWithSecretInvalid(byte[] value)
    {
        using IHost host = Build(services => services.AddSingleton<ISecretSource>(new SecretSourceFake().WithVersions(CequensToken.Name, (1, CequensHarness.Token()), (2, value))));

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.secretinvalid", failure.Code);
        Assert.Equal(new Dictionary<string, string> { ["key"] = "smsCequens" }, failure.Details);
    }

    [Fact]
    public async Task StartingAsync_HighestVersionWithinTheForm_Starts()
    {
        byte[] longest = Enumerable.Repeat((byte)'~', 8192).ToArray();
        using IHost host = Build(services => services.AddSingleton<ISecretSource>(new SecretSourceFake().WithVersions(CequensToken.Name, (1, [0x00]), (2, longest))));

        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    private static IHost Build(Action<IServiceCollection> secrets)
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        secrets(builder.Services);
        builder.Services.AddHermesCequensSms();
        return builder.Build();
    }
}