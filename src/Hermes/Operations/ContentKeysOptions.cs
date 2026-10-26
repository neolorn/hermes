using System.ComponentModel.DataAnnotations;

namespace Hermes.Operations;

/// <summary><c>Hermes:Secrets:ContentKeys</c> (<c>08</c> section 4; <c>06</c> MSG-OPS-008).</summary>
internal sealed class ContentKeysOptions
{
    /// <summary>The write version; none where the key's one version is it. Checked only where the key is needed.</summary>
    public int? WriteVersion { get; set; }

    [Range(1L, 4_294_967_296L)]
    public long MaxValues { get; set; } = 2_147_483_648L;

    [Range(100, 1_000_000)]
    public int ReserveBlock { get; set; } = 10_000;
}