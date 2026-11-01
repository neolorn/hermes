using System.Globalization;
using System.Text;

namespace Hermes.Core;

/// <summary>
/// A conversation of a topic's live view (<c>09</c> MSG-CONV-005; <c>05</c> MSG-API-009): open
/// or snoozed, with its assignee, its place in the queue and the minutes it has waited. It
/// prints no person reference.
/// </summary>
public sealed record LiveConversation
{
    /// <summary>The conversation's identifier.</summary>
    public required ConversationId Conversation { get; init; }

    /// <summary>Its status, open or snoozed.</summary>
    public required ConversationStatus Status { get; init; }

    /// <summary>The staff member assigned, or none while it is queued.</summary>
    public string? Assignee { get; init; }

    /// <summary>Its place in the topic's queue, from 1, or none once it is assigned.</summary>
    public int? Place { get; init; }

    /// <summary>The whole minutes since it was queued.</summary>
    public required int Waited { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Conversation = {Conversation.Value}, Status = {Status}, Place = {Place}, Waited = {Waited}");
        return true;
    }
}