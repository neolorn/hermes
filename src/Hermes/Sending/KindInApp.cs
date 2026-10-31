using System.Collections.Generic;

namespace Hermes.Sending;

/// <summary>A kind's in-app options (<c>02</c> MSG-INAPP-001).</summary>
/// <param name="Target">The key of where its entries lead, or none.</param>
/// <param name="TargetValues">The names of the send's top-level scalar values that go with the target, none where there is no target.</param>
internal sealed record KindInApp(string? Target, IReadOnlyList<string> TargetValues);