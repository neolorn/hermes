using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

namespace Hermes.Tests;

/// <summary>A secret source holding the secrets a test gives it, by name.</summary>
internal sealed class SecretSourceFake : ISecretSource
{
    private readonly Dictionary<string, SecretVersions> secrets = new(StringComparer.Ordinal);

    /// <summary>Holds <paramref name="versions"/> as the secret <paramref name="name"/>, version 1 first.</summary>
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

    public ValueTask<SecretVersions?> ReadAsync(string name, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(secrets.GetValueOrDefault(name));
    }
}