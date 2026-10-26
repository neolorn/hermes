using System.ComponentModel.DataAnnotations;

namespace Hermes.Operations;

/// <summary><c>Hermes:Database</c> (<c>08</c> section 4).</summary>
internal sealed class DatabaseOptions
{
    [Range(0, 10)]
    public int Retries { get; set; } = 3;
}