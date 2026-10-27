using System.Collections.Generic;

using Hermes.Core;

namespace Hermes.Content;

/// <summary>A send's render: each channel's rendered parts, or the code and channel it is refused with.</summary>
/// <param name="Channels">The rendered channels, in the order rendered, where none was refused.</param>
/// <param name="Refusal">The code, where a channel was refused.</param>
/// <param name="Channel">The channel refused.</param>
internal sealed record SendRender(IReadOnlyList<RenderedChannel> Channels, string? Refusal, Channel? Channel)
{
    internal static SendRender Done(IReadOnlyList<RenderedChannel> channels)
    {
        return new SendRender(channels, null, null);
    }

    internal static SendRender Refused(string code, Channel channel)
    {
        return new SendRender([], code, channel);
    }
}