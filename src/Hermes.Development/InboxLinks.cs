using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using MimeKit.Text;

namespace Hermes.Development;

/// <summary>
/// The links of a captured message (<c>02</c> MSG-DEV-003): every absolute <c>http</c> and
/// <c>https</c> address it carries, each once, in the order met: an email's <c>href</c>
/// attributes of its HTML, then the addresses in its text, then a push's <c>url</c>.
/// </summary>
internal static partial class InboxLinks
{
    /// <summary>The links of a message of <paramref name="html"/>, <paramref name="text"/> and <paramref name="url"/>, each of which may be none.</summary>
    internal static IReadOnlyList<string> Of(string? html, string? text, string? url)
    {
        List<string> links = [];
        HashSet<string> met = new(StringComparer.Ordinal);
        if (html is not null)
        {
            HtmlTokenizer tokenizer = new(new StringReader(html));
            while (tokenizer.ReadNextToken(out HtmlToken? token))
            {
                if (token is HtmlTagToken { IsEndTag: false } tag)
                {
                    foreach (HtmlAttribute attribute in tag.Attributes)
                    {
                        if (attribute.Id == HtmlAttributeId.Href && attribute.Value is { } href)
                        {
                            Add(links, met, href);
                        }
                    }
                }
            }
        }

        if (text is not null)
        {
            foreach (Match address in Address().Matches(text))
            {
                Add(links, met, address.Value.TrimEnd('.', ',', ';', ':', '!', '?'));
            }
        }

        if (url is not null)
        {
            Add(links, met, url);
        }

        return links;
    }

    private static void Add(List<string> links, HashSet<string> met, string candidate)
    {
        string link = candidate.Trim();
        if (Uri.TryCreate(link, UriKind.Absolute, out Uri? address)
            && (address.Scheme == Uri.UriSchemeHttp || address.Scheme == Uri.UriSchemeHttps)
            && met.Add(link))
        {
            links.Add(link);
        }
    }

    /// <summary>An <c>http</c> or <c>https</c> address in text, up to the first space, quote, bracket or angle bracket.</summary>
    [GeneratedRegex(@"https?://[^\s<>""'()\[\]{}]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Address();
}