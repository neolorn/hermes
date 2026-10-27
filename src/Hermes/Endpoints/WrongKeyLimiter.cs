using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Hermes.Channels;

using Microsoft.Extensions.Options;

namespace Hermes.Endpoints;

/// <summary>
/// Limits, per source address and per process, the report requests whose key does not match
/// to <c>Hermes:Sms:WrongKeyRate</c> in any minute (<c>02</c> MSG-SMS-004); a request whose key
/// matches is never limited. Each source keeps no more instants than the rate, and a source
/// whose last request is a minute old is forgotten.
/// </summary>
internal sealed class WrongKeyLimiter(IOptionsMonitor<ChannelsOptions> channels, TimeProvider clock)
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly Lock gate = new();

    private readonly Dictionary<string, Queue<DateTimeOffset>> sources = new(StringComparer.Ordinal);

    private DateTimeOffset swept = DateTimeOffset.MinValue;

    /// <summary>Counts a wrong-key request from <paramref name="source"/>; answers none where it is within the rate, or the time until the source's minute window frees.</summary>
    internal TimeSpan? Take(string source)
    {
        int rate = channels.CurrentValue.Sms.WrongKeyRate;
        DateTimeOffset now = clock.GetUtcNow();
        lock (gate)
        {
            Sweep(now);
            if (!sources.TryGetValue(source, out Queue<DateTimeOffset>? taken))
            {
                taken = new Queue<DateTimeOffset>();
                sources[source] = taken;
            }

            while (taken.Count > 0 && now - taken.Peek() >= Window)
            {
                taken.Dequeue();
            }

            while (taken.Count > rate)
            {
                taken.Dequeue();
            }

            if (taken.Count >= rate)
            {
                return taken.Peek() + Window - now;
            }

            taken.Enqueue(now);
            return null;
        }
    }

    /// <summary>Forgets, at most once a minute, every source whose last request is a minute old.</summary>
    private void Sweep(DateTimeOffset now)
    {
        if (now - swept < Window)
        {
            return;
        }

        swept = now;
        foreach (string stale in sources.Where(source => source.Value.Count == 0 || now - source.Value.Last() >= Window).Select(source => source.Key).ToList())
        {
            sources.Remove(stale);
        }
    }
}