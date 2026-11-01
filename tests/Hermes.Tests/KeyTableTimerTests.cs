using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Operations;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Npgsql;

using Xunit;

namespace Hermes.Tests;

/// <summary>The key-table timer's stop (<c>06</c> MSG-OPS-008): a tick in flight ends before the host goes on to dispose what the tick uses.</summary>
[Trait("Category", "integration")]
public sealed class KeyTableTimerTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task StopAsync_MidTickWithTheHostsTimeOver_EndsTheTickBeforeItReturns()
    {
        string connection = await EngineHost.DatabaseAsync(postgres);
        ManualClock clock = new(EngineHost.Start);
        await using EngineHost host = await EngineHost.StartAsync(connection, clock: clock);
        KeyTableTimer timer = host.Services.GetServices<IHostedService>().OfType<KeyTableTimer>().Single();

        // Another session holds the key table's rows, so the tick's write waits on them.
        await using NpgsqlConnection holder = new(connection);
        await holder.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlTransaction held = await holder.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await using (NpgsqlCommand lockRows = new("SELECT * FROM messaging.key_versions FOR UPDATE", holder, held))
        {
            await lockRows.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        clock.Advance(TimeSpan.FromMinutes(1));
        await WaitBlockedAsync(connection, holder.ProcessID);

        // The host's shutdown time is already over, as when it stops past its timeout.
        await timer.StopAsync(new CancellationToken(canceled: true));

        Assert.True(timer.ExecuteTask!.IsCompleted);
        await held.RollbackAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Waits until a session other than <paramref name="holder"/> waits on a lock.</summary>
    private static async Task WaitBlockedAsync(string connection, int holder)
    {
        await using NpgsqlConnection watcher = new(connection);
        await watcher.OpenAsync(TestContext.Current.CancellationToken);
        for (int attempt = 0; attempt < 200; attempt++)
        {
            await using NpgsqlCommand waiting = new("SELECT count(*) FROM pg_locks WHERE NOT granted AND pid <> @holder", watcher);
            waiting.Parameters.AddWithValue("holder", holder);
            if ((long)(await waiting.ExecuteScalarAsync(TestContext.Current.CancellationToken))! > 0)
            {
                return;
            }

            await Task.Delay(25, TestContext.Current.CancellationToken);
        }

        Assert.Fail("The tick never waited on the key table's rows.");
    }
}