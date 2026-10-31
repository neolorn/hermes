using System;

namespace Hermes.Sending;

/// <summary>
/// An entry of a person's in-application list (<c>02</c> MSG-INAPP-001): its delivery's parts
/// in every declared language and its target values, protected bound to the entry's row, with
/// the send's language, its instants and the target the kind declared when it was written.
/// </summary>
internal sealed class EntryRow
{
    public required Guid Id { get; set; }

    public required string Person { get; set; }

    public required string Kind { get; set; }

    public required string Category { get; set; }

    /// <summary>The send's language, the entry's last fallback when it is read.</summary>
    public required string Language { get; set; }

    public required DateTimeOffset WrittenAt { get; set; }

    public DateTimeOffset? ReadAt { get; set; }

    /// <summary>The delivery that wrote it, until the retention job deletes that delivery's record.</summary>
    public Guid? DeliveryId { get; set; }

    /// <summary>The target the kind declared, or none.</summary>
    public string? Target { get; set; }

    /// <summary>The protected values, as <see cref="InAppParts"/> writes them: each part by part and language, and the target values.</summary>
    public required string Parts { get; set; }

    /// <summary>The <c>contentKeys</c> version the library's protector wrote the values under, or none for a host's protector.</summary>
    public int? ContentKeyVersion { get; set; }
}