using System;

namespace Hermes.Sending;

/// <summary>
/// A row of the send table: the send's identifier, its kind, its instant and the idempotency key
/// it was recorded with, nothing else of the recipient (<c>01</c> MSG-SEND-003, MSG-SEND-017).
/// </summary>
internal sealed class SendRow
{
    public required Guid Id { get; set; }

    public required string Kind { get; set; }

    public required DateTimeOffset RecordedAt { get; set; }

    /// <summary>The key the host gave the send, kept for its notices as long as the send's record, none once its person is erased.</summary>
    public string? IdempotencyKey { get; set; }
}