using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Hermes.Core;

/// <summary>A page of a conversation's messages, newest first (<c>09</c> MSG-CONV-001; <c>05</c> MSG-API-009). It prints none of its messages.</summary>
public sealed record MessagePage
{
    /// <summary>The page's messages, newest first.</summary>
    public required IReadOnlyList<MessageRecord> Messages { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Messages = {Messages.Count}");
        return true;
    }
}