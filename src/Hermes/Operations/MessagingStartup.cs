using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Hermes.Operations;

/// <summary>
/// Runs the engine's startup checks in <see cref="StartingAsync"/>, which the host calls on
/// every lifecycle service before any hosted service's <c>StartAsync</c>, so they complete
/// before a host's own hosted service starts or a request is served (<c>04</c> MSG-LIB-003).
/// </summary>
internal sealed class MessagingStartup(IConfiguration configuration, ConfigurationState state) : IHostedLifecycleService
{
    public Task StartingAsync(CancellationToken cancellationToken)
    {
        string? key = ConfigurationCheck.InvalidKey(configuration.GetSection("Hermes"), TimeZoneInfo.GetSystemTimeZones());
        if (key is not null)
        {
            throw new MessagingStartupException(Codes.ConfigurationInvalid, new Dictionary<string, string>(StringComparer.Ordinal) { ["key"] = key });
        }

        state.Start();
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StartedAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StoppingAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StoppedAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}