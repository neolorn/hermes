using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Operations;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace Hermes.Sending;

/// <summary>
/// The places of a category's limits (<c>01</c> MSG-SEND-014): each lock's key, the count of
/// a window's places, and their release when a delivery ends in a final status other than
/// <c>sent</c> and <c>delivered</c>, a delivery on which a provider call is recorded releasing
/// only its <c>Limit</c> places, since its message may have gone out.
/// </summary>
internal static class Places
{
    private const string Release = """
        DELETE FROM messaging.places p
        WHERE p.delivery_id = ANY(@ids)
          AND (p.kind = 'limit' OR NOT EXISTS (SELECT 1 FROM messaging.calls c WHERE c.delivery_id = p.delivery_id))
        """;

    private const string ReleaseLimits = "DELETE FROM messaging.places WHERE delivery_id = ANY(@ids) AND kind = 'limit'";

    /// <summary>
    /// The lock key of a place: the SHA-256 of its channel, its category, its window in whole
    /// seconds as decimal ASCII digits and, for a <c>Limit</c> place, its hash, joined by zero
    /// bytes, read as <see cref="AdvisoryLocks.Of"/> reads it; a layout two releases share.
    /// </summary>
    internal static long Key(string channel, string category, int windowSeconds, byte[]? hash)
    {
        byte[] channelBytes = Encoding.UTF8.GetBytes(channel);
        byte[] categoryBytes = Encoding.UTF8.GetBytes(category);
        byte[] windowBytes = Encoding.ASCII.GetBytes(windowSeconds.ToString(CultureInfo.InvariantCulture));
        int length = channelBytes.Length + 1 + categoryBytes.Length + 1 + windowBytes.Length + (hash is null ? 0 : 1 + hash.Length);
        byte[] key = new byte[length];
        Span<byte> span = key;
        channelBytes.CopyTo(span);
        int at = channelBytes.Length + 1;
        categoryBytes.CopyTo(span[at..]);
        at += categoryBytes.Length + 1;
        windowBytes.CopyTo(span[at..]);
        if (hash is not null)
        {
            hash.CopyTo(span[(at + windowBytes.Length + 1)..]);
        }

        return AdvisoryLocks.Of(key);
    }

    /// <summary>The whole seconds of a limit's window.</summary>
    internal static int Seconds(TimeSpan window)
    {
        return (int)window.TotalSeconds;
    }

    /// <summary>The places counted in a <c>Limit</c>'s window at <paramref name="now"/>: those of any of <paramref name="hashes"/> taken after the window's start.</summary>
    internal static Task<int> CountAsync(MessagingContext context, string channel, string category, TimeSpan window, IReadOnlyList<byte[]>? hashes, DateTimeOffset now, CancellationToken cancellationToken)
    {
        int seconds = Seconds(window);
        DateTimeOffset start = now - window;
        IQueryable<PlaceRow> places = context.Places.Where(place => place.Channel == channel && place.Category == category && place.WindowSeconds == seconds && place.TakenAt > start);
        if (hashes is null)
        {
            return places.CountAsync(place => place.Kind == PlaceRow.Deployment, cancellationToken);
        }

        List<byte[]> every = [.. hashes];
        return places.CountAsync(place => place.Kind == PlaceRow.Limit && every.Contains(place.Hash!), cancellationToken);
    }

    /// <summary>Releases the places of <paramref name="deliveries"/>, each having ended in a final status other than <c>sent</c> and <c>delivered</c>.</summary>
    internal static Task ReleaseAsync(MessagingContext context, IReadOnlyList<Guid> deliveries, CancellationToken cancellationToken)
    {
        return context.Database.ExecuteSqlRawAsync(Release, [new NpgsqlParameter("ids", deliveries.ToArray())], cancellationToken);
    }

    /// <summary>Releases only the <c>Limit</c> places of <paramref name="deliveries"/>, as a report of failure does (<c>02</c> MSG-SMS-005).</summary>
    internal static Task ReleaseLimitsAsync(MessagingContext context, IReadOnlyList<Guid> deliveries, CancellationToken cancellationToken)
    {
        return context.Database.ExecuteSqlRawAsync(ReleaseLimits, [new NpgsqlParameter("ids", deliveries.ToArray())], cancellationToken);
    }
}