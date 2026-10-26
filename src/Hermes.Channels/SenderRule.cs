using System;
using System.Collections.Generic;
using System.Linq;

namespace Hermes.Channels;

/// <summary>
/// An SMS sender and the numbers a deployment allows (<c>02</c> MSG-SMS-002): a sender is
/// an alphanumeric name of 1 to 11 characters or a number of 1 to 15 digits (3GPP TS 23.040
/// section 9.1.2.5), and a number is allowed where it starts with one of the allowed
/// prefixes, each <c>+</c> followed by 0 to 6 digits (<c>08</c> section 4).
/// </summary>
internal static class SenderRule
{
    private const int MostNameCharacters = 11;

    private const int MostNumberDigits = 15;

    private const int MostPrefixDigits = 6;

    /// <summary>Whether <paramref name="sender"/> is a sender by the base rule, which a provider may narrow.</summary>
    internal static bool IsSender(string sender)
    {
        ArgumentNullException.ThrowIfNull(sender);

        bool isName = sender.Length is >= 1 and <= MostNameCharacters
            && sender.All(character => char.IsAsciiLetterOrDigit(character) || character == ' ');
        bool isNumber = sender.Length is >= 1 and <= MostNumberDigits && sender.All(char.IsAsciiDigit);
        return isName || isNumber;
    }

    /// <summary>Whether <paramref name="prefix"/> is <c>+</c> followed by 0 to 6 digits.</summary>
    internal static bool IsPrefix(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);

        return prefix.Length is >= 1 and <= MostPrefixDigits + 1 && prefix[0] == '+' && prefix.Skip(1).All(char.IsAsciiDigit);
    }

    /// <summary>Whether <paramref name="number"/> starts with one of <paramref name="prefixes"/>.</summary>
    internal static bool Allows(IEnumerable<string> prefixes, string number)
    {
        ArgumentNullException.ThrowIfNull(prefixes);
        ArgumentNullException.ThrowIfNull(number);

        return prefixes.Any(prefix => number.StartsWith(prefix, StringComparison.Ordinal));
    }
}