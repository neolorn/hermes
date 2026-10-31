using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Hermes.Core;

/// <summary>
/// An entry of a person's in-application list as a read answers it, in the language it reads
/// in (<c>02</c> MSG-INAPP-003). It prints none of its parts, target or target values.
/// </summary>
public sealed record InAppEntry
{
    /// <summary>The entry's identifier.</summary>
    public required EntryId Entry { get; init; }

    /// <summary>The kind.</summary>
    public required string Kind { get; init; }

    /// <summary>The kind's category.</summary>
    public required string Category { get; init; }

    /// <summary>The instant it was written.</summary>
    public required DateTimeOffset WrittenAt { get; init; }

    /// <summary>The instant it was marked read, or none.</summary>
    public DateTimeOffset? ReadAt { get; init; }

    /// <summary>The declared language it reads in.</summary>
    public required string Language { get; init; }

    /// <summary>The title, plain text.</summary>
    public required string Title { get; init; }

    /// <summary>The body, plain text, or none.</summary>
    public string? Body { get; init; }

    /// <summary>The key of where the entry leads, as the kind declared it when the entry was written, or none.</summary>
    public string? Target { get; init; }

    /// <summary>The send's values that go with the target, by name; none where the kind declares none.</summary>
    public required IReadOnlyDictionary<string, SendValue> TargetValues { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Entry = {Entry.Value}, Kind = {Kind}, Category = {Category}, WrittenAt = {WrittenAt:O}, ReadAt = {ReadAt:O}, Language = {Language}");
        return true;
    }
}