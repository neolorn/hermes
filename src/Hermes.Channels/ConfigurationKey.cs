using System;

namespace Hermes.Channels;

/// <summary>
/// A key of <c>08</c> section 4 holding one value: how its raw text reads, its own rule, and
/// its default as the options type holds it (<c>06</c> MSG-OPS-001).
/// </summary>
/// <param name="Path">The key as <c>08</c> spells it, under <c>Hermes</c>.</param>
/// <param name="StartOnly">Whether the key takes effect only at the next start.</param>
/// <param name="Read">The value a raw text reads as, as the binder reads it; none where it does not read.</param>
/// <param name="Within">Whether a value that reads is within the key's own rule.</param>
/// <param name="Default">The key's value where it is absent.</param>
internal sealed record ConfigurationKey(string Path, bool StartOnly, Func<string, object?> Read, Func<object, bool> Within, object? Default);