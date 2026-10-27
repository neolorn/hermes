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
/// Renders a send's channels in its language when it is recorded (<c>01</c> MSG-SEND-018;
/// <c>03</c> MSG-TPL-002): a value a template reads that the send does not pass is a fault
/// found before any render; each channel's parts are rendered within its limit (<c>02</c>
/// MSG-EMAIL-003, MSG-SMS-001, MSG-PUSH-002; <c>03</c> MSG-TPL-007), the first refusal
/// answering with its code and channel.
/// </summary>
internal sealed class SendRenderer(Templates templates, TemplateRenderer renderer, IOptionsMonitor<ChannelsOptions> channels)
{
    /// <summary>
    /// Throws <see cref="ArgumentException"/> where a part of <paramref name="chosen"/> in
    /// <paramref name="language"/> reads a value that neither <paramref name="values"/> nor the
    /// library passes.
    /// </summary>
    internal void CheckPassed(KindDeclaration kind, IReadOnlyList<Channel> chosen, string language, IReadOnlyDictionary<string, SendValue> values)
    {
        HashSet<string> passed = new(values.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (Channel channel in chosen)
        {
            foreach ((string part, TemplatePart template) in Parts(kind.Name, channel, language))
            {
                if (template.Reads.Passed.FirstOrDefault(name => !passed.Contains(name) && !TemplateNames.Reserved.Contains(name)) is { } missing)
                {
                    throw new ArgumentException($"The template of {kind.Name}'s {Spellings.Of(channel)} {part} in {language} reads the value '{missing}', which the send does not pass.", nameof(values));
                }
            }
        }
    }

    /// <summary>Renders each of <paramref name="chosen"/>, or answers the first refusal's code and channel.</summary>
    internal async ValueTask<SendRender> RenderAsync(KindDeclaration kind, IReadOnlyList<Channel> chosen, string language, string zone, IReadOnlyDictionary<string, SendValue> values)
    {
        CultureInfo culture = SendLanguage.Culture(language);
        string direction = SendLanguage.Direction(language);
        TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById(zone);
        Dictionary<string, FluidValue> converted = new(StringComparer.OrdinalIgnoreCase)
        {
            [TemplateNames.Language] = new StringValue(language),
            [TemplateNames.Direction] = new StringValue(direction),
        };
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
            IReadOnlyList<(string Part, TemplatePart Template)> parts = Parts(kind.Name, channel, language);
            if (parts.Any(part => part.Template.Reads.All.Overlaps(unconverted)))
            {
                return SendRender.Refused(Codes.RenderLimit, channel);
            }

            RenderBudget budget = channel switch
            {
                Channel.Email => RenderBudget.Bytes(channels.CurrentValue.Email.MaxBytes),
                Channel.Sms => RenderBudget.Units(RenderBudget.SmsUnits),
                _ => RenderBudget.Bytes(RenderBudget.PushBytes),
            };
            Dictionary<string, string> texts = new(StringComparer.Ordinal);
            foreach ((string part, TemplatePart template) in parts)
            {
                bool html = string.Equals(part, TemplateParts.Html, StringComparison.Ordinal);
                Rendering rendering = await renderer.RenderAsync(template.Template, converted, html, budget, culture, timeZone).ConfigureAwait(false);
                if (rendering.Refusal is { } refusal)
                {
                    return SendRender.Refused(refusal, channel);
                }

                texts[part] = channel == Channel.Email && string.Equals(part, TemplateParts.Subject, StringComparison.Ordinal) ? HeaderText.Spaced(rendering.Text!) : rendering.Text!;
            }

            if (channel == Channel.Sms && SmsText.Measure(texts[TemplateParts.Text]).Segments > kind.Sms!.MaxSegments)
            {
                return SendRender.Refused(Codes.TooLong, channel);
            }

            if (channel == Channel.Push && PushPayload.Of(texts, language, direction).Length > RenderBudget.PushBytes)
            {
                return SendRender.Refused(Codes.TooLong, channel);
            }

            rendered.Add(new RenderedChannel(channel, texts));
        }

        return SendRender.Done(rendered);
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