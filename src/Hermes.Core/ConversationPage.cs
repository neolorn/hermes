using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Hermes.Core;

/// <summary>
/// A page of a person's conversations, newest activity first (<c>09</c> MSG-CONV-006; <c>05</c>
/// MSG-API-009). It prints none of its conversations.
/// </summary>
public sealed record ConversationPage
{
    /// <summary>The page's conversations, newest activity first.</summary>
    public required IReadOnlyList<ConversationRecord> Conversations { get; init; }

    /// <summary>The cursor of the next page, or none on the last.</summary>
    public string? Next { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Conversations = {Conversations.Count}");
        return true;
    }
}