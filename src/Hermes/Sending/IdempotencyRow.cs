using System;

namespace Hermes.Sending;

/// <summary>
/// A row of the idempotency table (<c>01</c> MSG-SEND-009): the kind, the scope, the key, the
/// receipt, the instants and the digest of the send under a <c>hashKeys</c> version, never the
/// send itself.
/// </summary>
internal sealed class IdempotencyRow
{
    public required string Kind { get; set; }

    /// <summary>Whether the scope is an anonymous recipient's destinations' hashes rather than a person reference.</summary>
    public required bool Anonymous { get; set; }

    /// <summary>The person reference's UTF-8 bytes, or the email's and the phone's keyed hashes, 64 bytes.</summary>
    public required byte[] Scope { get; set; }

    public required string Key { get; set; }

    public required Guid SendId { get; set; }

    /// <summary>The receipt, as the delivery identifiers by channel spelling, in JSON.</summary>
    public required string Receipt { get; set; }

    public required byte[] Digest { get; set; }

    public required int HashVersion { get; set; }

    public required DateTimeOffset RecordedAt { get; set; }

    public required DateTimeOffset LapsesAt { get; set; }
}