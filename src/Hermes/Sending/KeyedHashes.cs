using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using Hermes.Operations;

namespace Hermes.Sending;

/// <summary>
/// The keyed hash of <c>06</c> MSG-OPS-008: HMAC-SHA-256 under a <c>hashKeys</c> version of a
/// domain, a zero byte and the value, made under the write version and looked up under every
/// version this process holds.
/// </summary>
internal sealed class KeyedHashes(HeldKeys keys)
{
    internal const string Email = "email";

    internal const string Sms = "sms";

    internal const string PushEndpoint = "push-endpoint";

    internal const string PushTopic = "push-topic";

    internal const string Person = "person";

    internal const string Idempotency = "idempotency";

    private HeldKey Held => keys.Find(KeyNeeds.HashKeys) ?? throw new InvalidOperationException("The keyed hash is used before the startup checks read hashKeys.");

    /// <summary>The write version.</summary>
    internal int WriteVersion => Held.WriteVersion;

    /// <summary>The versions this process holds, in ascending order.</summary>
    internal IReadOnlyList<int> Versions => [.. Held.Versions.Keys.Order()];

    /// <summary>The hash of <paramref name="value"/> in <paramref name="domain"/> under the write version.</summary>
    internal byte[] Write(string domain, ReadOnlySpan<byte> value)
    {
        HeldKey held = Held;
        return Of(held.Write, domain, value);
    }

    /// <summary>The hash of <paramref name="value"/> in <paramref name="domain"/> under <paramref name="version"/>.</summary>
    internal byte[] Under(int version, string domain, ReadOnlySpan<byte> value)
    {
        return Of(Held.Versions[version], domain, value);
    }

    /// <summary>The hash of <paramref name="value"/> in <paramref name="domain"/> under every held version, in ascending order of version.</summary>
    internal IReadOnlyList<(int Version, byte[] Hash)> Every(string domain, ReadOnlySpan<byte> value)
    {
        HeldKey held = Held;
        List<(int, byte[])> hashes = [];
        foreach (int version in held.Versions.Keys.Order())
        {
            hashes.Add((version, Of(held.Versions[version], domain, value)));
        }

        return hashes;
    }

    /// <summary>HMAC-SHA-256 under <paramref name="key"/> of <paramref name="domain"/>, a zero byte and <paramref name="value"/>.</summary>
    internal static byte[] Of(byte[] key, string domain, ReadOnlySpan<byte> value)
    {
        int length = Encoding.ASCII.GetByteCount(domain);
        byte[] message = new byte[length + 1 + value.Length];
        Encoding.ASCII.GetBytes(domain, message);
        value.CopyTo(message.AsSpan(length + 1));
        return HMACSHA256.HashData(key, message);
    }
}