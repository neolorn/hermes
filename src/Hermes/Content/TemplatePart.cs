using Fluid;

namespace Hermes.Content;

/// <summary>A template part as it was read, parsed and checked when the application started (<c>03</c> MSG-TPL-004).</summary>
/// <param name="Template">The parsed part.</param>
/// <param name="Reads">The top-level names it reads.</param>
internal sealed record TemplatePart(IFluidTemplate Template, TemplateReads Reads);