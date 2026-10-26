using System;

namespace Hermes.Sending;

/// <summary>A row of the send table: the send's identifier, its kind and its instant, nothing of the recipient (<c>01</c> MSG-SEND-003).</summary>
internal sealed class SendRow
{
    public required Guid Id { get; set; }

    public required string Kind { get; set; }

    public required DateTimeOffset RecordedAt { get; set; }
}