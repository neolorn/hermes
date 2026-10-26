using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

namespace Hermes.Tests;

/// <summary>A secret source holding the secrets a test gives it, by name, and keeping the names read.</summary>
internal sealed class SecretSourceFake : ISecretSource
{
    private readonly Dictionary<string, SecretVersions> secrets = new(StringComparer.Ordinal);

    private readonly ConcurrentQueue<string> read = new();

    /// <summary>The names read, in order.</summary>
    internal IReadOnlyCollection<string> Read => read;

    /// <summary>Holds <paramref name="versions"/> as the secret <paramref name="name"/>, numbered from 1.</summary>
    internal SecretSourceFake With(string name, params byte[][] versions)
    {
        List<SecretVersion> held = [];
        for (int index = 0; index < versions.Length; index++)
        {
            held.Add(new SecretVersion { Version = index + 1, Value = versions[index] });
        }

        secrets[name] = new SecretVersions { Versions = held };
        return this;
    }

    /// <summary>Holds <paramref name="versions"/> as the secret <paramref name="name"/>, each with the number given.</summary>
    internal SecretSourceFake WithVersions(string name, params (int Version, byte[] Value)[] versions)
    {
        List<SecretVersion> held = [];
        foreach ((int version, byte[] value) in versions)
        {
            held.Add(new SecretVersion { Version = version, Value = value });
        }

        secrets[name] = new SecretVersions { Versions = held };
        return this;
    }

    /// <summary>Holds no secret <paramref name="name"/>.</summary>
    internal SecretSourceFake Without(string name)
    {
        secrets.Remove(name);
        return this;
    }

    public ValueTask<SecretVersions?> ReadAsync(string name, CancellationToken cancellationToken)
    {
        read.Enqueue(name);
        return ValueTask.FromResult(secrets.GetValueOrDefault(name));
    }
}