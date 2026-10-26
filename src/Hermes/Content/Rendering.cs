namespace Hermes.Content;

/// <summary>A part's render: its text, or the code the send is refused with (<c>03</c> MSG-TPL-007).</summary>
/// <param name="Text">The rendered text, where the render completed.</param>
/// <param name="Refusal">The code, where it was refused.</param>
internal sealed record Rendering(string? Text, string? Refusal)
{
    internal static Rendering Rendered(string text)
    {
        return new Rendering(text, null);
    }

    internal static Rendering Refused(string code)
    {
        return new Rendering(null, code);
    }
}