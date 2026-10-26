using System;

using Hermes.Core;

namespace Hermes.Sending;

/// <summary>A cap a category declares on a channel (<c>01</c> MSG-SEND-014).</summary>
/// <param name="Channel">The channel it counts.</param>
/// <param name="Count">The most deliveries it allows within the window.</param>
/// <param name="Window">The window it counts over.</param>
internal sealed record LimitDeclaration(Channel Channel, int Count, TimeSpan Window);