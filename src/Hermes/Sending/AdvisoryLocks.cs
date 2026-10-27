using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Operations;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace Hermes.Sending;

/// <summary>
/// The transaction-scoped advisory locks of <c>01</c> MSG-SEND-009 and <c>02</c> MSG-PUSH-003
/// (<c>07</c> MSG-ENG-005): a lock's key is the first eight bytes, read as a big-endian signed
/// 64-bit integer, of the SHA-256 of its key's bytes, a layout two releases share.
/// </summary>
internal static class AdvisoryLocks
{
    private const string Exclusive = "SELECT pg_advisory_xact_lock(@key)";

    private const string Shared = "SELECT pg_advisory_xact_lock_shared(@key)";

    /// <summary>The lock key of <paramref name="bytes"/>.</summary>
    internal static long Of(ReadOnlySpan<byte> bytes)
    {
        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(bytes, digest);
        return BinaryPrimitives.ReadInt64BigEndian(digest);
    }

    /// <summary>The idempotency key's lock: <c>idempotency</c>, a zero byte, the kind, a zero byte and the key.</summary>
    internal static long Idempotency(string kind, string key)
    {
        byte[] bytes = Encoding.UTF8.GetBytes($"{KeyedHashes.Idempotency}\0{kind}\0{key}");
        return Of(bytes);
    }

    /// <summary>A delivery report's reference's lock: <c>sms-report</c>, a zero byte and the reference, unkeyed, since a reference is no personal data (<c>02</c> MSG-SMS-004).</summary>
    internal static long Report(string reference)
    {
        return Of(Encoding.UTF8.GetBytes($"sms-report\0{reference}"));
    }

    /// <summary>The lock under which held reports are counted and inserted (<c>02</c> MSG-SMS-004).</summary>
    internal static long ReportHold { get; } = Of("sms-report-hold"u8);

    /// <summary>The person's locks: the keyed hash of the reference, domain <c>person</c>, under every held version, in ascending order of version.</summary>
    internal static IReadOnlyList<long> Person(KeyedHashes hashes, string person)
    {
        List<long> locks = [];
        foreach ((int _, byte[] hash) in hashes.Every(KeyedHashes.Person, Encoding.UTF8.GetBytes(person)))
        {
            locks.Add(Of(hash));
        }

        return locks;
    }

    /// <summary>Takes each of <paramref name="keys"/>, in order, in <paramref name="context"/>'s transaction, shared where <paramref name="shared"/>.</summary>
    internal static async Task TakeAsync(MessagingContext context, IEnumerable<long> keys, bool shared, CancellationToken cancellationToken)
    {
        foreach (long key in keys)
        {
            await context.Database.ExecuteSqlRawAsync(shared ? Shared : Exclusive, [new NpgsqlParameter("key", key)], cancellationToken).ConfigureAwait(false);
        }
    }
}