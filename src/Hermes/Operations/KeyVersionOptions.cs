namespace Hermes.Operations;

/// <summary>A key's <c>WriteVersion</c> under <c>Hermes:Secrets</c> (<c>08</c> section 4; <c>06</c> MSG-OPS-008).</summary>
internal sealed class KeyVersionOptions
{
    /// <summary>The write version; none where the key's one version is it. Checked only where the key is needed.</summary>
    public int? WriteVersion { get; set; }
}