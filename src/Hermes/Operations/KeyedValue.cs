namespace Hermes.Operations;

/// <summary>A protected value of a <see cref="KeyedTable"/>: its <c>bytea</c> column and its field in the context.</summary>
/// <param name="Column">The column.</param>
/// <param name="Field">The field in the value's context.</param>
internal readonly record struct KeyedValue(string Column, string Field);