using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Hermes.Conversations.Live;

/// <summary>
/// The public hub's connections this process holds (<c>09</c> MSG-CONV-008), each with the
/// keys its caller is known by: a customer's <c>person:</c> and reference, or a visitor's
/// <c>visitor:</c> and the base64 of their reference's hash under each version held. Nothing
/// of them is stored: a visitor is never online to staff.
/// </summary>
internal sealed class PublicConnections
{
    private readonly ConcurrentDictionary<string, string[]> held = new(StringComparer.Ordinal);

    /// <summary>The key a customer is known by.</summary>
    internal static string CustomerKey(string person)
    {
        return "person:" + person;
    }

    /// <summary>The key a visitor is known by, from one hash of their reference.</summary>
    internal static string VisitorKey(byte[] hash)
    {
        return "visitor:" + Convert.ToBase64String(hash);
    }

    internal void Add(string connection, string[] keys)
    {
        held[connection] = keys;
    }

    internal void Remove(string connection)
    {
        held.TryRemove(connection, out _);
    }

    /// <summary>Whether any connection is held here.</summary>
    internal bool Any => !held.IsEmpty;

    /// <summary>The keys <paramref name="connection"/> is known by, or none.</summary>
    internal IReadOnlyList<string>? KeysOf(string connection)
    {
        return held.TryGetValue(connection, out string[]? keys) ? keys : null;
    }

    /// <summary>The connections here known by <paramref name="key"/>.</summary>
    internal IReadOnlyList<string> Of(string key)
    {
        return [.. held.Where(each => each.Value.Contains(key, StringComparer.Ordinal)).Select(each => each.Key)];
    }
}