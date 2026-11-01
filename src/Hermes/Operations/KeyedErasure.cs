namespace Hermes.Operations;

/// <summary>What an erasure does to a person's rows of a <see cref="KeyedTable"/>.</summary>
internal enum KeyedErasure
{
    /// <summary>The rows are deleted.</summary>
    Delete,

    /// <summary>The rows' person reference, values and version column are emptied, and the rest kept.</summary>
    Clear,
}