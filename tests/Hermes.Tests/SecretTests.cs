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
        MessagingStartupException failure = await StartFailure(new SecretSourceFake());

        Assert.Equal("messaging.startup.secretunavailable", failure.Code);
        Assert.Equal("connection", failure.Details["key"]);
    }

    [Fact]
    public async Task Start_ConnectionWithNoVersion_StopsTheStartAsUnavailable()
    {
        MessagingStartupException failure = await StartFailure(new SecretSourceFake().With("connection"));

        Assert.Equal("messaging.startup.secretunavailable", failure.Code);
        Assert.Equal("connection", failure.Details["key"]);
    }

    [Theory]
    [InlineData(new byte[] { 0xC3, 0x28 })]
    [InlineData(new byte[] { (byte)'=', (byte)'x' })]
    [InlineData(new byte[] { (byte)'N', (byte)'o', (byte)'=', (byte)'1' })]
    public async Task Start_ConnectionThatDoesNotRead_StopsTheStartAsInvalid(byte[] value)
    {
        MessagingStartupException failure = await StartFailure(new SecretSourceFake().With("connection", value));

        Assert.Equal("messaging.startup.secretinvalid", failure.Code);
        Assert.Equal("connection", failure.Details["key"]);
    }

    [Fact]
    public async Task Start_ConnectionOfSeveralVersions_UsesTheHighest()
    {
        MessagingStartupException failure = await StartFailure(new SecretSourceFake().With("connection", Encoding.UTF8.GetBytes("Host=localhost;Port=1;Timeout=1"), [0xC3, 0x28]));

        Assert.Equal("connection", failure.Details["key"]);
    }

    private static async Task<MessagingStartupException> StartFailure(SecretSourceFake secrets)
    {
        using IHost host = TestHost.Build(
            TestHost.Languages(),
            declare =>
            {
                declare.Category("security").Essential();
                declare.Kind("sign.in", "security").Sms();
            },
            services => services.AddSingleton<ISecretSource>(secrets));
        return await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));
    }
}