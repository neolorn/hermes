using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Hermes.Channels;

/// <summary>
/// Makes the keys of <c>08</c> section 4 by the form of their values, each read from its raw
/// text as the configuration binder reads it, in the invariant culture.
/// </summary>
internal static class ConfigurationKeys
{
    /// <summary>A whole number from <paramref name="least"/> to <paramref name="most"/>; none where its default is none.</summary>
    internal static ConfigurationKey Whole(string path, bool startOnly, int least, int most, int? value)
    {
        return new(
            path,
            startOnly,
            text => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int read) ? read : null,
            read => (int)read >= least && (int)read <= most,
            value);
    }

    /// <summary>A whole number from <paramref name="least"/> to <paramref name="most"/>, beyond an <see cref="int"/>'s range.</summary>
    internal static ConfigurationKey Count(string path, long least, long most, long value)
    {
        return new(
            path,
            false,
            text => long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long read) ? read : null,
            read => (long)read >= least && (long)read <= most,
            value);
    }

    /// <summary>A key whose rule says a decimal number, read in the invariant culture as the binder reads it (<c>08</c> section 4).</summary>
    internal static ConfigurationKey Decimal(string path, double least, double most, double value)
    {
        return new(
            path,
            false,
            text => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double read) ? read : null,
            read => (double)read >= least && (double)read <= most,
            value);
    }

    /// <summary>
    /// A key whose rule says a decimal number, zero or more, in a provider's unit, read as the
    /// binder reads a <see cref="decimal"/>, so its value is never one the binder cannot hold.
    /// </summary>
    internal static ConfigurationKey Amount(string path, decimal value)
    {
        return new(
            path,
            false,
            text => decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal read) ? read : null,
            read => (decimal)read >= 0,
            value);
    }

    /// <summary>A text within <paramref name="within"/>; none by default.</summary>
    internal static ConfigurationKey Text(string path, Func<string, bool> within)
    {
        return new(path, false, text => text, read => within((string)read), null);
    }

    /// <summary>A text within <paramref name="within"/>, taking effect at the next start where <paramref name="startOnly"/>; none by default.</summary>
    internal static ConfigurationKey Text(string path, bool startOnly, Func<string, bool> within)
    {
        return new(path, startOnly, text => text, read => within((string)read), null);
    }

    /// <summary>
    /// One of <paramref name="values"/>, spelt as <c>08</c> section 4 spells it; any other text
    /// does not read, so the binder never meets it.
    /// </summary>
    internal static ConfigurationKey Choice(string path, bool startOnly, IReadOnlyList<string> values, string value)
    {
        return new(path, startOnly, text => values.Contains(text, StringComparer.Ordinal) ? text : null, _ => true, value);
    }

    /// <summary>A flag, start only unless its row of <c>08</c> section 4 gives no such rule (<c>InApp:AlwaysOn</c>).</summary>
    internal static ConfigurationKey Flag(string path, bool value, bool startOnly = true)
    {
        return new(path, startOnly, text => bool.TryParse(text, out bool read) ? read : null, _ => true, value);
    }

    /// <summary>A time of day.</summary>
    internal static ConfigurationKey TimeOfDay(string path, TimeOnly value)
    {
        return new(
            path,
            false,
            text => TimeOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly read) ? read : null,
            _ => true,
            value);
    }

    /// <summary>A duration from <paramref name="least"/> to <paramref name="most"/>; none where its default is none.</summary>
    internal static ConfigurationKey Duration(string path, bool startOnly, TimeSpan least, TimeSpan most, TimeSpan? value)
    {
        return new(
            path,
            startOnly,
            text => TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out TimeSpan read) ? read : null,
            read => (TimeSpan)read >= least && (TimeSpan)read <= most,
            value);
    }
}