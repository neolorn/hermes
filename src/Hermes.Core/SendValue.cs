using System;
using System.Collections.Generic;
using System.Text;

namespace Hermes.Core;

/// <summary>
/// A template's value (<c>03</c> MSG-TPL-002): a string, a whole number, a decimal number, a
/// boolean, an instant, or a list of maps of those, a list holding no list. No case prints its
/// value, which is a template's content.
/// </summary>
public abstract record SendValue
{
    private SendValue()
    {
    }

    /// <summary>A string.</summary>
    /// <param name="Value">The string.</param>
    public sealed record PlainText(string Value) : SendValue
    {
        /// <inheritdoc/>
        protected override bool PrintMembers(StringBuilder builder)
        {
            return false;
        }
    }

    /// <summary>A whole number.</summary>
    /// <param name="Value">The number.</param>
    public sealed record WholeNumber(long Value) : SendValue
    {
        /// <inheritdoc/>
        protected override bool PrintMembers(StringBuilder builder)
        {
            return false;
        }
    }

    /// <summary>A decimal number.</summary>
    /// <param name="Value">The number.</param>
    public sealed record DecimalNumber(decimal Value) : SendValue
    {
        /// <inheritdoc/>
        protected override bool PrintMembers(StringBuilder builder)
        {
            return false;
        }
    }

    /// <summary>A boolean.</summary>
    /// <param name="Value">The boolean.</param>
    public sealed record Flag(bool Value) : SendValue
    {
        /// <inheritdoc/>
        protected override bool PrintMembers(StringBuilder builder)
        {
            return false;
        }
    }

    /// <summary>An instant.</summary>
    /// <param name="Value">The instant.</param>
    public sealed record Instant(DateTimeOffset Value) : SendValue
    {
        /// <inheritdoc/>
        protected override bool PrintMembers(StringBuilder builder)
        {
            return false;
        }
    }

    /// <summary>A list of maps whose values are of the other cases, a list's entries holding no list.</summary>
    /// <param name="Items">The maps, each by entry name.</param>
    public sealed record List(IReadOnlyList<IReadOnlyDictionary<string, SendValue>> Items) : SendValue
    {
        /// <inheritdoc/>
        protected override bool PrintMembers(StringBuilder builder)
        {
            return false;
        }
    }
}