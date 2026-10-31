namespace Hermes.Operations;

/// <summary><c>Hermes:InApp</c> (<c>08</c> section 4).</summary>
internal sealed class InAppOptions
{
    /// <summary>Whether the in-app channel of every optional category cannot be turned off (<c>02</c> MSG-INAPP-004).</summary>
    public bool AlwaysOn { get; set; }
}