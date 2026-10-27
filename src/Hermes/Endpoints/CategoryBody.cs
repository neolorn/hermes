using System.Collections.Generic;

namespace Hermes.Endpoints;

/// <summary>A category of <c>GET /preferences</c>, with the channels its kinds use.</summary>
/// <param name="Name">The category's declared name.</param>
/// <param name="Essential">Whether the person cannot turn it off.</param>
/// <param name="Channels">The channels its kinds use, each on or off.</param>
internal sealed record CategoryBody(string Name, bool Essential, IReadOnlyList<ChannelBody> Channels);