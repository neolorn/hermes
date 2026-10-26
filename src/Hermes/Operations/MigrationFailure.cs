using System.Collections.Generic;

namespace Hermes.Operations;

/// <summary>Why <c>hermes migrate</c> failed: a <c>(tool)</c> code of <c>08</c> section 1 and its details.</summary>
/// <param name="Code">The code.</param>
/// <param name="Details">The details.</param>
internal sealed record MigrationFailure(string Code, IReadOnlyDictionary<string, string> Details);