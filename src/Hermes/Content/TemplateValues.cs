using System;
using System.Collections.Generic;

using Fluid.Values;

using Hermes.Core;

namespace Hermes.Content;

/// <summary>
/// The send's values as Fluid reads them (<c>03</c> MSG-TPL-002): a list's maps compare their
/// entry names ignoring case, as Fluid compares the top-level names, and a list of more items
/// than <c>Hermes:Templates:MaxSteps</c> is not converted, the send being refused with
/// <c>renderlimit</c> before the render, since no render could go through it within its steps.
/// </summary>
internal static class TemplateValues
{
    /// <summary>The value Fluid reads for <paramref name="value"/>, or none where it holds a list of more than <paramref name="maxSteps"/> items.</summary>
    internal static FluidValue? Of(SendValue value, int maxSteps)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value switch
        {
            SendValue.PlainText text => new StringValue(text.Value),
            SendValue.WholeNumber number => NumberValue.Create(number.Value),
            SendValue.DecimalNumber number => NumberValue.Create(number.Value),
            SendValue.Flag flag => BooleanValue.Create(flag.Value),
            SendValue.Instant instant => new DateTimeValue(instant.Value),
            SendValue.List list => List(list, maxSteps),
            _ => throw new ArgumentException("A value is of a case the library does not know.", nameof(value)),
        };
    }

    private static ArrayValue? List(SendValue.List list, int maxSteps)
    {
        if (list.Items.Count > maxSteps)
        {
            return null;
        }

        FluidValue[] items = new FluidValue[list.Items.Count];
        for (int index = 0; index < items.Length; index++)
        {
            Dictionary<string, FluidValue> entries = new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, SendValue> entry in list.Items[index])
            {
                if (Of(entry.Value, maxSteps) is not { } converted)
                {
                    return null;
                }

                entries[entry.Key] = converted;
            }

            items[index] = new DictionaryValue(new Map(entries));
        }

        return new ArrayValue(items);
    }

    /// <summary>A list's map, its entries found by name ignoring case.</summary>
    private sealed class Map(Dictionary<string, FluidValue> entries) : IFluidIndexable
    {
        public int Count => entries.Count;

        public IEnumerable<string> Keys => entries.Keys;

        public bool TryGetValue(string name, out FluidValue value)
        {
            return entries.TryGetValue(name, out value!);
        }
    }
}