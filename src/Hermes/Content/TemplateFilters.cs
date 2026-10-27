using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.Tasks;

using Fluid;
using Fluid.Filters;
using Fluid.Values;

namespace Hermes.Content;

/// <summary>
/// The filters a render may call (<c>03</c> MSG-TPL-007), each a wrapper of Fluid's own, or the
/// library's <c>format_date</c> and <c>format_number</c> (<c>03</c> MSG-TPL-002). A wrapper
/// checks the arguments a filter cannot take before the call, computes the length of a
/// <c>replace</c>, <c>join</c> or <c>url_encode</c> result before it is built, checks every
/// other result, and in an HTML part encodes every value a string filter combines with an
/// already-encoded one.
/// </summary>
internal static class TemplateFilters
{
    /// <summary>The encoder of an HTML part (<c>03</c> MSG-TPL-005): it escapes markup and quotes and leaves every script's letters as they are.</summary>
    internal static readonly HtmlEncoder Html = HtmlEncoder.Create(UnicodeRanges.All);

    private const int MaxDigits = 28;

    /// <summary>Puts the allowed filters, and no other, in <paramref name="filters"/>.</summary>
    internal static void Register(FilterCollection filters)
    {
        ArgumentNullException.ThrowIfNull(filters);
        filters.Clear();
        filters.AddFilter("append", Bounded(StringFilters.Append, 0));
        filters.AddFilter("capitalize", Bounded(StringFilters.Capitalize));
        filters.AddFilter("downcase", Bounded(StringFilters.Downcase));
        filters.AddFilter("upcase", Bounded(StringFilters.Upcase));
        filters.AddFilter("lstrip", Bounded(StringFilters.LStrip));
        filters.AddFilter("rstrip", Bounded(StringFilters.RStrip));
        filters.AddFilter("strip", Bounded(StringFilters.Strip));
        filters.AddFilter("prepend", Bounded(StringFilters.Prepend, 0));
        filters.AddFilter("remove", Bounded(StringFilters.Remove, 0));
        filters.AddFilter("remove_first", Bounded(StringFilters.RemoveFirst, 0));
        filters.AddFilter("replace", Bounded(Replace, 0, 1));
        filters.AddFilter("replace_first", Bounded(StringFilters.ReplaceFirst, 0, 1));
        filters.AddFilter("slice", Bounded(Slice));
        filters.AddFilter("strip_html", Bounded(MiscFilters.StripHtml));
        filters.AddFilter("strip_newlines", Bounded(StringFilters.StripNewLines));
        filters.AddFilter("truncate", Bounded(StringFilters.Truncate, 1));
        filters.AddFilter("truncatewords", Bounded(StringFilters.TruncateWords, 1));
        filters.AddFilter("url_encode", Bounded(UrlEncode));
        filters.AddFilter("abs", Bounded(NumberFilters.Abs));
        filters.AddFilter("at_least", Bounded(NumberFilters.AtLeast));
        filters.AddFilter("at_most", Bounded(NumberFilters.AtMost));
        filters.AddFilter("ceil", Bounded(NumberFilters.Ceil));
        filters.AddFilter("divided_by", Bounded(NumberFilters.DividedBy));
        filters.AddFilter("floor", Bounded(NumberFilters.Floor));
        filters.AddFilter("minus", Bounded(NumberFilters.Minus));
        filters.AddFilter("modulo", Bounded(NumberFilters.Modulo));
        filters.AddFilter("plus", Bounded(NumberFilters.Plus));
        filters.AddFilter("round", Bounded(Round));
        filters.AddFilter("times", Bounded(NumberFilters.Times));
        filters.AddFilter("first", Bounded(ArrayFilters.First));
        filters.AddFilter("last", Bounded(ArrayFilters.Last));
        filters.AddFilter("join", Join);
        filters.AddFilter("map", Bounded(ArrayFilters.Map));
        filters.AddFilter("reverse", Bounded(ArrayFilters.Reverse));
        filters.AddFilter("size", Bounded(ArrayFilters.Size));
        filters.AddFilter("sort", Bounded(ArrayFilters.Sort));
        filters.AddFilter("sort_natural", Bounded(ArrayFilters.SortNatural));
        filters.AddFilter("uniq", Bounded(ArrayFilters.Uniq));
        filters.AddFilter("where", Bounded(ArrayFilters.Where));
        filters.AddFilter("compact", Bounded(MiscFilters.Compact));
        filters.AddFilter("default", Default);
        filters.AddFilter(FilterRules.FormatDate, Bounded(FormatDate));
        filters.AddFilter(FilterRules.FormatNumber, Bounded(FormatNumber));
    }

    /// <summary>
    /// Wraps <paramref name="filter"/>, whose positional arguments at <paramref name="texts"/>
    /// join its result's text: in an HTML part where its input or one of those is already
    /// encoded, each of them not already encoded is encoded first and the result is marked as
    /// encoded; every result is checked against the part's bounds.
    /// </summary>
    private static FilterDelegate Bounded(FilterDelegate filter, params int[] texts)
    {
        return async (input, arguments, context) =>
        {
            RenderContext render = (RenderContext)context;
            if (texts.Length > 0 && render.Html && (IsEncoded(input) || Any(arguments, texts, IsEncoded)))
            {
                FluidValue combined = await filter(Encoded(input), Encoded(arguments, texts), context).ConfigureAwait(false);
                return Checked(new StringValue(combined.ToStringValue(), encode: false), render);
            }

            return Checked(await filter(input, arguments, context).ConfigureAwait(false), render);
        };
    }

    /// <summary>
    /// Fluid's <c>replace</c>, answering its input unchanged where the search is empty, and
    /// refusing a result longer than the part's limit before it is built.
    /// </summary>
    private static ValueTask<FluidValue> Replace(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        RenderContext render = (RenderContext)context;
        string search = arguments.At(0).ToStringValue();
        if (search.Length == 0)
        {
            return ValueTask.FromResult(input);
        }

        string text = input.ToStringValue();
        long found = 0;
        for (int index = text.IndexOf(search, StringComparison.Ordinal); index >= 0; index = text.IndexOf(search, index + search.Length, StringComparison.Ordinal))
        {
            found++;
        }

        RenderBudget budget = render.Budget;
        render.Check(budget.Measure(text) + (found * (budget.Measure(arguments.At(1).ToStringValue()) - budget.Measure(search))));
        return StringFilters.Replace(input, arguments, context);
    }

    /// <summary>
    /// Fluid's <c>join</c>, refusing a result longer than the part's limit before it is built;
    /// in an HTML part where the separator or an item is already encoded, every other is
    /// encoded first and the result is marked as encoded.
    /// </summary>
    private static async ValueTask<FluidValue> Join(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        RenderContext render = (RenderContext)context;
        if (input.Type != FluidValues.Array)
        {
            return Checked(input, render);
        }

        List<FluidValue> items = [.. input.Enumerate(context)];
        bool encode = render.Html && (IsEncoded(arguments.At(0)) || items.Exists(IsEncoded));
        if (encode)
        {
            items = items.ConvertAll(Encoded);
            arguments = Encoded(arguments, 0);
        }

        RenderBudget budget = render.Budget;
        long length = items.Count > 1 ? (items.Count - 1) * budget.Measure(arguments.At(0).ToStringValue()) : 0;
        foreach (FluidValue item in items)
        {
            length += budget.Measure(item.ToStringValue());
            render.Check(length);
        }

        FluidValue joined = await ArrayFilters.Join(new ArrayValue(items), arguments, context).ConfigureAwait(false);
        return encode ? new StringValue(joined.ToStringValue(), encode: false) : joined;
    }

    /// <summary>
    /// Fluid's <c>default</c>; in an HTML part where its input or its default is already
    /// encoded, the one it answers is encoded where it is not, and marked as encoded.
    /// </summary>
    private static async ValueTask<FluidValue> Default(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        RenderContext render = (RenderContext)context;
        FluidValue chosen = await MiscFilters.Default(input, arguments, context).ConfigureAwait(false);
        return Checked(render.Html && (IsEncoded(input) || IsEncoded(arguments.At(0))) ? Encoded(chosen) : chosen, render);
    }

    /// <summary>Fluid's <c>url_encode</c>, refusing a result longer than the part's limit before it is built.</summary>
    private static ValueTask<FluidValue> UrlEncode(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        RenderContext render = (RenderContext)context;
        long length = 0;
        foreach (byte value in Encoding.UTF8.GetBytes(input.ToStringValue()))
        {
            // WebUtility.UrlEncode writes these as they are, a space as '+', and every other byte as %XX.
            length += char.IsAsciiLetterOrDigit((char)value) || value is (byte)' ' or (byte)'-' or (byte)'_' or (byte)'.' or (byte)'!' or (byte)'*' or (byte)'(' or (byte)')' ? 1 : 3;
        }

        render.Check(length);
        return MiscFilters.UrlEncode(input, arguments, context);
    }

    /// <summary>Fluid's <c>slice</c>, refusing an offset or a length that is not a whole number with <c>renderfailed</c>.</summary>
    private static ValueTask<FluidValue> Slice(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        if (!arguments.At(0).IsInteger() || (arguments.Count > 1 && !arguments.At(1).IsInteger()))
        {
            throw ((RenderContext)context).Refuse(Codes.RenderFailed);
        }

        return StringFilters.Slice(input, arguments, context);
    }

    /// <summary>Fluid's <c>round</c>, refusing digits outside 0 to 28 with <c>renderfailed</c>.</summary>
    private static ValueTask<FluidValue> Round(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        if (!arguments.At(0).IsNil())
        {
            int digits = Convert.ToInt32(arguments.At(0).ToNumberValue(), CultureInfo.InvariantCulture);
            if (digits is < 0 or > MaxDigits)
            {
                throw ((RenderContext)context).Refuse(Codes.RenderFailed);
            }
        }

        return NumberFilters.Round(input, arguments, context);
    }

    /// <summary>An instant in the send's time zone, under a .NET date format string, in the send's language's culture; nil for any other value.</summary>
    private static ValueTask<FluidValue> FormatDate(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        if (input is not DateTimeValue || input.ToObjectValue() is not DateTimeOffset instant)
        {
            return ValueTask.FromResult<FluidValue>(NilValue.Instance);
        }

        DateTimeOffset local = TimeZoneInfo.ConvertTime(instant, context.TimeZone);
        return ValueTask.FromResult<FluidValue>(new StringValue(local.ToString(arguments.At(0).ToStringValue(), context.CultureInfo)));
    }

    /// <summary>A number under a .NET numeric format string, in the send's language's culture.</summary>
    private static ValueTask<FluidValue> FormatNumber(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        return ValueTask.FromResult<FluidValue>(new StringValue(input.ToNumberValue().ToString(arguments.At(0).ToStringValue(), context.CultureInfo)));
    }

    /// <summary>Refuses a string longer than the part's limit with <c>toolong</c>, and a list of more than its steps' items with <c>renderlimit</c>.</summary>
    private static FluidValue Checked(FluidValue result, RenderContext render)
    {
        if (result is StringValue)
        {
            render.Check(result.ToStringValue());
        }
        else if (result is ArrayValue array && array.Values.Count > render.MaxSteps)
        {
            throw render.Refuse(Codes.RenderLimit);
        }

        return result;
    }

    private static bool IsEncoded(FluidValue value)
    {
        return value is StringValue { Encode: false };
    }

    private static FluidValue Encoded(FluidValue value)
    {
        return IsEncoded(value) ? value : new StringValue(Html.Encode(value.ToStringValue()), encode: false);
    }

    /// <summary>The arguments, those at <paramref name="texts"/> encoded; none of the filters that combine text reads an argument by name.</summary>
    private static FilterArguments Encoded(FilterArguments arguments, params int[] texts)
    {
        FilterArguments encoded = new();
        for (int index = 0; index < arguments.Count; index++)
        {
            FluidValue value = arguments.At(index);
            encoded.Add(Array.IndexOf(texts, index) >= 0 ? Encoded(value) : value);
        }

        return encoded;
    }

    private static bool Any(FilterArguments arguments, int[] texts, Func<FluidValue, bool> test)
    {
        foreach (int index in texts)
        {
            if (index < arguments.Count && test(arguments.At(index)))
            {
                return true;
            }
        }

        return false;
    }
}