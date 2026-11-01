using System;

namespace Hermes.Conversations;

/// <summary>A staff member's support availability (<c>09</c> MSG-CONV-002): set by them, and the instant of their last assignment, which breaks ties.</summary>
internal sealed class SupportAgentRow
{
    public required Guid Id { get; set; }

    public required string Person { get; set; }

    public bool Available { get; set; }

    public DateTimeOffset? LastAssignedAt { get; set; }

    public required DateTimeOffset ChangedAt { get; set; }
}

/// <summary>A block of a visitor or a customer (<c>09</c> MSG-CONV-011), until an instant or until lifted; it holds exactly one of a person and a visitor's hash.</summary>
internal sealed class SupportBlockRow
{
    public required Guid Id { get; set; }

    public string? Person { get; set; }

    public byte[]? Visitor { get; set; }

    /// <summary>The <c>hashKeys</c> version of <see cref="Visitor"/>, or none for a person.</summary>
    public int? HashVersion { get; set; }

    /// <summary>The instant it ends, or none until it is lifted.</summary>
    public DateTimeOffset? Until { get; set; }

    public required DateTimeOffset BlockedAt { get; set; }
}

/// <summary>A visitor's acknowledgement of a version of the host's privacy notice (<c>09</c> MSG-CONV-010).</summary>
internal sealed class SupportAcknowledgementRow
{
    public required Guid Id { get; set; }

    public required byte[] Visitor { get; set; }

    public required int HashVersion { get; set; }

    public required string Version { get; set; }

    public required DateTimeOffset AcknowledgedAt { get; set; }

    public required string Language { get; set; }
}

/// <summary>A limit's counter (<c>09</c> MSG-CONV-011): what it counts, for whom by keyed hash, and the window it counts in.</summary>
internal sealed class SupportCounterRow
{
    public required Guid Id { get; set; }

    /// <summary><c>visitor-starts</c>, <c>visitor-messages</c> or <c>source-starts</c>.</summary>
    public required string Kind { get; set; }

    public required byte[] Hash { get; set; }

    public required int HashVersion { get; set; }

    public required DateTimeOffset WindowStart { get; set; }

    public required int Count { get; set; }
}

/// <summary>A saved reply (<c>09</c> MSG-CONV-005): shared by a topic or a staff member's own, exactly one of the two, its name unique among its owner's.</summary>
internal sealed class SupportReplyRow
{
    public required Guid Id { get; set; }

    public string? Topic { get; set; }

    public string? Person { get; set; }

    public required string Name { get; set; }

    public required string Text { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }
}