using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Hermes.Operations;

/// <summary>
/// A table of the schema <c>messaging</c> beyond the deliveries, subscriptions and in-app
/// entries that holds a protected value, a keyed hash or a person reference, which joins key
/// retirement, its re-protection and erasure through this description in the phase that adds
/// it (<c>06</c> MSG-OPS-008; <c>01</c> MSG-SEND-020; M-031). Its rows are keyed by a column
/// <c>id</c> of type <c>uuid</c>; a writer of a person's rows takes the person's locks shared
/// in its transaction, as the in-app list does (<see cref="Sending.AdvisoryLocks.Person"/>).
/// </summary>
internal sealed partial class KeyedTable
{
    /// <summary>Describes a table, each name an identifier of lower-case letters, digits and underscores.</summary>
    /// <param name="name">The table's name in the schema <c>messaging</c>.</param>
    /// <param name="context">The table's name in its values' context, <c>&lt;table&gt;:&lt;row id&gt;:&lt;field&gt;</c>.</param>
    /// <param name="person">The column of the person reference its rows are for, or none.</param>
    /// <param name="values">Its protected values, each a <c>bytea</c> column and its field in the context.</param>
    /// <param name="contentKeyVersion">The column recording the <c>contentKeys</c> version of a row's library values, required where it holds values.</param>
    /// <param name="hashVersion">The column recording the <c>hashKeys</c> version of a row's keyed hash, or none.</param>
    /// <param name="erasure">What an erasure does to the person's rows, where it has a person column.</param>
    internal KeyedTable(
        string name,
        string context,
        string? person = null,
        IReadOnlyList<KeyedValue>? values = null,
        string? contentKeyVersion = null,
        string? hashVersion = null,
        KeyedErasure erasure = KeyedErasure.Delete)
    {
        Name = Identifier(name, nameof(name));
        Context = Identifier(context, nameof(context));
        Person = person is null ? null : Identifier(person, nameof(person));
        Values = values ?? [];
        foreach (KeyedValue value in Values)
        {
            _ = Identifier(value.Column, nameof(values));
            _ = Identifier(value.Field, nameof(values));
        }

        ContentKeyVersion = contentKeyVersion is null ? null : Identifier(contentKeyVersion, nameof(contentKeyVersion));
        HashVersion = hashVersion is null ? null : Identifier(hashVersion, nameof(hashVersion));
        if (Values.Count > 0 && ContentKeyVersion is null)
        {
            throw new ArgumentException("A table holding protected values records their contentKeys version.", nameof(contentKeyVersion));
        }

        if (!Enum.IsDefined(erasure))
        {
            throw new ArgumentOutOfRangeException(nameof(erasure));
        }

        Erasure = erasure;
    }

    /// <summary>The table's name in the schema <c>messaging</c>.</summary>
    internal string Name { get; }

    /// <summary>The table's name in its values' context.</summary>
    internal string Context { get; }

    /// <summary>The column of the person reference, or none.</summary>
    internal string? Person { get; }

    /// <summary>The protected values, each a column and its field.</summary>
    internal IReadOnlyList<KeyedValue> Values { get; }

    /// <summary>The column recording the <c>contentKeys</c> version of a row's library values, or none.</summary>
    internal string? ContentKeyVersion { get; }

    /// <summary>The column recording the <c>hashKeys</c> version of a row's keyed hash, or none.</summary>
    internal string? HashVersion { get; }

    /// <summary>What an erasure does to the person's rows.</summary>
    internal KeyedErasure Erasure { get; }

    /// <summary>The condition that no row of the table records <c>@version</c> in <paramref name="column"/>.</summary>
    internal string NoRowUnder(string column)
    {
        return $"NOT EXISTS (SELECT 1 FROM messaging.{Name} WHERE {column} = @version)";
    }

    /// <summary>
    /// Selects a batch of rows whose values were made under a <c>contentKeys</c> version below
    /// <c>@write</c>, after <c>@after</c> in the order of their ids, locking each and skipping
    /// those a transaction holds: one line a value, with the row's id, its person (none where it
    /// has no column) and the value's position from 1 in <see cref="Values"/>.
    /// </summary>
    internal string SelectToProtectAgain()
    {
        string person = Person ?? "NULL::text";
        string columns = string.Concat(Values.Select(value => ", " + value.Column));
        string values = string.Join(", ", Values.Select(value => "batch." + value.Column));
        return $"""
            SELECT batch.id AS "Id", batch.owner AS "Person", taken.position AS "Position", taken.stored AS "Value"
            FROM (SELECT id, {person} AS owner{columns} FROM messaging.{Name}
                  WHERE {ContentKeyVersion} < @write AND id > @after ORDER BY id LIMIT @batch FOR UPDATE SKIP LOCKED) AS batch
            CROSS JOIN LATERAL unnest(ARRAY[{values}]::bytea[]) WITH ORDINALITY AS taken(stored, position)
            ORDER BY batch.id, taken.position
            """;
    }

    /// <summary>
    /// Writes a row's values protected again, <c>@v0</c> onwards, and <c>@version</c>, where each
    /// value is still the one read, <c>@was0</c> onwards.
    /// </summary>
    internal string ProtectAgain()
    {
        string set = string.Join(", ", Values.Select((value, index) => $"{value.Column} = @v{index}"));
        string still = string.Concat(Values.Select((value, index) => $" AND {value.Column} IS NOT DISTINCT FROM @was{index}"));
        return $"UPDATE messaging.{Name} SET {set}, {ContentKeyVersion} = @version WHERE id = @id{still}";
    }

    /// <summary>
    /// The erasure of <c>@person</c>'s rows: deleted, or their person reference, values and
    /// version column emptied and the rest kept; none where the table has no person column.
    /// </summary>
    internal string? Erase()
    {
        if (Person is null)
        {
            return null;
        }

        if (Erasure == KeyedErasure.Delete)
        {
            return $"DELETE FROM messaging.{Name} WHERE {Person} = @person";
        }

        IEnumerable<string> emptied = [Person, .. Values.Select(value => value.Column), .. ContentKeyVersion is null ? [] : new[] { ContentKeyVersion }];
        return $"UPDATE messaging.{Name} SET {string.Join(", ", emptied.Select(column => column + " = NULL"))} WHERE {Person} = @person";
    }

    private static string Identifier(string text, string parameter)
    {
        ArgumentNullException.ThrowIfNull(text, parameter);
        return IdentifierPattern().IsMatch(text) ? text : throw new ArgumentException($"\"{text}\" is not an identifier of lower-case letters, digits and underscores.", parameter);
    }

    [GeneratedRegex(@"^[a-z][a-z0-9_]{0,62}\z", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierPattern();
}