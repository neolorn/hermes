using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Hermes.Core;

/// <summary>
/// A delivery as an export holds it (<c>01</c> MSG-SEND-020): its destination and content
/// while it holds them, neither once it is past content retention, its values' key was
/// erased, or a value did not open under its binding. It prints neither.
/// </summary>
public sealed record DeliveryExport
{
    /// <summary>The kind.</summary>
    public required string Kind { get; init; }

    /// <summary>The channel.</summary>
    public required Channel Channel { get; init; }

    /// <summary>The status.</summary>
    public required DeliveryStatus Status { get; init; }

    /// <summary>The instant it was recorded.</summary>
    public required DateTimeOffset RecordedAt { get; init; }

    /// <summary>The instant of its last move.</summary>
    public required DateTimeOffset LastMovedAt { get; init; }

    /// <summary>The destination: the address or number, or none for push and where it holds none.</summary>
    public string? Destination { get; init; }

    /// <summary>The content, by part.</summary>
    public required IReadOnlyDictionary<string, string> Content { get; init; }

    /// <summary>Whether it is past content retention, so it carries no destination or content.</summary>
    public required bool ContentDeleted { get; init; }

    /// <summary>Whether its values' key was erased, so it carries no destination or content.</summary>
    public required bool Erased { get; init; }

    /// <summary>Whether a value did not open under its binding, so it carries no destination or content.</summary>
    public required bool Rejected { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Kind = {Kind}, Channel = {Channel}, Status = {Status}, RecordedAt = {RecordedAt:O}, LastMovedAt = {LastMovedAt:O}, ContentDeleted = {ContentDeleted}, Erased = {Erased}, Rejected = {Rejected}");
        return true;
    }
}