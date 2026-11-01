namespace Hermes.Core;

/// <summary>
/// The template part a template source is asked for (<c>03</c> MSG-TPL-001, MSG-TPL-006). A key
/// names one of three things: a kind's part, by <see cref="Kind"/>, <see cref="Channel"/>,
/// language and part; a library page's, by <see cref="Page"/>, language and part; or an
/// automatic reply's, by <see cref="Reply"/> alone with <see cref="Kind"/> and <see cref="Page"/>
/// none, its language and the part <c>text</c> (<c>09</c> MSG-CONV-003). A source that reads
/// <c>Kind == null</c> as a page also reads <see cref="Reply"/>.
/// </summary>
public sealed record TemplateKey
{
    /// <summary>The kind, for a kind's template; none for a page or a reply.</summary>
    public string? Kind { get; init; }

    /// <summary>The channel, for a kind's template; none for a page.</summary>
    public Channel? Channel { get; init; }

    /// <summary>The page, for a library page; none for a kind's template or a reply.</summary>
    public string? Page { get; init; }

    /// <summary>The language, a declared BCP 47 tag.</summary>
    public required string Language { get; init; }

    /// <summary>The part: <c>subject</c>, <c>text</c>, <c>html</c>, <c>title</c>, <c>body</c>, <c>url</c>, <c>tag</c>, <c>icon</c> or <c>badge</c>.</summary>
    public required string Part { get; init; }

    /// <summary>The automatic reply rule's name, for its template; none for a kind's template or a page.</summary>
    public string? Reply { get; init; }
}