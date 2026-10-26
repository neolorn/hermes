using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

using Fluid;
using Fluid.Values;

using Hermes.Operations;

using Microsoft.Extensions.Options;

namespace Hermes.Content;

/// <summary>
/// Renders a template part strictly and within its bounds (<c>03</c> MSG-TPL-002,
/// MSG-TPL-005, MSG-TPL-007): Fluid's <c>StrictVariables</c> and <c>StrictFilters</c> set, its
/// steps and depth the configured limits, its clock the library's <see cref="TimeProvider"/>,
/// an HTML part under the HTML encoder and every other part unencoded. A render past its steps
/// answers <c>renderlimit</c>; one whose buffer or filter result passes the channel's limit,
/// <c>toolong</c>; one that reads a member missing from passed data or a name not set, or whose
/// filter cannot compute, <c>renderfailed</c>; any other exception is thrown on.
/// </summary>
internal sealed class TemplateRenderer
{
    private readonly TemplateOptions options;

    public TemplateRenderer(IOptionsMonitor<MessagingOptions> monitor, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        ArgumentNullException.ThrowIfNull(clock);

        // The limits are read at start only (08 section 4).
        TemplatesOptions templates = monitor.CurrentValue.Templates;
        options = new TemplateOptions
        {
            StrictVariables = true,
            StrictFilters = true,
            MaxSteps = templates.MaxSteps,
            MaxRecursion = templates.MaxRecursion,
            Now = clock.GetUtcNow,
        };
        TemplateFilters.Register(options.Filters);
    }

    /// <summary>The configured <c>Hermes:Templates:MaxSteps</c>.</summary>
    internal int MaxSteps => options.MaxSteps;

    /// <summary>
    /// Renders <paramref name="template"/> with <paramref name="values"/>, in
    /// <paramref name="culture"/> and <paramref name="zone"/>, its buffer taking from
    /// <paramref name="budget"/>; answers its text, or the code it is refused with.
    /// </summary>
    internal async ValueTask<Rendering> RenderAsync(
        IFluidTemplate template,
        IReadOnlyDictionary<string, FluidValue> values,
        bool html,
        RenderBudget budget,
        CultureInfo culture,
        TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(values);

        RenderContext context = new(options, budget, html)
        {
            CultureInfo = culture,
            TimeZone = zone,
        };
        foreach (KeyValuePair<string, FluidValue> value in values)
        {
            context.SetValue(value.Key, value.Value);
        }

        TextEncoder encoder = html ? TemplateFilters.Html : NullEncoder.Default;
        using BoundedWriter writer = new(budget, context);
        try
        {
            await template.RenderAsync(writer, encoder, context).ConfigureAwait(false);
        }
        catch (InvalidOperationException) when (context.Refusal is { } refusal)
        {
            return Rendering.Refused(refusal);
        }
        catch (InvalidOperationException) when (context.PastSteps)
        {
            return Rendering.Refused(Codes.RenderLimit);
        }
        catch (FluidException)
        {
            // Only strict variables throw it at render: the startup walk leaves no unknown filter.
            return Rendering.Refused(Codes.RenderFailed);
        }
        catch (ArithmeticException)
        {
            return Rendering.Refused(Codes.RenderFailed);
        }

        return Rendering.Rendered(writer.ToString());
    }
}