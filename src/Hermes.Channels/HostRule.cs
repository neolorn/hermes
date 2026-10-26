using System;
using System.Collections.Generic;
using System.Linq;

namespace Hermes.Channels;

/// <summary>
/// A host name as <c>Email:Host</c> and <c>Push:AllowedHosts</c> take one (<c>08</c>
/// section 4): an ASCII DNS name by RFC 1123 section 2.1, whose last label is not all
/// digits, so no IPv4 address is one, and the allowed-host entries, where a leading
/// <c>*.</c> matches any subdomain and covers at least three labels after it
/// (<c>02</c> MSG-PUSH-004).
/// </summary>
internal static class HostRule
{
    private const int MostCharacters = 253;

    private const int MostLabelCharacters = 63;

    private const int LeastWildcardLabels = 3;

    private const string Wildcard = "*.";

    /// <summary>Whether <paramref name="text"/> is a host name.</summary>
    internal static bool IsHostName(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length is 0 or > MostCharacters)
        {
            return false;
        }

        string[] labels = text.Split('.');
        return labels.All(IsLabel) && !labels[^1].All(char.IsAsciiDigit);
    }

    /// <summary>Whether <paramref name="entry"/> is an allowed-host entry: a host name, or <c>*.</c> before one of at least three labels.</summary>
    internal static bool IsAllowedHostEntry(string entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (!entry.StartsWith(Wildcard, StringComparison.Ordinal))
        {
            return IsHostName(entry);
        }

        string suffix = entry[Wildcard.Length..];
        return IsHostName(suffix) && suffix.Split('.').Length >= LeastWildcardLabels;
    }

    /// <summary>Whether <paramref name="host"/> is one <paramref name="entries"/> allow; host names compare without regard to case.</summary>
    internal static bool Allows(IEnumerable<string> entries, string host)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(host);

        return IsHostName(host) && entries.Any(entry => Matches(entry, host));
    }

    private static bool Matches(string entry, string host)
    {
        if (!entry.StartsWith(Wildcard, StringComparison.Ordinal))
        {
            return string.Equals(entry, host, StringComparison.OrdinalIgnoreCase);
        }

        // The dot stays with the suffix, so the entry matches a subdomain and never the name itself.
        string suffix = entry[1..];
        return host.Length > suffix.Length && host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLabel(string label)
    {
        return label.Length is >= 1 and <= MostLabelCharacters
            && label[0] != '-'
            && label[^1] != '-'
            && label.All(character => char.IsAsciiLetterOrDigit(character) || character == '-');
    }
}