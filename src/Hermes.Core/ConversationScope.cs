namespace Hermes.Core;

/// <summary>A scope of the membership seam: an organization or a group, by its reference (<c>09</c> MSG-CONV-006).</summary>
public sealed record ConversationScope
{
    /// <summary>The scope's kind.</summary>
    public required ScopeKind Kind { get; init; }

    /// <summary>The scope's reference, as the host knows it.</summary>
    public required string Reference { get; init; }
}