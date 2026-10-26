using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Hermes.Channels;

/// <summary>
/// The channels' configuration values in force (<c>06</c> MSG-OPS-001): at the start, the keys
/// the application's use reads, as the start's checks passed them; on each reload, every
/// value within its rule, while a reloaded value outside its rule, or a required one removed,
/// leaves the last valid one in force and is answered by <see cref="InvalidKeys"/> until it
/// is put right. The options are bound from <see cref="Effective"/>, which holds only values
/// that read. A shipped provider called before the start's checks complete throws.
/// </summary>
internal sealed class ChannelsConfiguration : IOptionsChangeTokenSource<ChannelsOptions>, IDisposable
{
    private readonly IConfigurationSection hermes;
    private readonly Lock gate = new();
    private Dictionary<string, string> inForce;
    private CancellationTokenSource changed = new();
    private IDisposable? watch;
    private Func<string, bool> isSender = _ => true;

    internal ChannelsConfiguration(IConfigurationSection hermes)
    {
        this.hermes = hermes;
        inForce = ChannelsCheck.Readable(ChannelsCheck.Raw(hermes));
        Effective = Build(inForce);
    }

    /// <summary>The section <c>Hermes</c> of the configuration given to the registration.</summary>
    internal IConfigurationSection Section => hermes;

    /// <summary>The values in force, under the section's own key names, which the options are bound from.</summary>
    internal IConfiguration Effective { get; private set; }

    /// <summary>The keys whose reloaded value is outside its rule, in <c>08</c> section 4's order.</summary>
    internal ImmutableArray<string> InvalidKeys { get; private set; } = [];

    /// <summary>The channels and shipped providers the application uses, once the start's checks passed.</summary>
    internal ChannelUse? Use { get; private set; }

    /// <inheritdoc/>
    public string Name => Options.DefaultName;

    /// <summary>
    /// Takes the values of the keys <paramref name="use"/> reads as the start's checks passed
    /// them, and follows the section's reloads from now on, checking a sender against
    /// <paramref name="sender"/> too.
    /// </summary>
    internal void Start(ChannelUse use, Func<string, bool> sender)
    {
        lock (gate)
        {
            Use = use;
            isSender = sender;
            inForce = Of(use, ChannelsCheck.Raw(hermes));
            Publish();
            watch ??= ChangeToken.OnChange(hermes.GetReloadToken, Reload);
        }
    }

    /// <summary>Throws where the start's checks have not completed, as a shipped provider called before them does (<c>04</c> MSG-LIB-003).</summary>
    internal void ThrowIfNotStarted()
    {
        if (Use is null)
        {
            throw new InvalidOperationException("The channels' startup checks have not completed; the host has not started.");
        }
    }

    /// <inheritdoc/>
    public IChangeToken GetChangeToken()
    {
        lock (gate)
        {
            return new CancellationChangeToken(changed.Token);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        watch?.Dispose();
        changed.Dispose();
    }

    /// <summary>Takes the section's reloaded values that are within their rules.</summary>
    internal void Reload()
    {
        lock (gate)
        {
            if (Use is not { } use)
            {
                return;
            }

            Dictionary<string, string> raw = Of(use, ChannelsCheck.Raw(hermes));
            Dictionary<string, string> candidate = new(inForce, StringComparer.Ordinal);
            IReadOnlyList<string> required = ChannelKeys.Required(use);
            HashSet<string> invalid = new(StringComparer.Ordinal);
            foreach (string path in ChannelKeys.Read(use))
            {
                if (ChannelsCheck.Holds(raw, path, required.Contains(path, StringComparer.Ordinal), isSender))
                {
                    Replace(candidate, raw, path);
                }
                else
                {
                    invalid.Add(path);
                }
            }

            // A rule over other keys that no longer holds keeps every key it reads at its value in force.
            for (ConfigurationRule? broken = Broken(use, candidate); broken is not null; broken = Broken(use, candidate))
            {
                invalid.Add(broken.Path);
                foreach (string path in broken.Refers.Prepend(broken.Path))
                {
                    Replace(candidate, inForce, path);
                }
            }

            inForce = candidate;
            InvalidKeys = [.. ChannelKeys.Order.Where(invalid.Contains)];
            Publish();
        }
    }

    private static ConfigurationRule? Broken(ChannelUse use, Dictionary<string, string> candidate)
    {
        Func<string, object> value = ChannelsCheck.Values(path => candidate.GetValueOrDefault(path));
        IReadOnlyList<string> read = ChannelKeys.Read(use);
        return ChannelKeys.Rules.FirstOrDefault(rule => read.Contains(rule.Path, StringComparer.Ordinal) && !rule.Holds(value));
    }

    /// <summary>Sets the key at <paramref name="path"/> in <paramref name="target"/>, a list's items together, to its value in <paramref name="source"/>.</summary>
    private static void Replace(Dictionary<string, string> target, IReadOnlyDictionary<string, string> source, string path)
    {
        string prefix = path + ":";
        foreach (string entry in target.Keys.Where(entry => string.Equals(entry, path, StringComparison.Ordinal) || entry.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        {
            target.Remove(entry);
        }

        foreach (KeyValuePair<string, string> entry in source.Where(entry => string.Equals(entry.Key, path, StringComparison.Ordinal) || entry.Key.StartsWith(prefix, StringComparison.Ordinal)))
        {
            target[entry.Key] = entry.Value;
        }
    }

    /// <summary>The values of <paramref name="raw"/> of the keys <paramref name="use"/> reads.</summary>
    private static Dictionary<string, string> Of(ChannelUse use, Dictionary<string, string> raw)
    {
        IReadOnlyList<string> read = ChannelKeys.Read(use);
        return raw.Where(entry => read.Any(path => string.Equals(entry.Key, path, StringComparison.Ordinal) || entry.Key.StartsWith(path + ":", StringComparison.Ordinal)))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
    }

    private void Publish()
    {
        Effective = Build(inForce);
        CancellationTokenSource previous = changed;
        changed = new CancellationTokenSource();
        previous.Cancel();
    }

    private static InForceConfiguration Build(Dictionary<string, string> values)
    {
        return new InForceConfiguration(values);
    }
}