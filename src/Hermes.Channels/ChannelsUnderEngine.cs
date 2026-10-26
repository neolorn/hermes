namespace Hermes.Channels;

/// <summary>
/// Registered by <c>AddHermes</c>: the engine runs the channels' checks among its own, with
/// its own definition of what is used (<c>04</c> MSG-LIB-003), so the channels' own startup
/// does nothing.
/// </summary>
internal sealed class ChannelsUnderEngine;