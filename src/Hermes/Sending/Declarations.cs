using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Hermes.Sending;

/// <summary>What the host declared in <c>AddHermes</c>, in the order it declared it (<c>01</c> MSG-SEND-001, MSG-SEND-002).</summary>
/// <param name="Kinds">The kinds.</param>
/// <param name="Categories">The categories.</param>
/// <param name="Retired">The names of the kinds retired.</param>
/// <param name="Assemblies">The assemblies whose embedded resources hold templates (<c>03</c> MSG-TPL-006).</param>
internal sealed record Declarations(IReadOnlyList<KindDeclaration> Kinds, IReadOnlyList<CategoryDeclaration> Categories, IReadOnlyList<string> Retired, IReadOnlyList<Assembly> Assemblies)
{
    /// <summary>
    /// Whether an optional category has a kind with email, which needs
    /// <c>Hermes:PublicBaseAddress</c> (<c>08</c> section 4).
    /// </summary>
    internal bool NeedsPublicBaseAddress => Kinds.Any(kind => kind.Email is not null
        && Categories.Any(category => string.Equals(category.Name, kind.Category, StringComparison.Ordinal) && !category.Essential));
}