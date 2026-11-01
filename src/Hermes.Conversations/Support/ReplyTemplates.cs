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
using Hermes.Sending;

namespace Hermes.Conversations.Support;

/// <summary>
/// The automatic reply rules' templates (<c>09</c> MSG-CONV-003; <c>03</c> MSG-TPL-004): read,
/// parsed and checked at start in every declared language, from the host's template source,
/// then <c>replies/&lt;name&gt;.&lt;language&gt;.text.liquid</c> under the template folder and
/// the host's assemblies; and rendered with the reply's four values alone, bounded as a
/// message's text is.
/// </summary>
internal sealed class ReplyTemplates(TemplateRenderer renderer)
{
    /// <summary>The part every reply's template has.</summary>
    internal const string Part = "text";

    private FrozenDictionary<(string Rule, string Language), TemplatePart> parts = FrozenDictionary<(string Rule, string Language), TemplatePart>.Empty;

    /// <summary>
    /// Reads each rule's template in each language, answering the first missing as
    /// <c>messaging.startup.templatemissing</c> or the first that does not parse or walk within
    /// the limits as <c>messaging.startup.declarationinvalid</c>, with its details; none where
    /// every one is held.
    /// </summary>
    internal async Task<(string Code, Dictionary<string, string> Details)?> LoadAsync(
        IReadOnlyList<ReplyRule> rules,
        MessagingOptions current,
        ITemplateSource? host,
        string contentRoot,
        IReadOnlyList<System.Reflection.Assembly> assemblies,
        TemplateParser parser,
        CancellationToken cancellationToken)
    {
        string folder = Path.GetFullPath(current.Templates.Path, Path.GetFullPath(contentRoot));
        TemplateSources sources = new(host, folder, assemblies);
        Dictionary<(string Rule, string Language), TemplatePart> loaded = [];
        foreach (ReplyRule rule in rules)
        {
            foreach (string language in current.Languages)
            {
                string? text = await sources.ReadAsync(new TemplateKey { Reply = rule.Name, Language = language, Part = Part }, cancellationToken).ConfigureAwait(false);
                if (text is null)
                {
                    return (Codes.TemplateMissing, new Dictionary<string, string>(StringComparer.Ordinal) { ["rule"] = rule.Name, ["language"] = language });
                }

                if (parser.Parse(text) is not { } template || TemplateWalk.Walk(template, current.Templates.MaxSteps, current.Templates.MaxRecursion) is not { } reads)
                {
                    return (Codes.DeclarationInvalid, new Dictionary<string, string>(StringComparer.Ordinal) { ["declaration"] = rule.Name, ["field"] = language + "." + Part });
                }

                loaded[(rule.Name, language)] = new TemplatePart(template, reads);
            }
        }

        parts = loaded.ToFrozenDictionary();
        return null;
    }

    /// <summary>
    /// Renders <paramref name="rule"/>'s template in <paramref name="language"/> with
    /// <paramref name="values"/>, in the language's culture and <paramref name="zone"/>; answers
    /// its text, or none where it is not held, fails to render, or renders past 4,000 characters.
    /// </summary>
    internal async Task<string?> RenderAsync(string rule, string language, IReadOnlyDictionary<string, SendValue?> values, TimeZoneInfo zone, int maxSteps)
    {
        if (!parts.TryGetValue((rule, language), out TemplatePart? part))
        {
            return null;
        }

        Dictionary<string, FluidValue> fluid = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string name, SendValue? value) in values)
        {
            fluid[name] = value is null ? NilValue.Instance : TemplateValues.Of(value, maxSteps) ?? NilValue.Instance;
        }

        Rendering rendering = await renderer.RenderAsync(part.Template, fluid, html: false, RenderBudget.Units(Chats.StaffChats.MostTextCharacters + 1), SendLanguage.Culture(language), zone).ConfigureAwait(false);
        return rendering.Text is { Length: >= 1 and <= Chats.StaffChats.MostTextCharacters } text ? text : null;
    }
}