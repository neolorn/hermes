using System.Collections.Generic;

namespace Hermes.Core;

/// <summary>A refusal a library call answers: its code of <c>08</c> section 1 and its details (<c>07</c> MSG-ENG-003).</summary>
public sealed record Failure
{
    /// <summary>The code, of <c>08</c> section 1.</summary>
    public required string Code { get; init; }

    /// <summary>The details <c>08</c> section 1 gives the code, by name.</summary>
    public required IReadOnlyDictionary<string, string> Details { get; init; }
}