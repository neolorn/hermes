using System;
using System.Globalization;
using System.Linq;
using System.Text;

using Hermes.Channels;

namespace Hermes.Sending;

/// <summary>
/// The destinations of a send (<c>01</c> MSG-SEND-003): an address of RFC 5321's mailbox form,
/// at most 254 characters, its domain turned into its ASCII form where given in Unicode, a
/// local part outside ASCII refused; a phone number in E.164.
/// </summary>
internal static class Destinations
{
    private const int MostNumberDigits = 15;

    /// <summary>The address in the form it is sent, its domain in ASCII form, or none where it does not read.</summary>
    internal static string? Email(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        int at = text.LastIndexOf('@');
        if (at <= 0 || at == text.Length - 1)
        {
            return null;
        }

        string local = text[..at];
        string domain = text[(at + 1)..];
        if (!local.All(char.IsAscii))
        {
            return null;
        }

        string address;
        if (domain.All(char.IsAscii))
        {
            address = text;
        }
        else
        {
            try
            {
                address = local + "@" + new IdnMapping { UseStd3AsciiRules = true }.GetAscii(domain);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        return AddressRule.IsAddress(address) ? address : null;
    }

    /// <summary>The form an address is hashed in: as it is sent, in lower case.</summary>
    internal static string EmailHashed(string address)
    {
        Span<char> lower = address.Length <= 256 ? stackalloc char[address.Length] : new char[address.Length];
        Ascii.ToLower(address, lower, out int written);
        return new string(lower[..written]);
    }

    /// <summary>Whether <paramref name="text"/> is a number in E.164: a plus, then 1 to 15 digits, the first not zero.</summary>
    internal static bool IsPhone(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Length is >= 2 and <= MostNumberDigits + 1
            && text[0] == '+'
            && text[1] != '0'
            && text.AsSpan(1).IndexOfAnyExceptInRange('0', '9') < 0;
    }
}