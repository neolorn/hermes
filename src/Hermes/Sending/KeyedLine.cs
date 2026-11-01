using System;

namespace Hermes.Sending;

/// <summary>One value of a keyed table's row, as <see cref="Operations.KeyedTable.SelectToProtectAgain"/> reads it.</summary>
internal sealed class KeyedLine
{
    /// <summary>The row's id.</summary>
    public Guid Id { get; init; }

    /// <summary>The person the row's values are for, or none.</summary>
    public string? Person { get; init; }

    /// <summary>The value's position, from 1, in the table's values.</summary>
    public long Position { get; init; }

    /// <summary>The value, none where the row holds none.</summary>
    public byte[]? Value { get; init; }
}