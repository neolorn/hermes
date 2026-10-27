using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hermes.Tests;

/// <summary>A host service, registered before <c>AddHermes</c>, that sends <paramref name="send"/> in its start (<c>04</c> MSG-LIB-003 AC6).</summary>
internal sealed class SendingService(Send send, IServiceScopeFactory scopes) : IHostedService
{
    internal Result<SendReceipt>? Result { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        AsyncServiceScope scope = scopes.CreateAsyncScope();
        await using (scope)
        {
            Result = await scope.ServiceProvider.GetRequiredService<IMessages>().SendAsync(send, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}