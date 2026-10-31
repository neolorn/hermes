using System.Collections.Generic;

namespace Hermes.Core;

/// <summary>A page of a person's in-application list, newest first (<c>02</c> MSG-INAPP-003).</summary>
public sealed record InAppPage
{
    /// <summary>The page's entries, newest first by the instant written and then by identifier.</summary>
    public required IReadOnlyList<InAppEntry> Entries { get; init; }

    /// <summary>The cursor of the next page, or none on the last.</summary>
    public string? Next { get; init; }
}