using System;

namespace Hermes.Conversations;

/// <summary>A person a message mentions, one row each (<c>09</c> MSG-CONV-001, MSG-CONV-006).</summary>
internal sealed class MentionRow
{
    public required Guid MessageId { get; set; }

    public required string Person { get; set; }
}