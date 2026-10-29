using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

namespace Hermes.Testing;

/// <summary>
/// A secret source for tests (<c>04</c> MSG-LIB-008): it answers version 1 of
/// <c>contentKeys</c>, <c>hashKeys</c>, <c>tokenKeys</c>, <c>smsReportKey</c>,
/// <c>pushVapidKeys</c> and <c>emailSubmission</c>, each in its form and derived at
/// run time from the SHA-256 of its name, and neither connection secret, which a test sets.
/// </summary>
public sealed class FixedSecretSource : ISecretSource
{
    private const string Submission = "emailSubmission";

    private static readonly string[] Keys = ["contentKeys", "hashKeys", "tokenKeys", "smsReportKey", "pushVapidKeys"];

    /// <summary>The order of P-256's base point, above which no scalar is (SEC 2, section 2.4.2).</summary>
    private static readonly byte[] P256Order = Convert.FromHexString("FFFFFFFF00000000FFFFFFFFFFFFFFFFBCE6FAADA7179E84F3B9CAC2FC632551");

    private readonly Lock gate = new();

    private readonly Dictionary<string, SecretVersions> secrets = new(StringComparer.Ordinal);

    /// <summary>Answers the fixed secrets and no connection secret.</summary>
    public FixedSecretSource()
    {
        foreach (string key in Keys)
        {
            secrets[key] = First(Derived(key));
        }

        secrets[Submission] = First(Account());
    }

    /// <summary>Answers <paramref name="versions"/> for the secret <paramref name="name"/>, in place of what it answered.</summary>
    /// <param name="name">The secret's name.</param>
    /// <param name="versions">Its versions.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public void SetSecret(string name, SecretVersions versions)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(versions);
        lock (gate)
        {
            secrets[name] = versions;
        }
    }

    /// <summary>Answers nothing for the secret <paramref name="name"/>.</summary>
    /// <param name="name">The secret's name.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    public void RemoveSecret(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        lock (gate)
        {
            secrets.Remove(name);
        }
    }

    /// <inheritdoc />
    public ValueTask<SecretVersions?> ReadAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            return ValueTask.FromResult(secrets.GetValueOrDefault(name));
        }
    }

    private static SecretVersions First(byte[] value)
    {
        return new SecretVersions { Versions = [new SecretVersion { Version = 1, Value = value }] };
    }

    /// <summary>The SHA-256 of <paramref name="name"/>, hashed again until it is a P-256 scalar, so it is in the form of every key.</summary>
    private static byte[] Derived(string name)
    {
        byte[] value = SHA256.HashData(Encoding.UTF8.GetBytes(name));
        while (!value.AsSpan().ContainsAnyExcept((byte)0) || value.AsSpan().SequenceCompareTo(P256Order) >= 0)
        {
            value = SHA256.HashData(value);
        }

        return value;
    }

    /// <summary>The submission account, UTF-8 JSON of a user name and a password derived from the secret's name.</summary>
    private static byte[] Account()
    {
        using MemoryStream written = new();
        using (Utf8JsonWriter writer = new(written))
        {
            writer.WriteStartObject();
            writer.WriteString("userName", "hermes");
            writer.WriteString("password", Convert.ToHexString(Derived(Submission)));
            writer.WriteEndObject();
        }

        return written.ToArray();
    }
}