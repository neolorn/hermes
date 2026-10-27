using System;

namespace Hermes.Sending;

/// <summary>A row of the preference table (<c>01</c> MSG-SEND-010): whether a person receives a category on a channel, on where there is none.</summary>
internal sealed class PreferenceRow
{
    public required string Person { get; set; }

    public required string Category { get; set; }

    public required string Channel { get; set; }

    public required bool On { get; set; }

    public required DateTimeOffset SetAt { get; set; }
}