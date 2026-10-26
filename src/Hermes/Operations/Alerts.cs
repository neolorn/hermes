using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Hermes.Operations;

/// <summary>
/// Raises and clears the alert conditions of <c>08</c> section 5 through the alert sink
/// (<c>06</c> MSG-OPS-006): a condition is raised when it starts to hold and again each time
/// <c>Hermes:Alerts:Repeat</c> has passed since it was last raised while it holds, and cleared
/// once when it stops; its state per scope is kept in the alert table, on the library's own
/// connection, so the deployment raises it once however many processes see it.
/// </summary>
internal sealed partial class Alerts(MessagingDatabase database, IOptionsMonitor<MessagingOptions> options, IAlertSink sink, TimeProvider clock, ILogger<Alerts> logger)
{
    private const string Ensure = """
        INSERT INTO messaging.alerts (condition, scope, holding) VALUES (@condition, @scope, false)
        ON CONFLICT (condition, scope) DO NOTHING
        """;

    /// <summary>Writes the instant the condition holds until where later than the one recorded.</summary>
    private const string Extend = """
        UPDATE messaging.alerts SET holds_until = @until
        WHERE condition = @condition AND scope = @scope AND (holds_until IS NULL OR holds_until < @until)
        """;

    /// <summary>Marks the condition holding and raised now where it was not holding or was last raised a repeat ago.</summary>
    private const string Raise = """
        UPDATE messaging.alerts SET holding = true, last_raised_at = @now
        WHERE condition = @condition AND scope = @scope AND (NOT holding OR last_raised_at IS NULL OR last_raised_at <= @due)
        RETURNING 1 AS "Value"
        """;

    private const string Clear = """
        UPDATE messaging.alerts SET holding = false
        WHERE condition = @condition AND scope = @scope AND holding
        RETURNING 1 AS "Value"
        """;

    private readonly Lock gate = new();

    /// <summary>When this process last wrote each condition's instant, by condition and scope.</summary>
    private readonly Dictionary<(string Condition, string Scope), DateTimeOffset> written = [];

    /// <summary>The condition holds now in <paramref name="scope"/>: raised where it starts to hold or a repeat has passed.</summary>
    internal Task HoldingAsync(string condition, string? scope, IReadOnlyDictionary<string, string> details, CancellationToken cancellationToken)
    {
        return RaiseAsync(condition, scope, details, until: null, cancellationToken);
    }

    /// <summary>
    /// The condition holds, as this process meets it, until now plus twice its
    /// <c>Hermes:Dispatch:Interval</c>: written where later than the one recorded, at most once
    /// per that interval for each scope, and raised as <see cref="HoldingAsync"/> raises; the
    /// watch job clears it once that instant has passed.
    /// </summary>
    internal Task HoldsUntilAsync(string condition, string? scope, IReadOnlyDictionary<string, string> details, CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.GetUtcNow();
        TimeSpan interval = options.CurrentValue.Dispatch.Interval;
        lock (gate)
        {
            if (written.TryGetValue((condition, scope ?? string.Empty), out DateTimeOffset last) && now - last < interval)
            {
                return Task.CompletedTask;
            }

            written[(condition, scope ?? string.Empty)] = now;
        }

        return RaiseAsync(condition, scope, details, now + (2 * interval), cancellationToken);
    }

    /// <summary>The condition no longer holds in <paramref name="scope"/>: cleared once.</summary>
    internal async Task NotHoldingAsync(string condition, string? scope, CancellationToken cancellationToken)
    {
        List<int> cleared = await LibraryTransactions.RunAsync(
            database,
            options.CurrentValue,
            (context, token) => context.Database.SqlQueryRaw<int>(Clear, Named(condition, scope)).ToListAsync(token),
            cancellationToken).ConfigureAwait(false);
        if (cleared.Count > 0)
        {
            await TellAsync(condition, scope, details: null, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RaiseAsync(string condition, string? scope, IReadOnlyDictionary<string, string> details, DateTimeOffset? until, CancellationToken cancellationToken)
    {
        MessagingOptions current = options.CurrentValue;
        DateTimeOffset now = clock.GetUtcNow();
        List<int> raised = await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await context.Database.ExecuteSqlRawAsync(Ensure, Named(condition, scope), token).ConfigureAwait(false);
                if (until is not null)
                {
                    await context.Database.ExecuteSqlRawAsync(Extend, [.. Named(condition, scope), new NpgsqlParameter("until", until.Value)], token).ConfigureAwait(false);
                }

                return await context.Database.SqlQueryRaw<int>(
                    Raise,
                    [.. Named(condition, scope), new NpgsqlParameter("now", now), new NpgsqlParameter("due", now - current.Alerts.Repeat)]).ToListAsync(token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
        if (raised.Count > 0)
        {
            await TellAsync(condition, scope, details, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Tells the sink; a sink that fails is logged, the state staying as recorded, so the next raise is a repeat away.</summary>
    private async Task TellAsync(string condition, string? scope, IReadOnlyDictionary<string, string>? details, CancellationToken cancellationToken)
    {
        try
        {
            if (details is null)
            {
                await sink.ClearAsync(condition, scope, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await sink.RaiseAsync(condition, scope, details, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            SinkFailed(failure, condition, scope ?? "none");
        }
    }

    private static NpgsqlParameter[] Named(string condition, string? scope)
    {
        return [new NpgsqlParameter("condition", condition), new NpgsqlParameter("scope", scope ?? string.Empty)];
    }

    [LoggerMessage(EventId = 20, EventName = "AlertSinkFailed", Level = LogLevel.Error, Message = "The alert sink failed for {Condition}, scope {Scope}.")]
    private partial void SinkFailed(Exception failure, string condition, string scope);
}