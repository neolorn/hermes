using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Sending;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hermes.Operations;

/// <summary>
/// Runs the engine's startup checks in <see cref="StartingAsync"/>, which the host calls on
/// every lifecycle service before any hosted service's <c>StartAsync</c>, so they complete
/// before a host's own hosted service starts or a request is served (<c>04</c> MSG-LIB-003):
/// the configuration, then the declarations.
/// </summary>
internal sealed class MessagingStartup(IConfiguration configuration, ConfigurationState state, IOptionsMonitor<MessagingOptions> options, Declarations declarations) : IHostedLifecycleService
{
    public Task StartingAsync(CancellationToken cancellationToken)
    {
        string? key = ConfigurationCheck.InvalidKey(configuration.GetSection("Hermes"), TimeZoneInfo.GetSystemTimeZones(), declarations.NeedsPublicBaseAddress);
        if (key is not null)
        {
            throw Failure(Codes.ConfigurationInvalid, ("key", key));
        }

        state.Start();
        MessagingOptions current = options.CurrentValue;

        if (DeclarationCheck.Invalid(declarations, current.Languages, current.Retention.Places) is var (declaration, field))
        {
            throw Failure(Codes.DeclarationInvalid, ("declaration", declaration), ("field", field));
        }

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

    private static MessagingStartupException Failure(string code, params (string Name, string Value)[] details)
    {
        Dictionary<string, string> named = new(StringComparer.Ordinal);
        foreach ((string name, string value) in details)
        {
            named[name] = value;
        }

        return new MessagingStartupException(code, named);
    }
}