using System.ComponentModel.DataAnnotations;

namespace Hermes.Operations;

/// <summary><c>Hermes:Templates</c> (<c>08</c> section 4).</summary>
internal sealed class TemplatesOptions
{
    /// <summary>The default of <c>Templates:Path</c>.</summary>
    internal const string DefaultPath = "templates";

    /// <summary>The template folder, a relative one taken from the host's content root; checked on the raw value, as its rule reads the file system.</summary>
    public string Path { get; set; } = DefaultPath;

    [Range(1_000, 10_000_000)]
    public int MaxSteps { get; set; } = 100_000;

    [Range(1, 100)]
    public int MaxRecursion { get; set; } = 20;
}