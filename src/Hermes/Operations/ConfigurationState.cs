using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

using Hermes.Channels;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Hermes.Operations;

/// <summary>
/// The engine's configuration values in force (<c>06</c> MSG-OPS-001): at the start, the host's
/// section <c>Hermes</c> as the start's checks passed it; on each reload, every value within
/// its rule, while a start-only key keeps its value until the next start and a reloaded value
/// outside its rule leaves the last valid one in force and is answered by
/// <see cref="InvalidKeys"/> until it is put right. The options are bound from
/// <see cref="Effective"/>, which holds only values that read.
/// </summary>
internal sealed class ConfigurationState : IOptionsChangeTokenSource<MessagingOptions>, IDisposable
{
    private readonly IConfigurationSection hermes;
    private readonly bool publicBaseAddressRequired;
    private readonly Func<IReadOnlyCollection<TimeZoneInfo>> zones;
    private readonly Lock gate = new();
    private Dictionary<string, string> inForce;
    private CancellationTokenSource changed = new();
    private IDisposable? watch;

    internal ConfigurationState(IConfigurationSection hermes, bool publicBaseAddressRequired, Func<IReadOnlyCollection<TimeZoneInfo>> zones)
    {
        this.hermes = hermes;
        this.publicBaseAddressRequired = publicBaseAddressRequired;
        this.zones = zones;
        inForce = Readable(Raw(hermes));
        Effective = Build(inForce);
    }

    /// <summary>The values in force, under the section's own key names, which the options are bound from.</summary>
    internal IConfiguration Effective { get; private set; }

    /// <summary>The keys whose reloaded value is outside its rule, in <c>08</c> section 4's order.</summary>
    internal ImmutableArray<string> InvalidKeys { get; private set; } = [];

    /// <inheritdoc/>
    public string Name => Options.DefaultName;

    /// <summary>Takes the section's values as the start's checks passed them, and follows its reloads from now on.</summary>
    internal void Start()
    {
        lock (gate)
        {
            inForce = Raw(hermes);
            Publish();
            watch ??= ChangeToken.OnChange(hermes.GetReloadToken, Reload);
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
            Dictionary<string, string> raw = Raw(hermes);
            Dictionary<string, string> candidate = new(inForce, StringComparer.Ordinal);
            HashSet<string> invalid = new(StringComparer.Ordinal);

            foreach (ConfigurationKey key in ConfigurationRules.Keys.Where(key => !key.StartOnly))
            {
                if (!raw.TryGetValue(key.Path, out string? text))
                {
                    candidate.Remove(key.Path);
                }
                else if (ConfigurationCheck.Reads(key, text))
                {
                    candidate[key.Path] = text;
                }
                else
                {
                    invalid.Add(key.Path);
                }
            }

            Take(raw, candidate, invalid, "TimeZone", text => ConfigurationCheck.IsTimeZone(text, zones()), required: false);
            Take(raw, candidate, invalid, "PublicBaseAddress", ConfigurationCheck.IsPublicBaseAddress, publicBaseAddressRequired);

            // A rule over other keys that no longer holds keeps every key it reads at its value in force.
            for (ConfigurationRule? broken = Broken(candidate); broken is not null; broken = Broken(candidate))
            {
                invalid.Add(broken.Path);
                foreach (string path in broken.Refers.Prepend(broken.Path))
                {
                    if (inForce.TryGetValue(path, out string? text))
                    {
                        candidate[path] = text;
                    }
                    else
                    {
                        candidate.Remove(path);
                    }
                }
            }

            inForce = candidate;
            InvalidKeys = [.. Order(invalid)];
            Publish();
        }
    }

    private static ConfigurationRule? Broken(Dictionary<string, string> candidate)
    {
        Func<string, object> value = ConfigurationCheck.Values(path => candidate.GetValueOrDefault(path));
        return ConfigurationRules.Rules.FirstOrDefault(rule => !rule.Holds(value));
    }

    private static void Take(Dictionary<string, string> raw, Dictionary<string, string> candidate, HashSet<string> invalid, string path, Func<string, bool> within, bool required)
    {
        if (raw.TryGetValue(path, out string? text) ? within(text) : !required)
        {
            if (text is null)
            {
                candidate.Remove(path);
            }
            else
            {
                candidate[path] = text;
            }
        }
        else
        {
            invalid.Add(path);
        }
    }

    private void Publish()
    {
        Effective = Build(inForce);
        CancellationTokenSource previous = changed;
        changed = new CancellationTokenSource();
        previous.Cancel();
    }

    private static IEnumerable<string> Order(HashSet<string> invalid)
    {
        string[] order = ["PublicBaseAddress", "TimeZone", .. ConfigurationRules.Keys.Select(key => key.Path)];
        return order.Where(invalid.Contains);
    }

    /// <summary>The section's values of the engine's keys, by path under the section, a list's items under their own paths.</summary>
    private static Dictionary<string, string> Raw(IConfigurationSection hermes)
    {
        Dictionary<string, string> raw = new(StringComparer.Ordinal);
        foreach (string path in ConfigurationRules.Keys.Select(key => key.Path).Concat(["PublicBaseAddress", "TimeZone", "Templates:Path", "DefaultLanguage", .. ConfigurationRules.WriteVersions]))
        {
            if (hermes[path] is string text)
            {
                raw[path] = text;
            }
        }

        foreach (IConfigurationSection language in hermes.GetSection("Languages").GetChildren())
        {
            if (language.Value is string tag)
            {
                raw[$"Languages:{language.Key}"] = tag;
            }
        }

        return raw;
    }

    /// <summary>The values of <paramref name="raw"/> that read, so binding them never throws.</summary>
    private static Dictionary<string, string> Readable(Dictionary<string, string> raw)
    {
        Dictionary<string, ConfigurationKey> keys = ConfigurationRules.Keys.ToDictionary(key => key.Path, StringComparer.Ordinal);
        return raw.Where(entry => !keys.TryGetValue(entry.Key, out ConfigurationKey? key) || key.Read(entry.Value) is not null)
            .Where(entry => !string.Equals(entry.Key, "PublicBaseAddress", StringComparison.Ordinal) || ConfigurationCheck.IsPublicBaseAddress(entry.Value))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
    }

    private static IConfiguration Build(Dictionary<string, string> values)
    {
        // A write version that does not read is left unbound; it is checked where its key is needed (06 MSG-OPS-008).
        IEnumerable<KeyValuePair<string, string?>> bound = values
            .Where(entry => !ConfigurationRules.WriteVersions.Contains(entry.Key, StringComparer.Ordinal) || ConfigurationRules.ReadVersion(entry.Value) is not null)
            .Select(entry => new KeyValuePair<string, string?>(entry.Key, entry.Value));
        return new ConfigurationBuilder().AddInMemoryCollection(bound).Build();
    }
}