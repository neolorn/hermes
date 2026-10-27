using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Operations;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Hermes.Sending;

/// <summary>
/// The suppressions a host adds and withdraws (<c>01</c> MSG-SEND-012), each kept as the
/// destination's keyed hash under the write version, found under every version held, and
/// refused where a version in use is not held, since a lookup under it cannot be made.
/// </summary>
internal sealed class Suppressions(
    MessagingReady ready,
    Declarations declarations,
    IOptionsMonitor<MessagingOptions> options,
    KeyedHashes hashes,
    MessagingDatabase database,
    Alerts alerts,
    TimeProvider clock) : ISuppressions
{
    private const string SuppressHash = """
        INSERT INTO messaging.suppressions (channel, category, hash, hash_version, made_at) VALUES ('email', @category, @hash, @version, @now)
        ON CONFLICT (channel, category, hash) DO NOTHING
        """;

    public ValueTask<Result> AddAsync(Channel channel, string destination, string category, CancellationToken cancellationToken)
    {
        return ChangeAsync(channel, destination, category, add: true, cancellationToken);
    }

    public ValueTask<Result> RemoveAsync(Channel channel, string destination, string category, CancellationToken cancellationToken)
    {
        return ChangeAsync(channel, destination, category, add: false, cancellationToken);
    }

    /// <summary>The bytes a destination's suppression hash is made of, or none where it does not read: an address in its hashed form, or a number.</summary>
    internal static byte[]? Value(Channel channel, string? destination)
    {
        if (destination is null)
        {
            return null;
        }

        return channel switch
        {
            Channel.Email => Destinations.Email(destination) is { } address ? Encoding.UTF8.GetBytes(Destinations.EmailHashed(address)) : null,
            Channel.Sms => Destinations.IsPhone(destination) ? Encoding.UTF8.GetBytes(destination) : null,
            _ => null,
        };
    }

    /// <summary>
    /// Suppresses, for <paramref name="category"/>, the email address whose keyed hash under
    /// <paramref name="version"/> is <paramref name="hash"/>, as an unsubscribe token names it
    /// (<c>05</c> MSG-API-005); a hash already suppressed is left as it is.
    /// </summary>
    internal static Task<int> SuppressHashAsync(MessagingContext context, string category, byte[] hash, int version, DateTimeOffset now, CancellationToken cancellationToken)
    {
        return context.Database.ExecuteSqlRawAsync(
            SuppressHash,
            [
                new NpgsqlParameter("category", category),
                new NpgsqlParameter("hash", hash),
                new NpgsqlParameter("version", version),
                new NpgsqlParameter("now", now),
            ],
            cancellationToken);
    }

    /// <summary>The domain of <paramref name="channel"/>'s suppression hashes.</summary>
    internal static string Domain(Channel channel)
    {
        return channel == Channel.Email ? KeyedHashes.Email : KeyedHashes.Sms;
    }

    /// <summary>
    /// Whether the destination is suppressed for the category, looked up under every held
    /// version and then the versions in use read in the same transaction; none where a version
    /// in use is not held, answered as that version.
    /// </summary>
    internal static async Task<(bool Suppressed, int? Missing)> LookUpAsync(MessagingContext context, KeyedHashes hashes, Channel channel, string category, byte[] value, CancellationToken cancellationToken)
    {
        string spelling = Spellings.Of(channel);
        List<byte[]> every = [.. hashes.Every(Domain(channel), value).Select(hash => hash.Hash)];
        bool suppressed = await context.Suppressions
            .AsNoTracking()
            .AnyAsync(row => row.Channel == spelling && row.Category == category && every.Contains(row.Hash), cancellationToken)
            .ConfigureAwait(false);
        int? missing = await KeysInUse.MissingAsync(context, KeyNeeds.HashKeys, [.. hashes.Versions], cancellationToken).ConfigureAwait(false);
        return (suppressed, missing);
    }

    private async ValueTask<Result> ChangeAsync(Channel channel, string destination, string category, bool add, CancellationToken cancellationToken)
    {
        ready.ThrowIfNotReady();
        if (channel is not (Channel.Email or Channel.Sms))
        {
            return Invalid("channel");
        }

        CategoryDeclaration? declared = declarations.Categories.FirstOrDefault(each => string.Equals(each.Name, category, StringComparison.Ordinal));
        if (declared is null)
        {
            return Invalid("category");
        }

        if (add && declared.Essential)
        {
            return Result.Fail(new Failure { Code = Codes.SuppressionEssential, Details = new Dictionary<string, string>(StringComparer.Ordinal) { ["category"] = category } });
        }

        if (Value(channel, destination) is not { } value)
        {
            return Invalid("destination");
        }

        MessagingOptions current = options.CurrentValue;
        string spelling = Spellings.Of(channel);
        int? missing = await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                (bool suppressed, int? notHeld) = await LookUpAsync(context, hashes, channel, category, value, token).ConfigureAwait(false);
                if (notHeld is not null)
                {
                    return notHeld;
                }

                if (add && !suppressed)
                {
                    int version = hashes.WriteVersion;
                    context.Suppressions.Add(new SuppressionRow
                    {
                        Channel = spelling,
                        Category = category,
                        Hash = hashes.Under(version, Domain(channel), value),
                        HashVersion = version,
                        MadeAt = clock.GetUtcNow(),
                    });
                    await context.SaveChangesAsync(token).ConfigureAwait(false);
                }
                else if (!add && suppressed)
                {
                    List<byte[]> every = [.. hashes.Every(Domain(channel), value).Select(hash => hash.Hash)];
                    await context.Suppressions
                        .Where(row => row.Channel == spelling && row.Category == category && every.Contains(row.Hash))
                        .ExecuteDeleteAsync(token)
                        .ConfigureAwait(false);
                }

                return null;
            },
            cancellationToken).ConfigureAwait(false);
        if (missing is { } version)
        {
            await alerts.HoldsUntilAsync("key-unavailable", KeyNeeds.HashKeys, new Dictionary<string, string>(StringComparer.Ordinal) { ["version"] = version.ToString(System.Globalization.CultureInfo.InvariantCulture) }, cancellationToken).ConfigureAwait(false);
            return Result.Fail(new Failure { Code = Codes.KeyUnavailable, Details = new Dictionary<string, string>(StringComparer.Ordinal) { ["key"] = KeyNeeds.HashKeys } });
        }

        return Result.Success();
    }

    private static Result Invalid(string member)
    {
        return Result.Fail(new Failure { Code = Codes.SuppressionInvalid, Details = new Dictionary<string, string>(StringComparer.Ordinal) { ["member"] = member } });
    }
}