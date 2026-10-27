using System;
using System.Globalization;
using System.Text;

namespace Hermes.Core;

/// <summary>A person's live idempotency row as an export holds it (<c>01</c> MSG-SEND-020). It prints no key, which a host may build from the person's data.</summary>
public sealed record IdempotencyKeyExport
{
    /// <summary>The kind of the send it recorded.</summary>
    public required string Kind { get; init; }

    /// <summary>The idempotency key.</summary>
    public required string Key { get; init; }

    /// <summary>The instant it was recorded.</summary>
    public required DateTimeOffset RecordedAt { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Kind = {Kind}, RecordedAt = {RecordedAt:O}");
        return true;
    }
}