using System.Collections.Generic;

namespace Hermes.Endpoints;

/// <summary>The answer of <c>GET /preferences</c> (<c>05</c> MSG-API-003).</summary>
/// <param name="Categories">Every declared category, in declaration order.</param>
internal sealed record PreferencesBody(IReadOnlyList<CategoryBody> Categories);