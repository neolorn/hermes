using System;
using System.Collections.Generic;

namespace Hermes.Channels;

/// <summary>
/// A key of <c>08</c> section 4 holding a list, which takes its default only where none of
/// its items is configured (<c>06</c> MSG-OPS-001).
/// </summary>
/// <param name="Path">The key as <c>08</c> spells it, under <c>Hermes</c>.</param>
/// <param name="Item">Whether an item is within the key's rule.</param>
/// <param name="Default">The items where none is configured.</param>
internal sealed record ListKey(string Path, Func<string, bool> Item, IReadOnlyList<string> Default);