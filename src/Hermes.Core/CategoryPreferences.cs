using System.Collections.Generic;

namespace Hermes.Core;

/// <summary>A person's preferences for one declared category, on each channel its kinds use (<c>01</c> MSG-SEND-010).</summary>
public sealed record CategoryPreferences
{
    /// <summary>The category's declared name.</summary>
    public required string Category { get; init; }

    /// <summary>Whether the category is essential, and so cannot be turned off.</summary>
    public required bool Essential { get; init; }

    /// <summary>The channels its kinds use, each with whether the person receives it there.</summary>
    public required IReadOnlyList<ChannelPreference> Channels { get; init; }
}