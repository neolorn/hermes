using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Hermes.Conversations.Live;

/// <summary>The staff hub's connections this process holds, each with its person and its row of <c>connections</c> (<c>09</c> MSG-CONV-006).</summary>
internal sealed class LiveConnections
{
    private readonly ConcurrentDictionary<string, (string Person, Guid Row)> held = new(StringComparer.Ordinal);

    /// <summary>This process, as the rows of <c>connections</c> name it.</summary>
    internal Guid Process { get; } = Guid.CreateVersion7();

    internal void Add(string connection, string person, Guid row)
    {
        held[connection] = (person, row);
    }

    /// <summary>Removes <paramref name="connection"/>, answering its person and row, or none where it was not held.</summary>
    internal (string Person, Guid Row)? Remove(string connection)
    {
        return held.TryRemove(connection, out (string Person, Guid Row) removed) ? removed : null;
    }

    /// <summary>The person <paramref name="connection"/> is for, or none.</summary>
    internal string? PersonOf(string connection)
    {
        return held.TryGetValue(connection, out (string Person, Guid Row) found) ? found.Person : null;
    }

    /// <summary>The persons holding a connection here.</summary>
    internal IReadOnlySet<string> Persons()
    {
        return held.Values.Select(each => each.Person).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>The connections here of <paramref name="persons"/>, less those of <paramref name="except"/>.</summary>
    internal IReadOnlyList<string> Of(IReadOnlySet<string> persons, string? except = null)
    {
        return [.. held.Where(each => persons.Contains(each.Value.Person) && !string.Equals(each.Value.Person, except, StringComparison.Ordinal)).Select(each => each.Key)];
    }
}