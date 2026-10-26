using System.Collections.Generic;

namespace Hermes.Sending;

/// <summary>A category as the host declared it (<c>01</c> MSG-SEND-002), checked when the host starts.</summary>
/// <param name="Name">The category's name.</param>
/// <param name="Essential">Whether the person cannot turn it off.</param>
/// <param name="ConsentPurpose">The purpose each delivery needs the recipient's consent to, or none.</param>
/// <param name="QuietHours">Whether quiet hours apply as the host declared it, or none where it did not.</param>
/// <param name="Limits">The caps per destination, or per person for push.</param>
/// <param name="DeploymentLimits">The caps across every destination.</param>
/// <param name="Labels">The category's name as people read it, by language.</param>
internal sealed record CategoryDeclaration(
    string Name,
    bool Essential,
    string? ConsentPurpose,
    bool? QuietHours,
    IReadOnlyList<LimitDeclaration> Limits,
    IReadOnlyList<LimitDeclaration> DeploymentLimits,
    IReadOnlyDictionary<string, string> Labels);