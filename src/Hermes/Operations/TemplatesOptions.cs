using System.ComponentModel.DataAnnotations;

namespace Hermes.Operations;

/// <summary><c>Hermes:Templates</c> (<c>08</c> section 4).</summary>
internal sealed class TemplatesOptions
{
    [Range(1_000, 10_000_000)]
    public int MaxSteps { get; set; } = 100_000;

    [Range(1, 100)]
    public int MaxRecursion { get; set; } = 20;
}