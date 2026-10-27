using System.Collections.Generic;

using Hermes.Core;

namespace Hermes.Content;

/// <summary>A channel's rendered parts, by part name.</summary>
internal sealed record RenderedChannel(Channel Channel, IReadOnlyDictionary<string, string> Parts);