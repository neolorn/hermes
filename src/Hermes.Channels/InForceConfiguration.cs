using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Hermes.Channels;

/// <summary>
/// The channels' values in force as a configuration the binder reads, each value under its
/// path, keys compared without regard to case as the configuration's are. Hermes.Channels
/// takes the configuration abstractions alone (<c>07</c> MSG-ENG-009), so it holds the
/// values itself; they change only by a new instance, so its reload token never fires.
/// </summary>
internal sealed class InForceConfiguration : IConfiguration
{
    private readonly Dictionary<string, string> values;

    internal InForceConfiguration(IEnumerable<KeyValuePair<string, string>> values)
    {
        this.values = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
    }

    public string? this[string key]
    {
        get => values.GetValueOrDefault(key);
        set => throw new NotSupportedException("The values in force change only when the configuration reloads.");
    }

    public IConfigurationSection GetSection(string key)
    {
        return new Section(this, key);
    }

    public IEnumerable<IConfigurationSection> GetChildren()
    {
        return Children(null);
    }

    public IChangeToken GetReloadToken()
    {
        return NeverChanges.Token;
    }

    private IEnumerable<IConfigurationSection> Children(string? parent)
    {
        string prefix = parent is null ? string.Empty : parent + ConfigurationPath.KeyDelimiter;
        return values.Keys
            .Where(path => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && path.Length > prefix.Length)
            .Select(path => path[prefix.Length..].Split(ConfigurationPath.KeyDelimiter)[0])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(key => new Section(this, prefix + key));
    }

    private sealed class Section(InForceConfiguration root, string path) : IConfigurationSection
    {
        public string Key => ConfigurationPath.GetSectionKey(path);

        public string Path => path;

        public string? Value
        {
            get => root[path];
            set => throw new NotSupportedException("The values in force change only when the configuration reloads.");
        }

        public string? this[string key]
        {
            get => root[ConfigurationPath.Combine(path, key)];
            set => throw new NotSupportedException("The values in force change only when the configuration reloads.");
        }

        public IConfigurationSection GetSection(string key)
        {
            return new Section(root, ConfigurationPath.Combine(path, key));
        }

        public IEnumerable<IConfigurationSection> GetChildren()
        {
            return root.Children(path);
        }

        public IChangeToken GetReloadToken()
        {
            return NeverChanges.Token;
        }
    }

    private sealed class NeverChanges : IChangeToken
    {
        internal static readonly NeverChanges Token = new();

        public bool HasChanged => false;

        public bool ActiveChangeCallbacks => false;

        public IDisposable RegisterChangeCallback(Action<object?> callback, object? state)
        {
            return Registration.None;
        }

        private sealed class Registration : IDisposable
        {
            internal static readonly Registration None = new();

            public void Dispose()
            {
            }
        }
    }
}