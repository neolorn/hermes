using System;
using System.Collections.Concurrent;

using Lib.Net.Http.WebPush.Authentication;

namespace Hermes.Channels;

/// <summary>
/// The VAPID tokens of one key version, one for each push service origin, each kept and
/// reused for one hour after it was made, then replaced, since Apple asks for no more than
/// one new token an hour (<c>02</c> MSG-PUSH-001). The client makes each token to expire 12
/// hours after it is made.
/// </summary>
internal sealed class VapidTokenCache(TimeProvider time) : IVapidTokenCache
{
    /// <summary>How long a token is reused.</summary>
    internal static readonly TimeSpan Reuse = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<string, (string Token, DateTimeOffset Made)> tokens = new(StringComparer.Ordinal);

    public void Put(string audience, DateTimeOffset expiration, string token)
    {
        tokens[audience] = (token, time.GetUtcNow());
    }

    public string? Get(string audience)
    {
        return tokens.TryGetValue(audience, out (string Token, DateTimeOffset Made) held) && time.GetUtcNow() - held.Made < Reuse ? held.Token : null;
    }
}