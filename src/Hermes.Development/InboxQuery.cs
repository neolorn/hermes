using System;
using System.Globalization;
using System.Text.RegularExpressions;

using Hermes.Channels;
using Hermes.Core;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Hermes.Development;

/// <summary>
/// The query of <c>GET /messages</c> and <c>GET /messages/latest</c> (<c>05</c> MSG-API-007):
/// a parameter outside its rule, or given more than once, is refused naming it; a parameter of
/// neither route is ignored.
/// </summary>
internal static partial class InboxQuery
{
    internal const int DefaultLimit = 100;

    internal const int MostLimit = 1_000;

    internal const int MostWait = 30;

    /// <summary>The filter <paramref name="query"/> gives, or the refusal naming the parameter at fault.</summary>
    internal static (InboxFilter Filter, InboxRefusal? Refusal) Filter(IQueryCollection query)
    {
        InboxFilter filter = new();
        foreach (string name in (string[])["to", "person", "channel", "app", "kind", "after"])
        {
            if (!query.TryGetValue(name, out StringValues values))
            {
                continue;
            }

            if (values.Count != 1 || values[0] is not { Length: > 0 } value)
            {
                return (filter, InboxRefusal.Malformed(name));
            }

            InboxFilter? read = name switch
            {
                "to" => AddressRule.IsAddress(value) || IsPhone(value) ? filter with { To = value } : null,
                "person" => filter with { Person = value },
                "channel" => Channel(value) is { } channel ? filter with { Channel = channel } : null,
                "app" => filter with { App = value },
                "kind" => filter with { Kind = value },
                _ => Instant(value) is { } after ? filter with { After = after } : null,
            };
            if (read is null)
            {
                return (filter, InboxRefusal.Malformed(name));
            }

            filter = read;
        }

        return (filter, null);
    }

    /// <summary>The whole number <paramref name="name"/> holds, from <paramref name="least"/> to <paramref name="most"/>, <paramref name="otherwise"/> where it is not given, or none where it is outside its rule.</summary>
    internal static int? Whole(IQueryCollection query, string name, int least, int most, int otherwise)
    {
        if (!query.TryGetValue(name, out StringValues values))
        {
            return otherwise;
        }

        return values.Count == 1
            && WholeNumber().IsMatch(values[0] ?? string.Empty)
            && int.TryParse(values[0], NumberStyles.None, CultureInfo.InvariantCulture, out int number)
            && number >= least
            && number <= most
            ? number
            : null;
    }

    /// <summary>Whether <paramref name="text"/> is a phone number in E.164: a plus, then 1 to 15 digits, the first not zero.</summary>
    private static bool IsPhone(string text)
    {
        return Phone().IsMatch(text);
    }

    private static Channel? Channel(string text)
    {
        return text switch
        {
            "email" => Core.Channel.Email,
            "sms" => Core.Channel.Sms,
            "push" => Core.Channel.Push,
            _ => null,
        };
    }

    /// <summary>An instant in ISO 8601 with its offset, or none.</summary>
    private static DateTimeOffset? Instant(string text)
    {
        return Offset().IsMatch(text) && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset instant)
            ? instant
            : null;
    }

    [GeneratedRegex(@"^\+[1-9][0-9]{0,14}$", RegexOptions.CultureInvariant)]
    private static partial Regex Phone();

    [GeneratedRegex(@"^[0-9]{1,9}$", RegexOptions.CultureInvariant)]
    private static partial Regex WholeNumber();

    /// <summary>A date, a <c>T</c>, a time and an offset: <c>Z</c> or <c>+hh:mm</c>.</summary>
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d{1,7})?)?(Z|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant)]
    private static partial Regex Offset();
}