using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Operations;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace Hermes.Sending;

/// <summary>
/// Counts a push service's 400, 401 and 403 answers in a row per minute (<c>02</c>
/// MSG-PUSH-005), summed over <c>Hermes:Push:AuthFailWindow</c> for <c>push-auth-failing</c>.
/// </summary>
internal static class PushAuthFailures
{
    private const string Count = """
        INSERT INTO messaging.push_auth_failures (service, minute, count) VALUES (@service, @minute, 1)
        ON CONFLICT (service, minute) DO UPDATE SET count = push_auth_failures.count + 1
        """;

    private const string Sum = "SELECT coalesce(sum(count), 0)::integer AS \"Value\" FROM messaging.push_auth_failures WHERE service = @service AND minute > @now - @window";

    /// <summary>Counts one answer of <paramref name="service"/> at <paramref name="now"/> and answers the sum over <paramref name="window"/>.</summary>
    internal static Task<int> CountAsync(MessagingDatabase database, MessagingOptions options, string service, DateTimeOffset now, TimeSpan window, CancellationToken cancellationToken)
    {
        DateTimeOffset minute = new(now.UtcTicks - (now.UtcTicks % TimeSpan.TicksPerMinute), TimeSpan.Zero);
        return LibraryTransactions.RunAsync(
            database,
            options,
            async (context, token) =>
            {
                await context.Database.ExecuteSqlRawAsync(Count, [new NpgsqlParameter("service", service), new NpgsqlParameter("minute", minute)], token).ConfigureAwait(false);
                return await SumAsync(context, service, now, window, token).ConfigureAwait(false);
            },
            cancellationToken);
    }

    /// <summary>The services with a row within <paramref name="window"/> before <paramref name="now"/>.</summary>
    internal static Task<List<string>> ServicesAsync(MessagingContext context, DateTimeOffset now, TimeSpan window, CancellationToken cancellationToken)
    {
        DateTimeOffset after = now - window;
        return context.PushAuthFailures.Where(row => row.Minute > after).Select(row => row.Service).Distinct().ToListAsync(cancellationToken);
    }

    /// <summary>The details of <c>push-auth-failing</c> (<c>08</c> section 5).</summary>
    internal static Dictionary<string, string> Details(string service, int count)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal) { ["service"] = service, ["count"] = count.ToString(CultureInfo.InvariantCulture) };
    }

    /// <summary>The sum of <paramref name="service"/>'s rows over <paramref name="window"/> before <paramref name="now"/>.</summary>
    internal static Task<int> SumAsync(MessagingContext context, string service, DateTimeOffset now, TimeSpan window, CancellationToken cancellationToken)
    {
        return context.Database
            .SqlQueryRaw<int>(Sum, new NpgsqlParameter("service", service), new NpgsqlParameter("now", now), new NpgsqlParameter("window", window))
            .SingleAsync(cancellationToken);
    }
}