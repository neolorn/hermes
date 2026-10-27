using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Fluid.Values;

using Hermes.Operations;
using Hermes.Sending;

using Microsoft.Extensions.Options;

namespace Hermes.Content;

/// <summary>
/// Renders the library's own pages (<c>03</c> MSG-TPL-006) in a language, or in the default
/// where the language is no longer declared or holds no page, with the values each is given.
/// </summary>
internal sealed class PageRenderer(Templates templates, TemplateRenderer renderer, IOptionsMonitor<MessagingOptions> options)
{
    /// <summary>The most bytes a page renders to.</summary>
    private const int MostBytes = 256 * 1024;

    /// <summary>The page's HTML, or none where neither the language nor the default holds it.</summary>
    internal async ValueTask<string?> RenderAsync(string page, string? language, string? category = null, string? action = null)
    {
        MessagingOptions current = options.CurrentValue;
        string chosen = language is not null && Array.IndexOf(current.Languages, language) >= 0 && templates.Page(page, language) is not null
            ? language
            : current.DefaultLanguage!;
        if (templates.Page(page, chosen) is not { } part)
        {
            return null;
        }

        Dictionary<string, FluidValue> values = new(StringComparer.OrdinalIgnoreCase)
        {
            [TemplateNames.Language] = new StringValue(chosen),
            [TemplateNames.Direction] = new StringValue(SendLanguage.Direction(chosen)),
        };
        if (category is not null && Pages.Gives(page, Pages.Category))
        {
            values[Pages.Category] = new StringValue(category);
        }

        if (action is not null && Pages.Gives(page, Pages.Action))
        {
            values[Pages.Action] = new StringValue(action);
        }

        Rendering rendering = await renderer.RenderAsync(part.Template, values, html: true, RenderBudget.Bytes(MostBytes), SendLanguage.Culture(chosen), TimeZoneInfo.Utc).ConfigureAwait(false);
        return rendering.Text ?? throw new InvalidOperationException($"The page {page} in {chosen} is refused with {rendering.Refusal}, though the startup walk passed it.");
    }

    /// <summary>The label of <paramref name="category"/> in the language the page renders in, or its name where it has none there.</summary>
    internal string Label(CategoryDeclaration category, string? language)
    {
        MessagingOptions current = options.CurrentValue;
        string chosen = language is not null && Array.IndexOf(current.Languages, language) >= 0 ? language : current.DefaultLanguage!;
        return category.Labels.TryGetValue(chosen, out string? label) ? label : category.Name;
    }
}