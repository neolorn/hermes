using System.Globalization;
using System.Text;

namespace Hermes.Core;

/// <summary>A person's survey answer in their export (<c>09</c> MSG-CONV-004, MSG-CONV-010). It prints no comment.</summary>
public sealed record SurveyExport
{
    /// <summary>Whether the conversation resolved the matter.</summary>
    public required bool Resolved { get; init; }

    /// <summary>The rating, 1 to 5.</summary>
    public required int Rating { get; init; }

    /// <summary>The comment, none where none was given or it cannot be read.</summary>
    public string? Comment { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Resolved = {Resolved}, Rating = {Rating}");
        return true;
    }
}