using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

using Fluid.Values;

using Hermes.Channels;
using Hermes.Core;
using Hermes.Sending;

using Microsoft.Extensions.Options;

namespace Hermes.Content;

/// <summary>
/// Renders a send's channels when it is recorded (<c>01</c> MSG-SEND-018; <c>03</c>
/// MSG-TPL-002), the in-app channel in every declared language and every other in the send's
/// language: a value a template reads that the send does not pass is a fault found before any
/// render; each channel's parts are rendered within its limit (<c>02</c> MSG-EMAIL-003,
/// MSG-SMS-001, MSG-PUSH-002, MSG-INAPP-002; <c>03</c> MSG-TPL-007), the first refusal
/// answering with its code and channel.
/// </summary>
internal sealed class SendRenderer(Templates templates, TemplateRenderer renderer, IOptionsMonitor<ChannelsOptions> channels)
{
    /// <summary>The most characters of an in-app title (<c>02</c> MSG-INAPP-002).</summary>
    internal const int InAppTitle = 128;

    /// <summary>The most characters of an in-app body.</summary>
    internal const int InAppBody = 1_024;

    /// <summary>The UTF-16 code units a language's in-app title and body take at their most characters, a character taking two at most.</summary>
    private const int InAppUnits = 2 * (InAppTitle + InAppBody);

    /// <summary>
    /// Throws <see cref="ArgumentException"/> where a part of <paramref name="chosen"/> in
    /// <paramref name="language"/>, or of the in-app channel in any of
    /// <paramref name="languages"/>, reads a value that neither <paramref name="values"/> nor the
    /// library passes.
    /// </summary>
    internal void CheckPassed(KindDeclaration kind, IReadOnlyList<Channel> chosen, string language, IReadOnlyList<string> languages, IReadOnlyDictionary<string, SendValue> values)
    {
        HashSet<string> passed = new(values.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (Channel channel in chosen)
        {
            foreach (string rendered in Languages(channel, language, languages))
            {
                foreach ((string part, TemplatePart template) in Parts(kind.Name, channel, rendered))
                {
                    if (template.Reads.Passed.FirstOrDefault(name => !passed.Contains(name) && !TemplateNames.Reserved.Contains(name)) is { } missing)
                    {
                        throw new ArgumentException($"The template of {kind.Name}'s {Spellings.Of(channel)} {part} in {rendered} reads the value '{missing}', which the send does not pass.", nameof(values));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Renders each of <paramref name="chosen"/>, or answers the first refusal's code and
    /// channel: the in-app channel in each of <paramref name="languages"/>, its parts keyed by
    /// part and language (<c>title.en</c>), every other channel in <paramref name="language"/>.
    /// An optional category's email reads <paramref name="unsubscribe"/> as <c>unsubscribe</c>.
    /// </summary>
    internal async ValueTask<SendRender> RenderAsync(KindDeclaration kind, IReadOnlyList<Channel> chosen, string language, IReadOnlyList<string> languages, string zone, IReadOnlyDictionary<string, SendValue> values, string? unsubscribe = null)
    {
        string direction = SendLanguage.Direction(language);
        TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById(zone);
        Dictionary<string, FluidValue> converted = new(StringComparer.OrdinalIgnoreCase)
        {
            [TemplateNames.Language] = new StringValue(language),
            [TemplateNames.Direction] = new StringValue(direction),
        };
        if (unsubscribe is not null)
        {
            converted[TemplateNames.Unsubscribe] = new StringValue(unsubscribe);
        }

        HashSet<string> unconverted = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string name, SendValue value) in values)
        {
            if (TemplateValues.Of(value, renderer.MaxSteps) is { } fluid)
            {
                converted[name] = fluid;
            }
            else
            {
                unconverted.Add(name);
            }
        }

        List<RenderedChannel> rendered = [];
        foreach (Channel channel in chosen)
        {
            if (channel == Channel.InApp)
            {
                (Dictionary<string, string>? inApp, string? refused) = await InAppAsync(kind.Name, languages, timeZone, converted, unconverted).ConfigureAwait(false);
                if (refused is not null)
                {
                    return SendRender.Refused(refused, channel);
                }

                rendered.Add(new RenderedChannel(channel, inApp!));
                continue;
            }

            RenderBudget budget = channel switch
            {
                Channel.Email => RenderBudget.Bytes(channels.CurrentValue.Email.MaxBytes),
                Channel.Sms => RenderBudget.Units(RenderBudget.SmsUnits),
                _ => RenderBudget.Bytes(RenderBudget.PushBytes),
            };
            (Dictionary<string, string>? texts, string? refusal) = await PartsAsync(kind.Name, channel, language, converted, unconverted, budget, timeZone).ConfigureAwait(false);
            if (refusal is not null)
            {
                return SendRender.Refused(refusal, channel);
            }

            if (channel == Channel.Sms && SmsText.Measure(texts![TemplateParts.Text]).Segments > kind.Sms!.MaxSegments)
            {
                return SendRender.Refused(Codes.TooLong, channel);
            }

            if (channel == Channel.Push && PushPayload.Of(texts!, language, direction).Length > RenderBudget.PushBytes)
            {
                return SendRender.Refused(Codes.TooLong, channel);
            }

            rendered.Add(new RenderedChannel(channel, texts!));
        }

        return SendRender.Done(rendered);
    }

    /// <summary>The languages <paramref name="channel"/> is rendered in: every declared one for the in-app channel, the send's for any other.</summary>
    private static IReadOnlyList<string> Languages(Channel channel, string language, IReadOnlyList<string> languages)
    {
        return channel == Channel.InApp ? languages : [language];
    }

    /// <summary>
    /// Renders the in-app parts in each of <paramref name="languages"/>, each in its language's
    /// culture with its language and direction as <c>language</c> and <c>direction</c>, keyed by
    /// part and language; or answers the refusal, <c>messaging.send.toolong</c> for a part past
    /// its length in characters in any language.
    /// </summary>
    private async ValueTask<(Dictionary<string, string>? Texts, string? Refusal)> InAppAsync(string kind, IReadOnlyList<string> languages, TimeZoneInfo timeZone, Dictionary<string, FluidValue> converted, HashSet<string> unconverted)
    {
        Dictionary<string, string> texts = new(StringComparer.Ordinal);
        foreach (string language in languages)
        {
            Dictionary<string, FluidValue> own = new(converted, StringComparer.OrdinalIgnoreCase)
            {
                [TemplateNames.Language] = new StringValue(language),
                [TemplateNames.Direction] = new StringValue(SendLanguage.Direction(language)),
            };
            (Dictionary<string, string>? parts, string? refusal) = await PartsAsync(kind, Channel.InApp, language, own, unconverted, RenderBudget.Units(InAppUnits), timeZone).ConfigureAwait(false);
            if (refusal is not null)
            {
                return (null, refusal);
            }

            foreach ((string part, string text) in parts!)
            {
                if (!TextLength.AtMost(text, string.Equals(part, TemplateParts.Title, StringComparison.Ordinal) ? InAppTitle : InAppBody))
                {
                    return (null, Codes.TooLong);
                }

                texts[InAppParts.Field(part, language)] = text;
            }
        }

        return (texts, null);
    }

    /// <summary>Renders <paramref name="channel"/>'s parts in <paramref name="language"/> and its culture within <paramref name="budget"/>, or answers the refusal.</summary>
    private async ValueTask<(Dictionary<string, string>? Texts, string? Refusal)> PartsAsync(string kind, Channel channel, string language, Dictionary<string, FluidValue> converted, HashSet<string> unconverted, RenderBudget budget, TimeZoneInfo timeZone)
    {
        List<(string Part, TemplatePart Template)> parts = Parts(kind, channel, language);
        if (parts.Any(part => part.Template.Reads.All.Overlaps(unconverted)))
        {
            return (null, Codes.RenderLimit);
        }

        CultureInfo culture = SendLanguage.Culture(language);
        Dictionary<string, string> texts = new(StringComparer.Ordinal);
        foreach ((string part, TemplatePart template) in parts)
        {
            bool html = string.Equals(part, TemplateParts.Html, StringComparison.Ordinal);
            Rendering rendering = await renderer.RenderAsync(template.Template, converted, html, budget, culture, timeZone).ConfigureAwait(false);
            if (rendering.Refusal is { } refusal)
            {
                return (null, refusal);
            }

            texts[part] = channel == Channel.Email && string.Equals(part, TemplateParts.Subject, StringComparison.Ordinal) ? HeaderText.Spaced(rendering.Text!) : rendering.Text!;
        }

        return (texts, null);
    }

    private List<(string Part, TemplatePart Template)> Parts(string kind, Channel channel, string language)
    {
        List<(string, TemplatePart)> parts = [];
        foreach (string part in TemplateParts.Required(channel).Concat(TemplateParts.Optional(channel)))
        {
            if (templates.Kind(kind, channel, language, part) is { } template)
            {
                parts.Add((part, template));
            }
        }

        return parts;
    }
}