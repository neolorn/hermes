using System;

namespace Hermes.Channels;

/// <summary>
/// The bound on each provider call, <c>Dispatch:Timeout</c> (<c>08</c> section 4), read at
/// each call so that a reloaded value bounds the next (<c>02</c> MSG-CHAN-001).
/// </summary>
internal sealed class CallTimeout(Func<TimeSpan> current)
{
    /// <summary>The bound in force.</summary>
    internal TimeSpan Current => current();
}