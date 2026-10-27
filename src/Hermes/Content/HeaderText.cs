using Hermes.Core;

namespace Hermes.Content;

/// <summary>
/// The text of an email's header (<c>03</c> MSG-TPL-005): in a subject and in a display name,
/// every control character, U+0000 to U+001F and U+007F, is replaced by a space, so no value
/// can start a header of its own.
/// </summary>
internal static class HeaderText
{
    /// <summary><paramref name="text"/> with each control character replaced by a space.</summary>
    internal static string Spaced(string text)
    {
        return string.Create(text.Length, text, static (written, source) =>
        {
            for (int index = 0; index < source.Length; index++)
            {
                written[index] = source[index] is < ' ' or '\u007F' ? ' ' : source[index];
            }
        });
    }

    /// <summary><paramref name="address"/> with its display name's control characters replaced by spaces.</summary>
    internal static EmailAddress? Spaced(EmailAddress? address)
    {
        return address is { DisplayName: { } name } ? address with { DisplayName = Spaced(name) } : address;
    }
}