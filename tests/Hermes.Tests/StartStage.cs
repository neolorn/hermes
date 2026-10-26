using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.Hosting;

using Xunit;

namespace Hermes.Tests;

/// <summary>Asserts how far a host's start got.</summary>
internal static class StartStage
{
    /// <summary>Starts <paramref name="host"/> and asserts no check up to the seams stopped it.</summary>
    internal static async Task AssertPastTheSeams(IHost host)
    {
        MessagingStartupException? failure = await Record.ExceptionAsync(() => host.StartAsync(TestContext.Current.CancellationToken)) as MessagingStartupException;

        Assert.True(
            failure is null || failure.Code is not ("messaging.startup.configurationinvalid" or "messaging.startup.declarationinvalid" or "messaging.startup.declarationmissing"),
            $"The start stopped at the seams: {failure?.Message}");
        if (failure is null)
        {
            await host.StopAsync(TestContext.Current.CancellationToken);
        }
    }
}