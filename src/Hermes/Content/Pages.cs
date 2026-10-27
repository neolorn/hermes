using System;
using System.Collections.Generic;

namespace Hermes.Content;

/// <summary>The library's own pages and the values each is given (<c>03</c> MSG-TPL-006), each with the one part <c>html</c>.</summary>
internal static class Pages
{
    internal const string Unsubscribe = "unsubscribe";

    internal const string Unsubscribed = "unsubscribed";

    internal const string UnsubscribeInvalid = "unsubscribeinvalid";

    /// <summary>The page's category label.</summary>
    internal const string Category = "category";

    /// <summary>The address the <c>unsubscribe</c> page's form posts to.</summary>
    internal const string Action = "action";

    /// <summary>The pages, in the order <c>03</c> MSG-TPL-006 gives them.</summary>
    internal static IReadOnlyList<string> All { get; } = [Unsubscribe, Unsubscribed, UnsubscribeInvalid];

    /// <summary>Whether <paramref name="page"/> is given <paramref name="value"/>, one of <see cref="Category"/> and <see cref="Action"/>.</summary>
    internal static bool Gives(string page, string value)
    {
        return page switch
        {
            Unsubscribe => true,
            Unsubscribed => string.Equals(value, Category, StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }
}