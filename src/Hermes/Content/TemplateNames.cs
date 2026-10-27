using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace Hermes.Content;

/// <summary>The values the library adds to a template, whose names a send does not use (<c>03</c> MSG-TPL-002), and Fluid's loop value.</summary>
internal static class TemplateNames
{
    /// <summary>The one-click address, in an optional category's email only.</summary>
    internal const string Unsubscribe = "unsubscribe";

    /// <summary>The send's language.</summary>
    internal const string Language = "language";

    /// <summary>The send's language's direction, <c>ltr</c> or <c>rtl</c>.</summary>
    internal const string Direction = "direction";

    /// <summary>Fluid's loop value.</summary>
    internal const string ForLoop = "forloop";

    /// <summary>The names a send does not use, compared as Fluid compares names, ignoring case.</summary>
    internal static IReadOnlySet<string> Reserved { get; } = FrozenSet.Create(StringComparer.OrdinalIgnoreCase, Unsubscribe, Language, Direction);
}