using System;
using System.Collections.Frozen;
using System.Collections.Generic;

using Hermes.Core;

namespace Hermes.Content;

/// <summary>
/// The template parts read, parsed and checked when the application started, kept until it
/// stops, so a changed template takes effect at the next start (<c>03</c> MSG-TPL-006).
/// </summary>
internal sealed class Templates
{
    private FrozenDictionary<TemplateKey, TemplatePart> parts = FrozenDictionary<TemplateKey, TemplatePart>.Empty;

    /// <summary>Keeps <paramref name="loaded"/>, the parts the startup check passed.</summary>
    internal void Hold(IReadOnlyDictionary<TemplateKey, TemplatePart> loaded)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        parts = loaded.ToFrozenDictionary();
    }

    /// <summary>A kind's part in a declared language, or none where its template has no such part.</summary>
    internal TemplatePart? Kind(string kind, Channel channel, string language, string part)
    {
        return parts.GetValueOrDefault(new TemplateKey { Kind = kind, Channel = channel, Language = language, Part = part });
    }

    /// <summary>A page's <c>html</c> part in a declared language, or none.</summary>
    internal TemplatePart? Page(string page, string language)
    {
        return parts.GetValueOrDefault(new TemplateKey { Page = page, Language = language, Part = TemplateParts.Html });
    }
}