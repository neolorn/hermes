using System.Collections.Generic;

namespace Hermes.Endpoints;

/// <summary>A refusal as an endpoint answers it (<c>05</c> MSG-API-001).</summary>
/// <param name="Code">The refusal's code.</param>
/// <param name="Details">The details <c>08</c> section 1 gives the code.</param>
internal sealed record ErrorBody(string Code, IReadOnlyDictionary<string, string> Details);