using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Hermes.Core;

/// <summary>
/// An in-app entry as an export holds it (<c>01</c> MSG-SEND-020): its parts in every
/// language it holds, its target and its target values, no content or target values where
/// its values' key was erased or a value did not open under its binding. It prints none of
/// them.
/// </summary>
public sealed record InAppEntryExport
{
    /// <summary>The kind.</summary>
    public required string Kind { get; init; }

    /// <summary>The kind's category.</summary>
    public required string Category { get; init; }

    /// <summary>The instant it was written.</summary>
    public required DateTimeOffset WrittenAt { get; init; }

    /// <summary>The instant it was marked read, or none.</summary>
    public DateTimeOffset? ReadAt { get; init; }

    /// <summary>The parts, by part and language joined by <c>.</c> (<c>title.en</c>).</summary>
    public required IReadOnlyDictionary<string, string> Content { get; init; }

    /// <summary>The key of where the entry leads, or none.</summary>
    public string? Target { get; init; }

    /// <summary>The target values, by name.</summary>
    public required IReadOnlyDictionary<string, SendValue> TargetValues { get; init; }

    /// <summary>Whether its values' key was erased, so it carries no content or target values.</summary>
    public required bool Erased { get; init; }

    /// <summary>Whether a value did not open under its binding, so it carries no content or target values.</summary>
    public required bool Rejected { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Kind = {Kind}, Category = {Category}, WrittenAt = {WrittenAt:O}, ReadAt = {ReadAt:O}, Erased = {Erased}, Rejected = {Rejected}");
        return true;
    }
}