using System;
using System.Collections.Generic;
using System.Linq;

namespace Hermes.Sending;

/// <summary>What the host declared in <c>AddHermes</c>, in the order it declared it (<c>01</c> MSG-SEND-001, MSG-SEND-002).</summary>
/// <param name="Kinds">The kinds.</param>
/// <param name="Categories">The categories.</param>
/// <param name="Retired">The names of the kinds retired.</param>
internal sealed record Declarations(IReadOnlyList<KindDeclaration> Kinds, IReadOnlyList<CategoryDeclaration> Categories, IReadOnlyList<string> Retired)
{
    /// <summary>
    /// Whether an optional category has a kind with email, which needs
    /// <c>Hermes:PublicBaseAddress</c> (<c>08</c> section 4).
    /// </summary>
    internal bool NeedsPublicBaseAddress => Kinds.Any(kind => kind.Email is not null
        && Categories.Any(category => string.Equals(category.Name, kind.Category, StringComparison.Ordinal) && !category.Essential));
}