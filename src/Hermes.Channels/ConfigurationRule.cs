using System;
using System.Collections.Generic;

namespace Hermes.Channels;

/// <summary>
/// A rule of <c>08</c> section 4 that bounds one key by others, such as
/// <c>Dispatch:ClaimTimeout</c>'s "at least twice <c>Dispatch:Timeout</c> plus 30 seconds".
/// </summary>
/// <param name="Path">The key whose rule it is, which a value outside it names.</param>
/// <param name="Refers">The other keys the rule reads.</param>
/// <param name="Holds">Whether the rule holds, given each key's value.</param>
internal sealed record ConfigurationRule(string Path, IReadOnlyList<string> Refers, Func<Func<string, object>, bool> Holds);