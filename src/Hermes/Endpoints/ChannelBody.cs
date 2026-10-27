namespace Hermes.Endpoints;

/// <summary>A channel of a category of <c>GET /preferences</c>.</summary>
/// <param name="Channel">The channel, as <c>08</c> section 2 spells it.</param>
/// <param name="On">Whether the person receives the category on it.</param>
internal sealed record ChannelBody(string Channel, bool On);