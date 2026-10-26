using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Sending;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hermes.Operations;

/// <summary>
/// Runs the engine's startup checks in <see cref="StartingAsync"/>, which the host calls on
/// every lifecycle service before any hosted service's <c>StartAsync</c>, so they complete
/// before a host's own hosted service starts or a request is served (<c>04</c> MSG-LIB-003):
/// the configuration, the declarations, then the seams.
/// </summary>
internal sealed class MessagingStartup(
    IConfiguration configuration,
    ConfigurationState state,
    IOptionsMonitor<MessagingOptions> options,
    Declarations declarations,
    IServiceProvider services) : IHostedLifecycleService
{
    public async Task StartingAsync(CancellationToken cancellationToken)
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

        if (SeamCheck.Missing(services.GetRequiredService<IServiceProviderIsService>(), declarations, current) is { } seam)
        {
            throw Failure(Codes.DeclarationMissing, ("key", seam));
        }

        AsyncServiceScope scope = services.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            if (SeamCheck.InvalidProtector(scope.ServiceProvider.GetServices<IContentProtector>()) is { } protector)
            {
                throw Failure(Codes.DeclarationInvalid, ("declaration", protector), ("field", "Id"));
            }
        }
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