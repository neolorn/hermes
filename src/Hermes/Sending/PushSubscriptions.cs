using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Channels;
using Hermes.Core;
using Hermes.Operations;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Hermes.Sending;

/// <summary>
/// Registers and removes a person's push subscriptions (<c>02</c> MSG-PUSH-003, MSG-PUSH-004,
/// MSG-PUSH-007): a registration runs in one transaction under the endpoint's locks and then
/// the person's, protects the endpoint and keys for the person, and removes the least
/// recently used of the person's subscriptions past <c>Hermes:Push:MaxSubscriptions</c>.
/// </summary>
internal sealed partial class PushSubscriptions(
    MessagingDatabase database,
    KeyedHashes hashes,
    HeldKeys keys,
    KeyTable table,
    ContentProtection protection,
    Alerts alerts,
    IOptionsMonitor<MessagingOptions> options,
    IOptionsMonitor<ChannelsOptions> channels,
    TimeProvider clock,
    ILogger<PushSubscriptions> logger)
{
    private const string LockByHashes = "SELECT * FROM messaging.subscriptions WHERE endpoint_hash = ANY(@hashes) FOR UPDATE";

    /// <summary>What a registration or removal answers.</summary>
    internal enum Outcome
    {
        Done,
        EndpointNotAllowed,
        KeyUnknown,
        Unavailable,
    }

    /// <summary>The public key of the <c>pushVapidKeys</c> write version, the one new subscriptions use (<c>02</c> MSG-PUSH-007).</summary>
    internal byte[] PublicKey()
    {
        HeldKey vapid = keys.Find(KeyNeeds.PushVapidKeys)!;
        return KeyForms.CheckValue(KeyNeeds.PushVapidKeys, vapid.Write);
    }

    /// <summary>Registers the subscription of <paramref name="endpoint"/>, its 81 bytes of keys <paramref name="subscriptionKeys"/> and the browser's expiry, subscribed with <paramref name="serverKey"/>, for <paramref name="person"/>.</summary>
    internal async Task<Outcome> RegisterAsync(string person, string endpoint, byte[] subscriptionKeys, byte[] serverKey, DateTimeOffset? expiresAt, CancellationToken cancellationToken)
    {
        PushChannelOptions push = channels.CurrentValue.Push;
        if (SubscriptionRule.Host(endpoint) is not { } host || !HostRule.Allows(push.Hosts, host))
        {
            return Outcome.EndpointNotAllowed;
        }

        (Outcome found, int version) = await VersionAsync(serverKey, cancellationToken).ConfigureAwait(false);
        if (found != Outcome.Done)
        {
            return found;
        }

        if (version != keys.Find(KeyNeeds.PushVapidKeys)!.WriteVersion)
        {
            await table.MarkMadeAsync(KeyNeeds.PushVapidKeys, version, cancellationToken).ConfigureAwait(false);
        }

        MessagingOptions current = options.CurrentValue;
        byte[] endpointBytes = Encoding.UTF8.GetBytes(endpoint);
        IReadOnlyList<(int Version, byte[] Hash)> every = hashes.Every(KeyedHashes.PushEndpoint, endpointBytes);
        return await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await DeliveryClaims.LockTimeoutAsync(context, current.Dispatch.Timeout, token).ConfigureAwait(false);
                await AdvisoryLocks.TakeAsync(context, [.. every.Select(hash => AdvisoryLocks.Of(hash.Hash)), .. AdvisoryLocks.Person(hashes, person)], shared: false, token).ConfigureAwait(false);
                List<SubscriptionRow> rows = await context.Subscriptions.FromSqlRaw(LockByHashes, new NpgsqlParameter("hashes", every.Select(hash => hash.Hash).ToArray())).ToListAsync(token).ConfigureAwait(false);
                if (await KeysInUse.MissingAsync(context, KeyNeeds.HashKeys, hashes.Versions, token).ConfigureAwait(false) is { } missing)
                {
                    await KeyUnavailableAsync(KeyNeeds.HashKeys, missing).ConfigureAwait(false);
                    return Outcome.Unavailable;
                }

                SubscriptionRow? row = rows.FirstOrDefault();
                Guid id = row?.Id ?? Guid.CreateVersion7();
                byte[]? storedEndpoint = await ProtectAsync(person, endpointBytes, id, "endpoint", current.Dispatch.Timeout, token).ConfigureAwait(false);
                byte[]? storedKeys = storedEndpoint is null ? null : await ProtectAsync(person, subscriptionKeys, id, "keys", current.Dispatch.Timeout, token).ConfigureAwait(false);
                if (storedEndpoint is null || storedKeys is null)
                {
                    return Outcome.Unavailable;
                }

                DateTimeOffset now = clock.GetUtcNow();
                if (row is null)
                {
                    row = new SubscriptionRow
                    {
                        Id = id,
                        Person = person,
                        EndpointHash = hashes.Write(KeyedHashes.PushEndpoint, endpointBytes),
                        HashVersion = hashes.WriteVersion,
                        Endpoint = storedEndpoint,
                        Keys = storedKeys,
                        VapidVersion = version,
                        Service = Spellings.Of(SubscriptionRule.Service(host)),
                        MadeAt = now,
                    };
                    context.Subscriptions.Add(row);
                }

                row.Person = person;
                row.EndpointHash = hashes.Write(KeyedHashes.PushEndpoint, endpointBytes);
                row.HashVersion = hashes.WriteVersion;
                row.Endpoint = storedEndpoint;
                row.Keys = storedKeys;
                row.ContentKeyVersion = Messages.LibraryVersion(storedEndpoint);
                row.VapidVersion = version;
                row.Service = Spellings.Of(SubscriptionRule.Service(host));
                row.MadeAt = now;
                row.ExpiresAt = expiresAt;
                await context.SaveChangesAsync(token).ConfigureAwait(false);

                List<Guid> stale = await context.Subscriptions
                    .Where(other => other.Person == person && other.Id != id)
                    .OrderBy(other => other.LastAcceptedAt != null && other.LastAcceptedAt > other.MadeAt ? other.LastAcceptedAt : other.MadeAt)
                    .ThenBy(other => other.Id)
                    .Select(other => other.Id)
                    .ToListAsync(token).ConfigureAwait(false);
                int over = stale.Count + 1 - push.MaxSubscriptions;
                if (over > 0)
                {
                    List<Guid> removed = [.. stale.Take(over)];
                    await context.Subscriptions.Where(other => removed.Contains(other.Id)).ExecuteDeleteAsync(token).ConfigureAwait(false);
                }

                return Outcome.Done;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Removes the subscription of <paramref name="endpoint"/> where <paramref name="person"/> holds it, taking no person's lock; one another person holds is left as it is.</summary>
    internal Task<Outcome> RemoveAsync(string person, string endpoint, CancellationToken cancellationToken)
    {
        MessagingOptions current = options.CurrentValue;
        byte[][] every = [.. hashes.Every(KeyedHashes.PushEndpoint, Encoding.UTF8.GetBytes(endpoint)).Select(hash => hash.Hash)];
        return LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await DeliveryClaims.LockTimeoutAsync(context, current.Dispatch.Timeout, token).ConfigureAwait(false);
                if (await KeysInUse.MissingAsync(context, KeyNeeds.HashKeys, hashes.Versions, token).ConfigureAwait(false) is not null)
                {
                    return Outcome.Unavailable;
                }

                await context.Subscriptions.Where(row => every.Contains(row.EndpointHash) && row.Person == person).ExecuteDeleteAsync(token).ConfigureAwait(false);
                return Outcome.Done;
            },
            cancellationToken);
    }

    /// <summary>
    /// The held <c>pushVapidKeys</c> version whose public key <paramref name="serverKey"/> is and
    /// that is not retired; unavailable where it is the key of a version in use that this process
    /// does not hold, and unknown otherwise.
    /// </summary>
    private async Task<(Outcome Outcome, int Version)> VersionAsync(byte[] serverKey, CancellationToken cancellationToken)
    {
        HeldKey vapid = keys.Find(KeyNeeds.PushVapidKeys)!;
        int? held = vapid.Versions
            .Where(version => CryptographicOperations.FixedTimeEquals(KeyForms.CheckValue(KeyNeeds.PushVapidKeys, version.Value), serverKey))
            .Select(version => (int?)version.Key)
            .FirstOrDefault();
        List<KeyVersion> recorded = await LibraryTransactions.RunAsync(
            database,
            options.CurrentValue,
            (context, token) => context.KeyVersions.AsNoTracking().Where(row => row.Key == KeyNeeds.PushVapidKeys).ToListAsync(token),
            cancellationToken).ConfigureAwait(false);
        if (held is { } version)
        {
            return recorded.Any(row => row.Version == version && row.Retired) ? (Outcome.KeyUnknown, 0) : (Outcome.Done, version);
        }

        return recorded.Any(row => row.CheckValue.AsSpan().SequenceEqual(serverKey) && row.FirstMadeAt is not null && !row.Retired)
            ? (Outcome.Unavailable, 0)
            : (Outcome.KeyUnknown, 0);
    }

    /// <summary>Protects <paramref name="plaintext"/> for the person under the subscription's <paramref name="field"/>, bounded by <paramref name="timeout"/>; none where the key is not held or the protector throws or does not answer, which raises <c>key-unavailable</c> for its <c>Id</c>.</summary>
    private async Task<byte[]?> ProtectAsync(string person, byte[] plaintext, Guid id, string field, TimeSpan timeout, CancellationToken cancellationToken)
    {
        string context = ContentContext.Of(ContentContext.Subscriptions, id, field);
        try
        {
            using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            bound.CancelAfter(timeout);
            ContentProtecting made = await protection.ProtectAsync(person, plaintext, context, bound.Token).AsTask().WaitAsync(timeout, clock, cancellationToken).ConfigureAwait(false);
            if (made.Stored is null)
            {
                await KeyUnavailableAsync(made.Key!, made.Version).ConfigureAwait(false);
            }

            return made.Stored;
        }
        catch (Exception failure) when (!cancellationToken.IsCancellationRequested)
        {
            ProtectorFailed(logger, failure.GetType().Name);
            await KeyUnavailableAsync(protection.WriterId(), version: null).ConfigureAwait(false);
            return null;
        }
    }

    private Task KeyUnavailableAsync(string key, int? version)
    {
        Dictionary<string, string> details = new(StringComparer.Ordinal);
        if (version is { } held)
        {
            details["version"] = held.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return alerts.HoldsUntilAsync("key-unavailable", key, details, CancellationToken.None);
    }

    [LoggerMessage(EventId = 54, EventName = "SubscriptionProtectorFailed", Level = LogLevel.Warning, Message = "Protecting a push subscription failed with {Exception}.")]
    private static partial void ProtectorFailed(ILogger logger, string exception);
}