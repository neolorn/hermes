namespace Hermes.Core;

/// <summary>
/// The template part a template source is asked for (<c>03</c> MSG-TPL-001, MSG-TPL-006): a
/// kind's, by kind, channel, language and part, or a library page's, by page, language and part.
/// </summary>
public sealed record TemplateKey
{
    /// <summary>The kind, for a kind's template; none for a page.</summary>
    public string? Kind { get; init; }

    /// <summary>The channel, for a kind's template; none for a page.</summary>
    public Channel? Channel { get; init; }

    /// <summary>The page, for a library page; none for a kind's template.</summary>
    public string? Page { get; init; }

    /// <summary>The language, a declared BCP 47 tag.</summary>
    public required string Language { get; init; }

    /// <summary>The part: <c>subject</c>, <c>text</c>, <c>html</c>, <c>title</c>, <c>body</c>, <c>url</c>, <c>tag</c>, <c>icon</c> or <c>badge</c>.</summary>
    public required string Part { get; init; }
}