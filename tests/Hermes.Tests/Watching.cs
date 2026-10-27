using System;
using System.Threading.Tasks;

using Npgsql;

using Xunit;

namespace Hermes.Tests;

/// <summary>Drives the jobs of a test's host: moves its clock and waits for the run that starts to complete.</summary>
internal static class Watching
{
    /// <summary>The default <c>Hermes:Dispatch:Interval</c>, the watch job's interval.</summary>
    internal static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    /// <summary>Moves <paramref name="host"/>'s clock on by <paramref name="advance"/>, one interval where none is given, and waits for the watch run it starts.</summary>
    internal static Task RunAsync(EngineHost host, TimeSpan? advance = null)
    {
        return RunAsync(host, "watch", advance ?? Interval);
    }

    /// <summary>Moves <paramref name="host"/>'s clock on by <paramref name="advance"/> and waits for a run of <paramref name="job"/> to complete.</summary>
    internal static async Task RunAsync(EngineHost host, string job, TimeSpan advance)
    {
        DateTimeOffset? before = await CompletedAsync(host, job);
        host.Clock.Advance(advance);
        await UntilAsync(async () => await CompletedAsync(host, job) is { } completed && (before is null || completed > before));
    }

    internal static async Task<DateTimeOffset?> CompletedAsync(EngineHost host, string job)
    {
        await using NpgsqlConnection connection = new(host.Connection);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new("SELECT last_completed_at FROM messaging.jobs WHERE name = @job", connection);
        command.Parameters.AddWithValue("job", job);
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) is DateTime completed ? new DateTimeOffset(completed, TimeSpan.Zero) : null;
    }

    internal static async Task UntilAsync(Func<Task<bool>> condition)
    {
        for (int tries = 0; tries < 400; tries++)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(25, TestContext.Current.CancellationToken);
        }

        Assert.Fail("The condition did not come to hold.");
    }
}