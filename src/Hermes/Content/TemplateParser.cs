using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

using Fluid;
using Fluid.Ast;
using Fluid.Values;

namespace Hermes.Content;

/// <summary>
/// Parses a template part under the allowed tags (<c>03</c> MSG-TPL-007): <c>if</c>,
/// <c>unless</c>, <c>case</c>, <c>for</c> with <c>break</c> and <c>continue</c>,
/// <c>assign</c>, <c>capture</c> and <c>comment</c>; any other tag does not parse. The
/// library's own <c>capture</c> block takes Fluid's place, writing into a bounded buffer of
/// its own, and in an HTML part marks what it captured as already encoded.
/// </summary>
internal sealed class TemplateParser
{
    private const string Capture = "capture";

    private static readonly string[] Allowed = ["if", "unless", "case", "for", "break", "continue", "assign", Capture, "comment"];

    private readonly FluidParser parser = new();

    public TemplateParser()
    {
        foreach (string tag in parser.RegisteredTags.Keys.ToArray())
        {
            if (!Allowed.Contains(tag, StringComparer.Ordinal))
            {
                parser.RegisteredTags.Remove(tag);
            }
        }

        parser.RegisterIdentifierBlock(Capture, CaptureAsync);
    }

    /// <summary>The parsed part, or none where <paramref name="text"/> does not parse.</summary>
    internal IFluidTemplate? Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return parser.TryParse(text, out IFluidTemplate template, out _) ? template : null;
    }

    private static async ValueTask<Completion> CaptureAsync(string identifier, IReadOnlyList<Statement> statements, TextWriter writer, TextEncoder encoder, TemplateContext context)
    {
        RenderContext render = (RenderContext)context;
        using BoundedWriter buffer = new(render.Budget.Fresh(), render);
        Completion completion = await statements.RenderStatementsAsync(buffer, encoder, context).ConfigureAwait(false);
        context.SetValue(identifier, new StringValue(buffer.ToString(), encode: !render.Html));
        return completion;
    }
}