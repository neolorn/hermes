using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Operations;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hermes.Conversations;

/// <summary>
/// Runs the package's checks in <see cref="StartingAsync"/>, after the engine's, which
/// <c>AddHermes</c> registers before it (M-031): no key under <c>Hermes:Conversations:</c> is
/// one the package does not know, read raw from the configuration given to <c>AddHermes</c>.
/// </summary>
internal sealed class ConversationsStartup(IServiceProvider services) : IHostedLifecycleService
{
    public Task StartingAsync(CancellationToken cancellationToken)
    {
        ConfigurationState state = services.GetRequiredService<ConfigurationState>();
        if (ConversationsConfiguration.UnknownKey(state.Section) is { } key)
        {
            throw new MessagingStartupException(Codes.ConfigurationInvalid, new Dictionary<string, string>(StringComparer.Ordinal) { ["key"] = key });
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
}