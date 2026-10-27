using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Channels;
using Hermes.Core;
using Hermes.Operations;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Hermes.Sending;

/// <summary>
/// Runs passes (<c>01</c> MSG-SEND-007): each claims its channel's due deliveries with their
/// places in the rate, and works them one after another, as <see cref="DispatchPass"/> does.
/// The push channel has no pass in this release's build yet: its check 9 needs the
/// subscriptions of <c>02</c> MSG-PUSH-003.
/// </summary>
internal sealed partial class DispatchPasses
{
    private readonly IReadOnlyList<string> essential;

    private readonly IReadOnlyList<string> others;

    private readonly Dictionary<string, KindDeclaration> kinds;

    public DispatchPasses(
        MessagingDatabase database,
        Declarations declarations,
        IOptionsMonitor<MessagingOptions> options,
        IOptionsMonitor<ChannelsOptions> channels,
        IServiceScopeFactory scopes,
        Alerts alerts,
        Notices notices,
        MessagingMetrics metrics,
        TimeProvider clock,
        ILogger<DispatchPasses> logger)
    {
        ArgumentNullException.ThrowIfNull(declarations);
        Database = database;
        Options = options;
        Channels = channels;
        Scopes = scopes;
        Alerts = alerts;
        Notices = notices;
        Metrics = metrics;
        Clock = clock;
        Logger = logger;
        HashSet<string> essentialCategories = [.. declarations.Categories.Where(category => category.Essential).Select(category => category.Name)];
        essential = [.. declarations.Kinds.Where(kind => essentialCategories.Contains(kind.Category)).Select(kind => kind.Name)];
        others = [.. declarations.Kinds.Where(kind => !essentialCategories.Contains(kind.Category)).Select(kind => kind.Name), .. declarations.Retired];
        kinds = declarations.Kinds.ToDictionary(kind => kind.Name, StringComparer.Ordinal);
    }

    /// <summary>The channels a pass runs on.</summary>
    internal static IReadOnlyList<Channel> Dispatched { get; } = [Channel.Email, Channel.Sms];

    internal MessagingDatabase Database { get; }

    internal IOptionsMonitor<MessagingOptions> Options { get; }

    internal IOptionsMonitor<ChannelsOptions> Channels { get; }

    internal IServiceScopeFactory Scopes { get; }

    internal Alerts Alerts { get; }

    internal Notices Notices { get; }

    internal MessagingMetrics Metrics { get; }

    internal TimeProvider Clock { get; }

    internal ILogger Logger { get; }

    /// <summary>Runs one pass of <paramref name="channel"/>; a token already cancelled claims nothing.</summary>
    internal Task<PassOutcome> RunAsync(Channel channel, CancellationToken stopping)
    {
        stopping.ThrowIfCancellationRequested();
        return RunPassAsync(channel, stopping);
    }

    private async Task<PassOutcome> RunPassAsync(Channel channel, CancellationToken stopping)
    {
        using DispatchPass pass = new(this, channel, stopping);
        return await pass.RunAsync().ConfigureAwait(false);
    }

    /// <summary>The kind <paramref name="name"/> as this application declares it, or none for a retired one.</summary>
    internal KindDeclaration? Kind(string name)
    {
        return kinds.GetValueOrDefault(name);
    }

    /// <summary>
    /// Claims, for <paramref name="holder"/>, the channel's due deliveries up to the lesser of the
    /// batch and what the second's counter leaves of its rate, essential kinds first, adding them
    /// to the counter; a lock that times out claims nothing.
    /// </summary>
    internal async Task<(List<DeliveryRow> Rows, DateTimeOffset Second, int Limit)> ClaimAsync(Channel channel, Guid holder, MessagingOptions current, CancellationToken cancellationToken)
    {
        string spelling = Spellings.Of(channel);
        ChannelsOptions channelOptions = Channels.CurrentValue;
        int rate = channel == Channel.Email ? channelOptions.Email.Rate : channelOptions.Sms.Rate;
        try
        {
            return await LibraryTransactions.RunAsync(
                Database,
                current,
                async (context, token) =>
                {
                    await DeliveryClaims.LockTimeoutAsync(context, current.Dispatch.Timeout, token).ConfigureAwait(false);
                    DateTimeOffset now = Clock.GetUtcNow();
                    DateTimeOffset second = DeliveryClaims.Second(now);
                    int count = await DeliveryClaims.CounterAsync(context, spelling, second, token).ConfigureAwait(false);
                    int limit = Math.Max(0, Math.Min(current.Dispatch.BatchSize, rate - count));
                    List<DeliveryRow> rows = [];
                    if (limit > 0 && essential.Count > 0)
                    {
                        rows.AddRange(Ordered(await DeliveryClaims.ClaimAsync(context, spelling, essential, limit, holder, current.Dispatch.ClaimTimeout, now, token).ConfigureAwait(false)));
                    }

                    if (limit > rows.Count && others.Count > 0)
                    {
                        rows.AddRange(Ordered(await DeliveryClaims.ClaimAsync(context, spelling, others, limit - rows.Count, holder, current.Dispatch.ClaimTimeout, now, token).ConfigureAwait(false)));
                    }

                    if (rows.Count > 0)
                    {
                        await DeliveryClaims.CountAsync(context, spelling, second, rows.Count, token).ConfigureAwait(false);
                    }

                    return (rows, second, limit);
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception failure) when (IsLockTimeout(failure))
        {
            ClaimLockTimedOut(Logger, spelling);
            return ([], default, 0);
        }
    }

    /// <summary>Whether <paramref name="failure"/> is PostgreSQL's <c>lock_timeout</c> (55P03), however EF Core wraps it.</summary>
    internal static bool IsLockTimeout(Exception failure)
    {
        for (Exception? cause = failure; cause is not null; cause = cause.InnerException)
        {
            if (cause is PostgresException { SqlState: PostgresErrorCodes.LockNotAvailable })
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<DeliveryRow> Ordered(List<DeliveryRow> rows)
    {
        return rows.OrderBy(row => row.DueAt).ThenBy(row => row.Id);
    }

    [LoggerMessage(EventId = 30, EventName = "ClaimLockTimedOut", Level = LogLevel.Warning, Message = "The rate counter's lock for {Channel} timed out; the pass claimed nothing.")]
    private static partial void ClaimLockTimedOut(ILogger logger, string channel);
}