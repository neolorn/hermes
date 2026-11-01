using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Fluid.Values;

using Hermes.Content;
using Hermes.Core;
using Hermes.Operations;

namespace Hermes.Conversations.Support;

/// <summary>
/// The page <c>transcript</c> (<c>09</c> MSG-CONV-004; <c>03</c> MSG-TPL-006, MSG-TPL-007):
/// its one part <c>text</c>, read, parsed and checked at start in every declared language from
/// the host's template source, then the template folder, the host's assemblies and the
/// library's own, where topics are configured; rendered as plain text under ten times
/// <c>Hermes:Templates:MaxSteps</c>.
/// </summary>
internal sealed class TranscriptTemplates(TemplateRenderer renderer)
{
    /// <summary>The page's one part.</summary>
    internal const string Part = "text";

    /// <summary>How many times <c>Hermes:Templates:MaxSteps</c> a transcript's render may take.</summary>
    internal const int StepsTimes = 10;

    private FrozenDictionary<string, TemplatePart> parts = FrozenDictionary<string, TemplatePart>.Empty;

    /// <summary>
    /// Reads the part in each language, answering the first missing, or that does not parse or
    /// walk within the transcript's limits, as <c>messaging.startup.declarationinvalid</c> with
    /// <c>declaration</c> <c>transcript</c> and its field; none where every one is held.
    /// </summary>
    internal async Task<Dictionary<string, string>?> LoadAsync(
        MessagingOptions current,
        ITemplateSource? host,
        string contentRoot,
        IReadOnlyList<System.Reflection.Assembly> assemblies,
        TemplateParser parser,
        CancellationToken cancellationToken)
    {
        string folder = Path.GetFullPath(current.Templates.Path, Path.GetFullPath(contentRoot));
        TemplateSources sources = new(host, folder, assemblies);
        Dictionary<string, TemplatePart> loaded = new(StringComparer.Ordinal);
        foreach (string language in current.Languages)
        {
            string? text = await sources.ReadAsync(new TemplateKey { Page = Pages.Transcript, Language = language, Part = Part }, cancellationToken).ConfigureAwait(false);
            if (text is null
                || parser.Parse(text) is not { } template
                || TemplateWalk.Walk(template, current.Templates.MaxSteps * StepsTimes, current.Templates.MaxRecursion) is not { } reads)
            {
                return new Dictionary<string, string>(StringComparer.Ordinal) { ["declaration"] = Pages.Transcript, ["field"] = language + "." + Part };
            }

            loaded[language] = new TemplatePart(template, reads);
        }

        parts = loaded.ToFrozenDictionary(StringComparer.Ordinal);
        return null;
    }

    /// <summary>
    /// Renders the transcript in <paramref name="language"/> with <paramref name="values"/>, in
    /// the language's culture and <paramref name="zone"/>, at most <paramref name="bytes"/> in
    /// UTF-8; answers its text, or none where it renders past a bound or fails.
    /// </summary>
    internal async Task<string?> RenderAsync(string language, IReadOnlyDictionary<string, SendValue> values, TimeZoneInfo zone, long bytes)
    {
        if (!parts.TryGetValue(language, out TemplatePart? part))
        {
            return null;
        }

        int steps = renderer.MaxSteps * StepsTimes;
        Dictionary<string, FluidValue> fluid = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string name, SendValue value) in values)
        {
            if (TemplateValues.Of(value, steps) is not { } converted)
            {
                return null;
            }

            fluid[name] = converted;
        }

        Rendering rendering = await renderer.RenderAsync(part.Template, fluid, html: false, RenderBudget.Bytes(bytes), SendLanguage.Culture(language), zone, steps).ConfigureAwait(false);
        return rendering.Text;
    }
}