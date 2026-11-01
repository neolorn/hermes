using System.Globalization;
using System.Text;

namespace Hermes.Core;

/// <summary>
/// A saved reply (<c>09</c> MSG-CONV-005; <c>05</c> MSG-API-009): its name and plain text,
/// shared by a topic or a staff member's own. It prints neither its name nor its text.
/// </summary>
public sealed record SavedReplyRecord
{
    /// <summary>The reply's identifier.</summary>
    public required SavedReplyId Id { get; init; }

    /// <summary>The reply's name, 1 to 64 characters, unique among its owner's.</summary>
    public required string Name { get; init; }

    /// <summary>The reply's text, 1 to 4,000 characters, plain text without templating.</summary>
    public required string Text { get; init; }

    /// <summary>The topic that shares it, or none for a staff member's own.</summary>
    public string? Topic { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Id = {Id.Value}, Topic = {Topic}");
        return true;
    }
}