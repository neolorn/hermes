using System;

namespace Hermes.Sending;

/// <summary>
/// A row of the delivery table: one channel of a send (<c>01</c> MSG-SEND-005), its status,
/// its claim and its protected values (MSG-SEND-018), each an envelope bound to this row and
/// its person.
/// </summary>
internal sealed class DeliveryRow
{
    public required Guid Id { get; set; }

    public required Guid SendId { get; set; }

    public required string Kind { get; set; }

    public required string Category { get; set; }

    public required string Channel { get; set; }

    public required string Status { get; set; }

    public string? Reason { get; set; }

    /// <summary>The person reference, none for an anonymous recipient.</summary>
    public string? Person { get; set; }

    public required string Language { get; set; }

    public required string TimeZone { get; set; }

    /// <summary>The push requests' <c>Topic</c>, computed once at the send (<c>02</c> MSG-PUSH-002).</summary>
    public string? PushTopic { get; set; }

    public byte[]? Destination { get; set; }

    public byte[]? Unsubscribe { get; set; }

    public byte[]? Subject { get; set; }

    public byte[]? Text { get; set; }

    public byte[]? Html { get; set; }

    public byte[]? Title { get; set; }

    public byte[]? Body { get; set; }

    public byte[]? Url { get; set; }

    public byte[]? Tag { get; set; }

    public byte[]? Icon { get; set; }

    public byte[]? Badge { get; set; }

    /// <summary>The idempotency key the delivery keeps of its send, protected as its destination is (<c>01</c> MSG-SEND-017).</summary>
    public byte[]? IdempotencyKey { get; set; }

    /// <summary>An in-app delivery's protected values, as <see cref="InAppParts"/> writes them, until its dispatch moves them into its entry.</summary>
    public string? Parts { get; set; }

    /// <summary>The <c>contentKeys</c> version of the values the library's protector made, none where it made none (<c>06</c> MSG-OPS-008).</summary>
    public int? ContentKeyVersion { get; set; }

    public required DateTimeOffset RecordedAt { get; set; }

    public required DateTimeOffset DueAt { get; set; }

    /// <summary>The instant it became due, from which <c>dispatch-lag</c> is measured (<c>01</c> MSG-SEND-007).</summary>
    public required DateTimeOffset BecameDueAt { get; set; }

    public required DateTimeOffset LastMovedAt { get; set; }

    /// <summary>The attempts counted.</summary>
    public int Attempts { get; set; }

    /// <summary>The instant of the first hold for a key since the last counted attempt (<c>01</c> MSG-SEND-006).</summary>
    public DateTimeOffset? FirstHeldAt { get; set; }

    public Guid? ClaimedBy { get; set; }

    public DateTimeOffset? ClaimedAt { get; set; }

    public DateTimeOffset? ClaimExpiresAt { get; set; }

    public string? ProviderReference { get; set; }

    /// <summary>The sequence number of this delivery's next notice (<c>01</c> MSG-SEND-017).</summary>
    public int NextSequence { get; set; }
}