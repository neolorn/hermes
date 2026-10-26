using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Channels;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Hermes.Operations;

/// <summary>
/// The conditions every process judges on its key-table timer (<c>06</c> MSG-OPS-001,
/// MSG-OPS-006, MSG-OPS-008): a reloaded value outside its rule (<c>configuration-invalid</c>,
/// raised until it is put right), a migration in the history its release does not know
/// (<c>newer-release</c>), and its <c>contentKeys</c> write version used for more than
/// <c>Hermes:Secrets:ContentKeys:MaxValues</c> values (<c>content-key-limit</c>).
/// </summary>
internal sealed class TimerChecks(ConfigurationState state, ChannelsConfiguration channels, Alerts alerts, MessagingDatabase database, HeldKeys keys, IOptionsMonitor<MessagingOptions> options)
{
    private const string ConfigurationInvalid = "configuration-invalid";

    /// <summary>The keys this process has seen outside their rule, which it clears once they read again.</summary>
    private ImmutableHashSet<string> invalid = ImmutableHashSet.Create<string>(StringComparer.Ordinal);

    internal async Task RunAsync(CancellationToken cancellationToken)
    {
        await ConfigurationAsync(cancellationToken).ConfigureAwait(false);

        if (await SchemaCheck.UnknownAsync(database, cancellationToken).ConfigureAwait(false) is { } migration)
        {
            await alerts.HoldsUntilAsync("newer-release", null, Details(("migration", migration)), cancellationToken).ConfigureAwait(false);
        }

        await ContentKeyLimitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ConfigurationAsync(CancellationToken cancellationToken)
    {
        ImmutableArray<string> now = [.. state.InvalidKeys, .. channels.InvalidKeys];
        foreach (string key in now)
        {
            await alerts.HoldingAsync(ConfigurationInvalid, key, Details(("key", key)), cancellationToken).ConfigureAwait(false);
            invalid = invalid.Add(key);
        }

        foreach (string key in invalid.Except(now))
        {
            await alerts.NotHoldingAsync(ConfigurationInvalid, key, cancellationToken).ConfigureAwait(false);
            invalid = invalid.Remove(key);
        }
    }

    private async Task ContentKeyLimitAsync(CancellationToken cancellationToken)
    {
        if (keys.Find(KeyNeeds.ContentKeys) is not { } key)
        {
            return;
        }

        long count;
        MessagingContext context = database.Context();
        await using (context.ConfigureAwait(false))
        {
            count = await context.KeyVersions.AsNoTracking()
                .Where(row => row.Key == KeyNeeds.ContentKeys && row.Version == key.WriteVersion)
                .Select(row => row.ValueCount)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        }

        if (count > options.CurrentValue.Secrets.ContentKeys.MaxValues)
        {
            string version = key.WriteVersion.ToString(CultureInfo.InvariantCulture);
            await alerts.HoldsUntilAsync("content-key-limit", version, Details(("version", version), ("count", count.ToString(CultureInfo.InvariantCulture))), cancellationToken).ConfigureAwait(false);
        }
    }

    private static Dictionary<string, string> Details(params (string Name, string Value)[] details)
    {
        return details.ToDictionary(detail => detail.Name, detail => detail.Value, StringComparer.Ordinal);
    }
}