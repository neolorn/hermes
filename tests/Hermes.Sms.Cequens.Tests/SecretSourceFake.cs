using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

namespace Hermes.Sms.Cequens.Tests;

/// <summary>A secret source holding the secrets a test gives it, by name, which the test may change while the host runs.</summary>
internal sealed class SecretSourceFake : ISecretSource
{
    private readonly ConcurrentDictionary<string, SecretVersions> secrets = new(StringComparer.Ordinal);

    /// <summary>Holds <paramref name="versions"/> as the secret <paramref name="name"/>, each with the number given, in place of what it held.</summary>
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

    public ValueTask<SecretVersions?> ReadAsync(string name, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(secrets.GetValueOrDefault(name));
    }
}