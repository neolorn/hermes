using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hermes.Operations;

/// <summary>
/// The key-table timer every process that registers the engine runs, whether or not it runs
/// jobs: it writes the write versions' last-made instant and the token lifetime every
/// <c>Hermes:Dispatch:Interval</c> and when the process stops (<c>06</c> MSG-OPS-008).
/// </summary>
internal sealed partial class KeyTableTimer(KeyTable table, IOptionsMonitor<MessagingOptions> options, TimeProvider clock, ILogger<KeyTableTimer> logger) : BackgroundService
{
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        await WriteAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(options.CurrentValue.Dispatch.Interval, clock);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            TimeSpan interval = options.CurrentValue.Dispatch.Interval;
            if (timer.Period != interval)
            {
                timer.Period = interval;
            }

            await WriteAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task WriteAsync(CancellationToken cancellationToken)
    {
        try
        {
            await table.WriteMadeAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbException failure)
        {
            WriteFailed(failure);
        }
    }

    [LoggerMessage(EventId = 10, EventName = "KeyTableWriteFailed", Level = LogLevel.Warning, Message = "The key table's last-made instants were not written; the next interval writes them.")]
    private partial void WriteFailed(Exception failure);
}