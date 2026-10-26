using System;

namespace Hermes.Sending;

/// <summary>
/// The context a stored value is protected under, <c>&lt;table&gt;:&lt;row id&gt;:&lt;field&gt;</c>,
/// so a value copied to another row or field does not open (<c>01</c> MSG-SEND-018).
/// </summary>
internal static class ContentContext
{
    /// <summary>The table a delivery's values are bound to, whatever the table is called.</summary>
    internal const string Deliveries = "deliveries";

    /// <summary>The table a push subscription's values are bound to, whatever the table is called.</summary>
    internal const string Subscriptions = "subscriptions";

    /// <summary>The context of <paramref name="field"/> in the row <paramref name="row"/> of <paramref name="table"/>, its id in lower-case hexadecimal, hyphenated 8-4-4-4-12.</summary>
    internal static string Of(string table, Guid row, string field)
    {
        return $"{table}:{row:D}:{field}";
    }
}