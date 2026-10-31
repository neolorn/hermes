using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Fluid;

using Hermes.Core;
using Hermes.Sending;

namespace Hermes.Content;

/// <summary>
/// Loads, parses and checks every template part when the application starts (<c>03</c>
/// MSG-TPL-004): each part of each channel of each declared kind in each declared language,
/// then each page in each declared language. The first part outside its rule is answered with
/// the kind (or the page) and the channel, language and part, written as the part's file name
/// is (<c>email.ar.subject</c>; for a page, <c>ar.html</c>).
/// </summary>
internal static class TemplateCheck
{
    /// <summary>
    /// Reads every part from <paramref name="sources"/> into <paramref name="loaded"/>, answering
    /// the first outside its rule, or none.
    /// </summary>
    internal static async ValueTask<(string Declaration, string Field)?> LoadAsync(
        Declarations declarations,
        IReadOnlyList<string> languages,
        int maxSteps,
        int maxRecursion,
        TemplateSources sources,
        TemplateParser parser,
        Dictionary<TemplateKey, TemplatePart> loaded,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(declarations);
        ArgumentNullException.ThrowIfNull(languages);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(loaded);

        foreach (KindDeclaration kind in declarations.Kinds)
        {
            bool optional = !declarations.Categories.First(category => string.Equals(category.Name, kind.Category, StringComparison.Ordinal)).Essential;
            foreach (Channel channel in Channels(kind))
            {
                foreach (string language in languages)
                {
                    foreach ((string part, bool required) in Parts(channel))
                    {
                        TemplateKey key = new() { Kind = kind.Name, Channel = channel, Language = language, Part = part };
                        string field = $"{Spellings.Of(channel)}.{language}.{part}";
                        string? text = await sources.ReadAsync(key, cancellationToken).ConfigureAwait(false);
                        if (text is null)
                        {
                            if (required)
                            {
                                return (kind.Name, field);
                            }

                            continue;
                        }

                        bool unsubscribes = optional && channel == Channel.Email;
                        if (Part(parser, text, maxSteps, maxRecursion) is not { } loadedPart
                            || (Reads(loadedPart, TemplateNames.Unsubscribe) && !unsubscribes)
                            || (unsubscribes && (part is TemplateParts.Text or TemplateParts.Html) && !Reads(loadedPart, TemplateNames.Unsubscribe)))
                        {
                            return (kind.Name, field);
                        }

                        loaded[key] = loadedPart;
                    }
                }
            }
        }

        foreach (string page in Pages.All)
        {
            foreach (string language in languages)
            {
                TemplateKey key = new() { Page = page, Language = language, Part = TemplateParts.Html };
                string field = $"{language}.{TemplateParts.Html}";
                string? text = await sources.ReadAsync(key, cancellationToken).ConfigureAwait(false);
                if (text is null)
                {
                    if (declarations.NeedsPublicBaseAddress)
                    {
                        return (page, field);
                    }

                    continue;
                }

                if (Part(parser, text, maxSteps, maxRecursion) is not { } loadedPart
                    || Reads(loadedPart, TemplateNames.Unsubscribe)
                    || (Reads(loadedPart, Pages.Category) && !Pages.Gives(page, Pages.Category))
                    || (Reads(loadedPart, Pages.Action) && !Pages.Gives(page, Pages.Action)))
                {
                    return (page, field);
                }

                loaded[key] = loadedPart;
            }
        }

        return null;
    }

    private static TemplatePart? Part(TemplateParser parser, string text, int maxSteps, int maxRecursion)
    {
        return parser.Parse(text) is IFluidTemplate template && TemplateWalk.Walk(template, maxSteps, maxRecursion) is { } reads
            ? new TemplatePart(template, reads)
            : null;
    }

    private static bool Reads(TemplatePart part, string name)
    {
        return part.Reads.All.Contains(name);
    }

    private static IEnumerable<Channel> Channels(KindDeclaration kind)
    {
        if (kind.Email is not null)
        {
            yield return Channel.Email;
        }

        if (kind.Sms is not null)
        {
            yield return Channel.Sms;
        }

        if (kind.Push is not null)
        {
            yield return Channel.Push;
        }

        if (kind.InApp is not null)
        {
            yield return Channel.InApp;
        }
    }

    private static IEnumerable<(string Part, bool Required)> Parts(Channel channel)
    {
        return TemplateParts.Required(channel).Select(part => (part, true)).Concat(TemplateParts.Optional(channel).Select(part => (part, false)));
    }
}