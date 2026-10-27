using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Operations;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Hermes.Sending;

/// <summary>
/// The person's preferences (<c>01</c> MSG-SEND-010): on by default, set per optional category
/// and channel under the person's locks taken shared, so an erasure, which takes them
/// exclusively, never misses one, each wait bounded by <c>Hermes:Dispatch:Timeout</c>.
/// </summary>
internal sealed class Preferences(
    MessagingReady ready,
    Declarations declarations,
    IOptionsMonitor<MessagingOptions> options,
    KeyedHashes hashes,
    MessagingDatabase database,
    TimeProvider clock) : IPreferences
{
    private const int MostReferenceCharacters = 128;

    private const string Set = """
        INSERT INTO messaging.preferences (person, category, channel, "on", set_at) VALUES (@person, @category, @channel, @on, @now)
        ON CONFLICT (person, category, channel) DO UPDATE SET "on" = excluded."on", set_at = excluded.set_at
        """;

    public async ValueTask<IReadOnlyList<CategoryPreferences>> ReadAsync(string person, CancellationToken cancellationToken)
    {
        ready.ThrowIfNotReady();
        CheckPerson(person);
        List<PreferenceRow> rows = await LibraryTransactions.RunAsync(
            database,
            options.CurrentValue,
            (context, token) => context.Preferences.AsNoTracking().Where(row => row.Person == person).ToListAsync(token),
            cancellationToken).ConfigureAwait(false);
        return Of(declarations, rows);
    }

    /// <summary>Every declared category's preferences with the channels its kinds use, a channel on unless <paramref name="rows"/> turn it off.</summary>
    internal static IReadOnlyList<CategoryPreferences> Of(Declarations declarations, IReadOnlyList<PreferenceRow> rows)
    {
        return [.. declarations.Categories.Select(category => new CategoryPreferences
        {
            Category = category.Name,
            Essential = category.Essential,
            Channels = [.. declarations.ChannelsOf(category.Name).Select(channel => new ChannelPreference
            {
                Channel = channel,
                On = category.Essential || !rows.Any(row => row.Category == category.Name && row.Channel == Spellings.Of(channel) && !row.On),
            })],
        })];
    }

    public async ValueTask<Result> SetAsync(string person, string category, Channel channel, bool isOn, CancellationToken cancellationToken)
    {
        ready.ThrowIfNotReady();
        CheckPerson(person);
        CategoryDeclaration? declared = declarations.Categories.FirstOrDefault(each => string.Equals(each.Name, category, StringComparison.Ordinal));
        if (declared is null || !declarations.ChannelsOf(category).Contains(channel))
        {
            return Result.Fail(new Failure
            {
                Code = Codes.PreferenceUnknown,
                Details = new Dictionary<string, string>(StringComparer.Ordinal) { ["category"] = category, ["channel"] = Spellings.Of(channel) },
            });
        }

        if (declared.Essential)
        {
            return Result.Fail(new Failure { Code = Codes.PreferenceEssential, Details = new Dictionary<string, string>(StringComparer.Ordinal) { ["category"] = category } });
        }

        MessagingOptions current = options.CurrentValue;
        await LibraryTransactions.RunAsync(
            database,
            current,
            async (context, token) =>
            {
                await DeliveryClaims.LockTimeoutAsync(context, current.Dispatch.Timeout, token).ConfigureAwait(false);
                await AdvisoryLocks.TakeAsync(context, AdvisoryLocks.Person(hashes, person), shared: true, token).ConfigureAwait(false);
                return await context.Database.ExecuteSqlRawAsync(
                    Set,
                    [
                        new NpgsqlParameter("person", person),
                        new NpgsqlParameter("category", category),
                        new NpgsqlParameter("channel", Spellings.Of(channel)),
                        new NpgsqlParameter("on", isOn),
                        new NpgsqlParameter("now", clock.GetUtcNow()),
                    ],
                    token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <summary>Whether <paramref name="person"/> turned <paramref name="category"/> off on <paramref name="channel"/>, read now.</summary>
    internal static Task<bool> IsOffAsync(MessagingContext context, string person, string category, Channel channel, CancellationToken cancellationToken)
    {
        string spelling = Spellings.Of(channel);
        return context.Preferences.AsNoTracking().AnyAsync(row => row.Person == person && row.Category == category && row.Channel == spelling && !row.On, cancellationToken);
    }

    private static void CheckPerson(string person)
    {
        if (person is not { Length: >= 1 and <= MostReferenceCharacters })
        {
            throw new ArgumentException("A person reference is 1 to 128 characters.", nameof(person));
        }
    }
}