namespace Hermes.Conversations;

/// <summary>The package's settings read at start (<c>08</c> section 4).</summary>
internal sealed class ConversationsSettings
{
    /// <summary>The kind <c>Hermes:Conversations:AwayKind</c> names, a declared one, or none: no away notice is sent.</summary>
    internal string? AwayKind { get; set; }
}