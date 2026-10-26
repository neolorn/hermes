namespace Hermes.Core;

/// <summary>An email address with an optional display name, as a kind's sender or reply address.</summary>
public sealed record EmailAddress
{
    /// <summary>The address.</summary>
    public required string Address { get; init; }

    /// <summary>The name shown with the address, or none.</summary>
    public string? DisplayName { get; init; }
}