using System;

namespace Hermes.Channels;

/// <summary>
/// An email address as <c>02</c> MSG-EMAIL-002 gives it: an RFC 5322 section 3.4.1
/// <c>addr-spec</c> of at most 254 characters. Since RFC 5322 section 4 has an
/// implementation generate none of its obsolete forms, and a comment or a folded line in a
/// sender is no part of the address, the rule takes neither: a dot-atom or quoted local
/// part, and a dot-atom or literal domain, each in ASCII.
/// </summary>
internal static class AddressRule
{
    private const int MostCharacters = 254;

    /// <summary>Whether <paramref name="text"/> is an address.</summary>
    internal static bool IsAddress(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length > MostCharacters)
        {
            return false;
        }

        // A quoted local part and a domain literal may each hold an @, so every @ is tried as the separator.
        for (int at = text.IndexOf('@', StringComparison.Ordinal); at >= 0; at = text.IndexOf('@', at + 1))
        {
            ReadOnlySpan<char> local = text.AsSpan(0, at);
            ReadOnlySpan<char> domain = text.AsSpan(at + 1);
            if ((IsDotAtom(local) || IsQuoted(local)) && (IsDotAtom(domain) || IsLiteral(domain)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>One or more runs of <c>atext</c>, a single dot between each two.</summary>
    private static bool IsDotAtom(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty || text[0] == '.' || text[^1] == '.')
        {
            return false;
        }

        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];
            if (character == '.')
            {
                if (text[index - 1] == '.')
                {
                    return false;
                }
            }
            else if (!IsAtext(character))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>A quoted string: <c>qtext</c>, quoted pairs and white space between quotes.</summary>
    private static bool IsQuoted(ReadOnlySpan<char> text)
    {
        if (text.Length < 2 || text[0] != '"' || text[^1] != '"')
        {
            return false;
        }

        ReadOnlySpan<char> inner = text[1..^1];
        for (int index = 0; index < inner.Length; index++)
        {
            char character = inner[index];
            if (character == '\\')
            {
                index++;
                if (index == inner.Length || !(IsVisible(inner[index]) || IsWhiteSpace(inner[index])))
                {
                    return false;
                }
            }
            else if (!(IsQtext(character) || IsWhiteSpace(character)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>A domain literal: <c>dtext</c> and white space between brackets.</summary>
    private static bool IsLiteral(ReadOnlySpan<char> text)
    {
        if (text.Length < 2 || text[0] != '[' || text[^1] != ']')
        {
            return false;
        }

        foreach (char character in text[1..^1])
        {
            if (!(IsDtext(character) || IsWhiteSpace(character)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAtext(char character)
    {
        return char.IsAsciiLetterOrDigit(character) || "!#$%&'*+-/=?^_`{|}~".Contains(character, StringComparison.Ordinal);
    }

    private static bool IsQtext(char character)
    {
        return character is (char)33 or (>= (char)35 and <= (char)91) or (>= (char)93 and <= (char)126);
    }

    private static bool IsDtext(char character)
    {
        return character is (>= (char)33 and <= (char)90) or (>= (char)94 and <= (char)126);
    }

    private static bool IsVisible(char character)
    {
        return character is >= (char)33 and <= (char)126;
    }

    private static bool IsWhiteSpace(char character)
    {
        return character is ' ' or '\t';
    }
}