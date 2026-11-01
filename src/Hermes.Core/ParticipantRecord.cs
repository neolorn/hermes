using System.Globalization;
using System.Text;

namespace Hermes.Core;

/// <summary>A participant of a direct conversation (<c>09</c> MSG-CONV-006; <c>05</c> MSG-API-009). It prints no person reference.</summary>
public sealed record ParticipantRecord
{
    /// <summary>The participant's identifier.</summary>
    public required ParticipantId Participant { get; init; }

    /// <summary>The participant's person reference.</summary>
    public required string Person { get; init; }

    /// <summary>Whether the person holds a connection to the staff hub on any process (<c>09</c> MSG-CONV-006).</summary>
    public bool Online { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Participant = {Participant.Value}, Online = {Online}");
        return true;
    }
}