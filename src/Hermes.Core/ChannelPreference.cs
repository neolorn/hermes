namespace Hermes.Core;

/// <summary>Whether a person receives a category on one channel (<c>01</c> MSG-SEND-010).</summary>
public sealed record ChannelPreference
{
    /// <summary>The channel.</summary>
    public required Channel Channel { get; init; }

    /// <summary>Whether the person receives the category on it.</summary>
    public required bool On { get; init; }
}