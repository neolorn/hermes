using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hermes.Sms.Cequens;

/// <summary>
/// Checks the credential in <see cref="StartingAsync"/>, before any hosted service's
/// <c>StartAsync</c> (<c>02</c> MSG-SMS-007): a secret source that does not answer
/// <c>smsCequens</c>, or none registered, stops the start as unavailable, and a highest
/// version outside the token's form as invalid, each with the key <c>smsCequens</c>.
/// </summary>
internal sealed class CequensStartup(IServiceProvider services) : IHostedLifecycleService
{
    private const string SecretUnavailable = "messaging.startup.secretunavailable";

    private const string SecretInvalid = "messaging.startup.secretinvalid";

    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        AsyncServiceScope scope = services.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            SecretVersion? highest = scope.ServiceProvider.GetService<ISecretSource>() is { } secrets
                ? await CequensToken.HighestAsync(secrets, cancellationToken).ConfigureAwait(false)
                : null;
            if (highest is null)
            {
                throw Failure(SecretUnavailable);
            }

            if (!CequensToken.IsInForm(highest.Value.Span))
            {
                throw Failure(SecretInvalid);
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

    private static MessagingStartupException Failure(string code)
    {
        return new MessagingStartupException(code, new Dictionary<string, string>(StringComparer.Ordinal) { ["key"] = CequensToken.Name });
    }
}