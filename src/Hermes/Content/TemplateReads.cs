using System.Collections.Generic;

namespace Hermes.Content;

/// <summary>The top-level names a template part reads (<c>03</c> MSG-TPL-002, MSG-TPL-004), each compared ignoring case.</summary>
/// <param name="All">Every top-level name it reads.</param>
/// <param name="Passed">Those it does not set itself, nor Fluid's <c>forloop</c>: the ones a send, or the library, passes.</param>
internal sealed record TemplateReads(IReadOnlySet<string> All, IReadOnlySet<string> Passed);