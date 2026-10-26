using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.Configuration;

using Npgsql;

namespace Hermes.Operations;

/// <summary>Reads the secrets the used parts need when the host starts (<c>06</c> MSG-OPS-008).</summary>
internal static class SecretCheck
{
    internal const string Connection = "connection";

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// The connection string, the highest version of <c>connection</c>; a secret not answered
    /// stops the start as unavailable, one that is not a connection string in UTF-8 as invalid.
    /// </summary>
    internal static async Task<string> ConnectionAsync(ISecretSource secrets, CancellationToken cancellationToken)
    {
        IReadOnlyList<SecretVersion> versions = await ReadAsync(secrets, Connection, cancellationToken).ConfigureAwait(false);
        SecretVersion highest = versions.MaxBy(version => version.Version)!;
        try
        {
            string connection = StrictUtf8.GetString(highest.Value.Span);
            _ = new NpgsqlConnectionStringBuilder(connection);
            return connection;
        }
        catch (Exception invalid) when (invalid is DecoderFallbackException or ArgumentException)
        {
            throw MessagingStartup.Failure(Codes.SecretInvalid, ("key", Connection));
        }
    }

    /// <summary>
    /// Reads each of <paramref name="needed"/>, in order: a key not answered stops the start as
    /// unavailable; a version numbered below 1 or twice, or outside the key's form, as invalid;
    /// a write version absent where more than one version is held, or naming none held, as a
    /// configuration value outside its rule (<c>08</c> section 4).
    /// </summary>
    internal static async Task<IReadOnlyList<HeldKey>> KeysAsync(ISecretSource secrets, IConfiguration hermes, IReadOnlyList<string> needed, CancellationToken cancellationToken)
    {
        List<HeldKey> keys = [];
        foreach (string name in needed)
        {
            IReadOnlyList<SecretVersion> versions = await ReadAsync(secrets, name, cancellationToken).ConfigureAwait(false);
            SortedDictionary<int, byte[]> held = [];
            foreach (SecretVersion version in versions)
            {
                if (version.Version < 1 || held.ContainsKey(version.Version) || !KeyForms.InForm(name, version.Value.Span))
                {
                    throw MessagingStartup.Failure(Codes.SecretInvalid, ("key", name));
                }

                held[version.Version] = version.Value.ToArray();
            }

            keys.Add(new HeldKey(name, held, WriteVersion(hermes, name, held)));
        }

        return keys;
    }

    private static int WriteVersion(IConfiguration hermes, string name, SortedDictionary<int, byte[]> held)
    {
        string path = KeyNeeds.WriteVersionPath(name);
        string? configured = hermes[path];
        if (configured is null && held.Count == 1)
        {
            return held.Keys.Single();
        }

        if (configured is not null && ConfigurationRules.ReadVersion(configured) is int version && held.ContainsKey(version))
        {
            return version;
        }

        throw MessagingStartup.Failure(Codes.ConfigurationInvalid, ("key", path));
    }

    private static async Task<IReadOnlyList<SecretVersion>> ReadAsync(ISecretSource secrets, string name, CancellationToken cancellationToken)
    {
        SecretVersions? read = await secrets.ReadAsync(name, cancellationToken).ConfigureAwait(false);
        return read is { Versions.Count: > 0 } ? read.Versions : throw MessagingStartup.Failure(Codes.SecretUnavailable, ("key", name));
    }
}